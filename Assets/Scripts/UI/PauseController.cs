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
            pausePanel = transform.Find("PausePanel").gameObject;
            pausePanel.SetActive(false);

            pausePanel.transform.Find("BtnContinue").GetComponent<Button>().onClick.AddListener(Resume);
            pausePanel.transform.Find("BtnRestart").GetComponent<Button>().onClick.AddListener(Restart);
            pausePanel.transform.Find("BtnMainMenu").GetComponent<Button>().onClick.AddListener(GoToMainMenu);
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
