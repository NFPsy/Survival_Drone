using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using SurvivalDrone.Core;

namespace SurvivalDrone.UI
{
    // P 키를 누르면 일시정지(PAUSE) 화면을 띄우고 게임 시간을 멈추는(Time.timeScale = 0) 스크립트.
    // 계속하기/재시작/메인메뉴 3개 버튼을 제공하며, P를 다시 누르거나 "계속하기"를 누르면 풀린다.
    public class PauseController : MonoBehaviour
    {
        // "메인메뉴" 버튼을 눌렀을 때 돌아갈 씬 이름.
        [SerializeField] private string mainMenuSceneName = "MainMenu";

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
        // 선택했던 난이도는 PlayerPrefs에 저장되어 있어서, 씬을 새로 불러와도 그대로 유지된다.
        private void Restart()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // "메인메뉴" 버튼: 타이틀 화면으로 돌아간다.
        private void GoToMainMenu()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
