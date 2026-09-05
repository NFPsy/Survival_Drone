using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SurvivalDrone.Core;

namespace SurvivalDrone.UI
{
    // 게임이 끝났을 때(승리 또는 패배) 결과 화면을 보여주고,
    // "다시 시작"/"메인 메뉴" 버튼으로 다음 행동을 고를 수 있게 해주는 스크립트.
    public class ResultPanel : MonoBehaviour
    {
        // 결과 화면 전체 패널 (평소엔 꺼져 있다가 게임이 끝나면 켜짐).
        [SerializeField] private GameObject panel;

        // "승리" 또는 "패배" 문구를 보여줄 텍스트.
        [SerializeField] private Text resultText;

        // 게임 상태(진행중/승리/패배)가 바뀌는 것을 감지하기 위한 GameManager 연결.
        [SerializeField] private GameManager gameManager;

        // "다시 시작" 버튼: 지금 플레이 중인 씬을 처음부터 다시 불러온다.
        [SerializeField] private Button restartButton;

        // "메인 메뉴" 버튼: 타이틀 화면(MainMenu 씬)으로 돌아간다.
        [SerializeField] private Button mainMenuButton;

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
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        // 게임 상태가 바뀔 때 호출되는 함수.
        private void HandleStateChanged(MatchState state)
        {
            // 아직 "진행 중" 상태면(=게임이 끝난 게 아니면) 아무것도 하지 않는다.
            if (state == MatchState.Playing) return;

            // 게임이 끝났으면(승리 또는 패배) 결과 패널을 켜고 알맞은 문구를 표시한다.
            if (panel != null) panel.SetActive(true);
            if (resultText != null)
            {
                resultText.text = state == MatchState.Won ? "MISSION COMPLETE" : "GAME OVER";
            }
        }

        // "다시 시작" 버튼을 눌렀을 때 실행. 지금 씬을 그대로 다시 불러와서 처음부터 재도전한다.
        private void RestartGame()
        {
            // 결과 화면을 띄우면서 Time.timeScale을 0으로 멈춰뒀던 걸 반드시 1로 되돌려야 한다.
            // 그대로 두면 새로 불러온 씬도 멈춘 채로 시작돼버린다.
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // "메인 메뉴" 버튼을 눌렀을 때 실행. 타이틀 화면으로 돌아간다.
        private void GoToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }
    }
}
