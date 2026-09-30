using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SurvivalDrone.Core;
using SurvivalDrone.Player;
using SurvivalDrone.Drones;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 게임이 끝났을 때(승리 또는 패배) 결과 화면을 보여주고,
    // 이번 판에서 얻은 보상(코어·크레딧)을 보여주고, "로비로"/"재도전" 버튼으로 다음 행동을 고를 수 있게 해주는 스크립트.
    public class ResultPanel : MonoBehaviour
    {
        // 결과 화면 전체 패널 (평소엔 꺼져 있다가 게임이 끝나면 켜짐).
        [SerializeField] private GameObject panel;

        // "승리" 또는 "패배" 문구를 보여줄 텍스트.
        [SerializeField] private Text resultText;

        // 생존 시간/도달 레벨/보유 드론 수 같은 이번 판의 기록을 보여줄 텍스트.
        [SerializeField] private Text statsText;

        // 게임 상태(진행중/승리/패배)가 바뀌는 것을 감지하기 위한 GameManager 연결.
        [SerializeField] private GameManager gameManager;

        // 도달 레벨을 읽어오기 위한 플레이어 경험치 연결.
        [SerializeField] private PlayerExperience playerExperience;

        // 보유 드론 수를 읽어오기 위한 드론 매니저 연결.
        [SerializeField] private DroneManager droneManager;

        // "재도전" 버튼: 같은 스테이지를 처음부터 다시 시작한다.
        [SerializeField] private Button restartButton;

        // "로비로" 버튼: 로비(Lobby 씬)로 돌아간다. 방금 얻은 재화가 반영된 상태로 돌아간다.
        // (예전 이름 mainMenuButton으로 씬에 저장된 연결이 끊기지 않도록 FormerlySerializedAs를 붙였다)
        [FormerlySerializedAs("mainMenuButton")]
        [SerializeField] private Button lobbyButton;

        // "획득 보상" 영역: 이번 판에서 받은 코어·크레딧을 0에서부터 숫자가 올라가는 연출로 보여준다.
        [SerializeField] private GameObject rewardBox;
        [SerializeField] private Text coreRewardText;
        [SerializeField] private Text creditRewardText;

        // 보상 숫자가 0에서 목표값까지 올라가는 데 걸리는 시간(초).
        [SerializeField] private float rewardCountUpSeconds = 0.8f;

        // 로비 씬 이름.
        [SerializeField] private string lobbySceneName = "Lobby";

        // 승리했을 때 결과 문구에 쓸 색(청록색 계열 - "성공"의 느낌).
        [SerializeField] private Color victoryColor = new Color(0.4f, 0.95f, 1f);

        // 패배했을 때 결과 문구에 쓸 색(붉은색 계열 - "위험/실패"의 느낌).
        [SerializeField] private Color defeatColor = new Color(1f, 0.35f, 0.35f);

        // 버튼을 누를 때마다 재생할 공용 클릭음.
        [SerializeField] private AudioClip clickSound;

        private void OnEnable()
        {
            // 게임 상태가 바뀔 때마다 HandleStateChanged가 자동으로 호출되도록 연결.
            if (gameManager != null) gameManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            // 오브젝트가 사라질 때는 반드시 구독을 해제한다.
            if (gameManager != null) gameManager.OnStateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            // 게임 시작 시에는 결과 화면을 꺼둔다.
            if (panel != null) panel.SetActive(false);

            // 버튼 클릭 시 실행할 함수를 한 번만 연결해둔다.
            if (restartButton != null) restartButton.onClick.AddListener(RestartGame);
            if (lobbyButton != null) lobbyButton.onClick.AddListener(GoToLobby);
        }

        // 게임 상태가 바뀔 때 호출되는 함수.
        private void HandleStateChanged(MatchState state)
        {
            // 아직 "진행 중" 상태면(=게임이 끝난 게 아니면) 아무것도 하지 않는다.
            if (state == MatchState.Playing) return;

            bool won = state == MatchState.Won;

            // 게임이 끝났으면(승리 또는 패배) 결과 패널을 켜고 알맞은 문구/색을 표시한다.
            if (panel != null) panel.SetActive(true);
            if (resultText != null)
            {
                resultText.text = won ? "GAME CLEAR" : "GAME OVER";
                resultText.color = won ? victoryColor : defeatColor;
            }

            if (statsText != null) statsText.text = BuildStatsText();

            ShowReward();
            RecordMatchLog(won);
        }

        // 테스트(CBT) 기록에 이번 판 결과를 남긴다. (스테이지, 결과, 생존 시간, 도달 레벨, 내 전투력, 받은 보상)
        // 메타 시스템 없이 InGame만 단독 실행한 경우에는 남길 저장 데이터가 없으므로 건너뛴다.
        private void RecordMatchLog(bool won)
        {
            if (CurrencyManager.Instance == null) return;

            int stageNumber = StageProgress.Instance != null ? StageProgress.Instance.SelectedIndex + 1 : 0;
            int combatPower = DroneInventory.Instance != null ? DroneInventory.Instance.TotalCombatPower : 0;
            float elapsed = gameManager != null ? gameManager.ElapsedTime : 0f;
            int level = playerExperience != null ? playerExperience.Level : 1;
            int droneCount = droneManager != null ? droneManager.OwnedCount : 0;
            int rewardCore = gameManager != null ? gameManager.RewardCore : 0;
            int rewardCredit = gameManager != null ? gameManager.RewardCredit : 0;

            PlayLog.RecordMatch(SaveManager.Data, stageNumber, won, elapsed, level, droneCount, combatPower, rewardCore, rewardCredit);
            SaveManager.Save();
        }

        // 획득 보상 영역을 켜고 숫자 올라가는 연출을 시작한다.
        // 보상이 없으면(CurrencyManager 없이 InGame만 단독 실행한 경우) 영역 자체를 숨긴다.
        private void ShowReward()
        {
            int core = gameManager != null ? gameManager.RewardCore : 0;
            int credit = gameManager != null ? gameManager.RewardCredit : 0;

            if (rewardBox != null) rewardBox.SetActive(core > 0 || credit > 0);
            if (core <= 0 && credit <= 0) return;

            StartCoroutine(CountUpRoutine(core, credit));
        }

        // 코어·크레딧 숫자를 0에서 목표값까지 올린다.
        // 결과 화면이 뜰 때는 Time.timeScale이 0이라서 시간이 멈춰도 흐르는 unscaledDeltaTime을 써야 재생된다.
        private IEnumerator CountUpRoutine(int coreTarget, int creditTarget)
        {
            float elapsed = 0f;
            while (elapsed < rewardCountUpSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / rewardCountUpSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f); // 처음엔 빠르게, 끝에서 천천히 멈추는 느낌
                SetRewardTexts(Mathf.RoundToInt(coreTarget * eased), Mathf.RoundToInt(creditTarget * eased));
                yield return null;
            }
            SetRewardTexts(coreTarget, creditTarget);
        }

        private void SetRewardTexts(int core, int credit)
        {
            if (coreRewardText != null) coreRewardText.text = $"코어      + {core:N0}";
            if (creditRewardText != null) creditRewardText.text = $"크레딧    + {credit:N0}";
        }

        // 생존 시간/도달 레벨/보유 드론 수를 "분:초 · Lv.n · 드론 n종" 형태의 문구로 만드는 함수.
        private string BuildStatsText()
        {
            float elapsed = gameManager != null ? gameManager.ElapsedTime : 0f;
            int minutes = Mathf.FloorToInt(elapsed / 60f);
            int seconds = Mathf.FloorToInt(elapsed % 60f);

            int level = playerExperience != null ? playerExperience.Level : 1;
            int droneCount = droneManager != null ? droneManager.OwnedCount : 0;

            return $"생존 시간 {minutes:00}:{seconds:00}   ·   도달 레벨 {level}   ·   보유 드론 {droneCount}종";
        }

        // "재도전" 버튼을 눌렀을 때 실행. 지금 씬을 그대로 다시 불러와서 같은 스테이지를 처음부터 다시 도전한다.
        private void RestartGame()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            // 결과 화면을 띄우면서 Time.timeScale을 0으로 멈춰뒀던 걸 반드시 1로 되돌려야 한다.
            // 그대로 두면 새로 불러온 씬도 멈춘 채로 시작돼버린다.
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // "로비로" 버튼을 눌렀을 때 실행. 로비로 돌아간다.
        private void GoToLobby()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            Time.timeScale = 1f;
            SceneManager.LoadScene(lobbySceneName);
        }
    }
}
