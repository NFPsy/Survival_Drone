using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace SurvivalDrone.UI
{
    // 메인 메뉴 화면(게임 시작/조작법/게임 설명/설정)을 처리하는 스크립트.
    // 하위 오브젝트를 이름으로 직접 찾아서 버튼 이벤트를 연결하기 때문에,
    // 인스펙터에서 필드를 따로 연결할 필요 없이 정해진 이름의 자식 구조만 맞추면 동작한다.
    public class MainMenuController : MonoBehaviour
    {
        // 게임 시작 버튼을 눌렀을 때 로드할 실제 플레이 씬 이름.
        [SerializeField] private string gameplaySceneName = "SampleScene";

        private GameObject titlePanel;
        private GameObject controlsPanel;
        private GameObject aboutPanel;
        private GameObject settingsPanel;

        private void Awake()
        {
            titlePanel = transform.Find("TitlePanel").gameObject;
            controlsPanel = transform.Find("ControlsPanel").gameObject;
            aboutPanel = transform.Find("AboutPanel").gameObject;
            settingsPanel = transform.Find("SettingsPanel").gameObject;

            titlePanel.transform.Find("BtnStart").GetComponent<Button>().onClick.AddListener(StartGame);
            titlePanel.transform.Find("BtnControls").GetComponent<Button>().onClick.AddListener(delegate { ShowPanel(controlsPanel); });
            titlePanel.transform.Find("BtnAbout").GetComponent<Button>().onClick.AddListener(delegate { ShowPanel(aboutPanel); });
            titlePanel.transform.Find("BtnSettings").GetComponent<Button>().onClick.AddListener(delegate { ShowPanel(settingsPanel); });

            controlsPanel.transform.Find("BtnClose").GetComponent<Button>().onClick.AddListener(ShowTitle);
            aboutPanel.transform.Find("BtnClose").GetComponent<Button>().onClick.AddListener(ShowTitle);
            settingsPanel.transform.Find("BtnClose").GetComponent<Button>().onClick.AddListener(ShowTitle);

            ShowTitle();
        }

        // 팝업 패널 하나만 켜고 나머지(타이틀 포함)는 모두 끈다.
        private void ShowPanel(GameObject panelToShow)
        {
            titlePanel.SetActive(false);
            controlsPanel.SetActive(panelToShow == controlsPanel);
            aboutPanel.SetActive(panelToShow == aboutPanel);
            settingsPanel.SetActive(panelToShow == settingsPanel);
        }

        // 팝업을 닫고 처음 타이틀 화면(버튼 4개)으로 돌아간다.
        private void ShowTitle()
        {
            titlePanel.SetActive(true);
            controlsPanel.SetActive(false);
            aboutPanel.SetActive(false);
            settingsPanel.SetActive(false);
        }

        private void StartGame()
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
    }
}
