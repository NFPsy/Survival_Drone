using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using SurvivalDrone.Core;
using SurvivalDrone.Player;
using SurvivalDrone.Drones;
using SurvivalDrone.LevelUp;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // P 키를 누르면 일시정지(PAUSE) 화면을 띄우고 게임 시간을 멈추는(Time.timeScale = 0) 스크립트.
    // 계속하기/재시작/메인메뉴 3개 버튼을 제공하며, P를 다시 누르거나 "계속하기"를 누르면 풀린다.
    public class PauseController : MonoBehaviour
    {
        // "메인메뉴" 버튼을 눌렀을 때 돌아갈 씬 이름.
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        // "로비" 버튼을 눌렀을 때 돌아갈 씬 이름. (메인메뉴를 거치면 "게임 시작 → 슬롯 선택"을 다시 눌러야 해서 로비로 바로 가는 버튼을 따로 뒀다)
        [SerializeField] private string lobbySceneName = "Lobby";

        // 버튼을 누를 때마다 재생할 공용 클릭음.
        [SerializeField] private AudioClip clickSound;

        // 일시정지 화면 전체 패널 (평소엔 꺼져 있다가 P를 누르면 켜짐).
        private GameObject pausePanel;

        // 지금 일시정지 상태인지 여부.
        private bool isPaused;

        private void Awake()
        {
            // "PausePanel" 자식을 못 찾으면(이름이 바뀌었거나 지워졌으면) 경고만 남기고
            // 이 컴포넌트는 아무 동작도 하지 않도록 한다 — 예외로 게임 전체가 멈추는 것보다 안전하다.
            var pausePanelTransform = transform.Find("PausePanel");
            if (pausePanelTransform == null)
            {
                Debug.LogWarning("[PauseController] 'PausePanel' 자식 오브젝트를 찾지 못해 일시정지 기능이 비활성화됩니다.");
                // Update()가 계속 돌면서 null인 pausePanel을 건드리지 않도록 컴포넌트 자체를 꺼버린다.
                enabled = false;
                return;
            }

            pausePanel = pausePanelTransform.gameObject;
            pausePanel.SetActive(false);

            WireButton("BtnContinue", Resume);
            WireButton("BtnRestart", Restart);
            WireButton("BtnLobby", GoToLobby);
            WireButton("BtnMainMenu", GoToMainMenu);
        }

        // pausePanel 아래에서 buttonName인 버튼을 찾아 클릭 이벤트를 연결하는 함수.
        // 못 찾으면 경고 로그만 남기고 조용히 건너뛴다.
        private void WireButton(string buttonName, UnityEngine.Events.UnityAction action)
        {
            var buttonTransform = pausePanel.transform.Find(buttonName);
            var button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogWarning($"[PauseController] 'PausePanel/{buttonName}' 버튼을 찾지 못했습니다.");
                return;
            }
            button.onClick.AddListener(action);
        }

        private void Update()
        {
            // 새 Input System에서 키보드 P키가 이번 프레임에 눌렸는지 확인.
            if (Keyboard.current == null || !Keyboard.current.pKey.wasPressedThisFrame) return;

            if (isPaused)
            {
                Resume();
                return;
            }

            // Time.timeScale이 이미 0이면(레벨업 선택 화면이 떠서 멈춘 상태 등) 우리가 새로 일시정지를
            // 걸지 않는다. 그리고 승리/패배 화면이 뜬 뒤(State != Playing)에도 일시정지를 막는다.
            bool alreadyStoppedByOther = Time.timeScale == 0f;
            bool isPlaying = GameManager.Instance != null && GameManager.Instance.State == MatchState.Playing;
            if (!alreadyStoppedByOther && isPlaying)
            {
                Pause();
            }
        }

        // 일시정지 화면을 띄우고 시간을 멈춘다.
        private void Pause()
        {
            isPaused = true;
            pausePanel.SetActive(true);
            Time.timeScale = 0f;
        }

        // 일시정지 화면을 끄고 시간을 다시 흐르게 한다.
        private void Resume()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            isPaused = false;
            pausePanel.SetActive(false);
            Time.timeScale = 1f;
        }

        // "재시작" 버튼: 지금 플레이 중인 씬을 그대로 다시 불러온다.
        private void Restart()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            RecordAbandon("재시작");
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // "로비" 버튼: 로비(스테이지 선택·뽑기·격납고가 있는 허브)로 바로 간다. 저장 슬롯은 그대로 유지된다.
        // 판 도중에 나가는 것이므로 "메인메뉴"·"재시작"과 같이 이탈 기록을 남긴다.
        private void GoToLobby()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            RecordAbandon("로비");
            Time.timeScale = 1f;
            SceneManager.LoadScene(lobbySceneName);
        }

        // "메인메뉴" 버튼: 타이틀 화면으로 돌아간다.
        private void GoToMainMenu()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            RecordAbandon("메인메뉴");
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        // 판 도중에 나간다는 사실을 테스트(CBT) 기록에 남긴다.
        // 사망·클리어로 끝난 판은 결과 화면(ResultPanel)이 기록하지만, 일시정지 메뉴에서 나가면 아무 기록도 안 남아서
        // "어디서 지루해서/어려워서 나갔는지"를 알 수 없었다. 그래서 나가기 직전에 한 줄을 남긴다.
        private void RecordAbandon(string exitMethod)
        {
            // 판이 이미 끝났거나, 로비를 거치지 않고 InGame 씬만 단독으로 실행한 경우(저장 데이터 없음)에는 남기지 않는다.
            var game = GameManager.Instance;
            if (game == null || game.State != MatchState.Playing || CurrencyManager.Instance == null) return;

            int stageNumber = StageProgress.Instance != null ? StageProgress.Instance.SelectedIndex + 1 : 0;
            int combatPower = DroneInventory.Instance != null ? DroneInventory.Instance.TotalCombatPower : 0;

            // 누르는 순간 딱 한 번만 찾으면 되므로 씬에서 직접 찾는다. (매 프레임 부르는 곳이 아니라서 부담이 없다)
            var experience = FindFirstObjectByType<PlayerExperience>();
            var droneManager = FindFirstObjectByType<DroneManager>();
            int level = experience != null ? experience.Level : 1;
            int droneCount = droneManager != null ? droneManager.OwnedCount : 0;

            PlayLog.RecordAbandon(SaveManager.Data, stageNumber, game.ElapsedTime, game.CurrentRealElapsedSeconds, level, droneCount, combatPower, exitMethod, LevelUpPickLog.BuildSummary());
            SaveManager.Save();
        }
    }
}
