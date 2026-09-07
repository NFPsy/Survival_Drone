using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SurvivalDrone.Core;

namespace SurvivalDrone.UI
{
    // 메인 메뉴 화면(게임 시작/조작법/게임 설명/설정)을 처리하는 스크립트.
    // 하위 오브젝트를 이름으로 직접 찾아서 버튼 이벤트를 연결하기 때문에,
    // 인스펙터에서 필드를 따로 연결할 필요 없이 정해진 이름의 자식 구조만 맞추면 동작한다.
    public class MainMenuController : MonoBehaviour
    {
        // 게임 시작 버튼을 눌렀을 때 로드할 실제 플레이 씬 이름.
        // 인스펙터에서 씬 이름을 바꿀 일이 생겨도 코드를 안 고치도록 필드로 빼두었다.
        [SerializeField] private string gameplaySceneName = "InGame";

        // 처음 보이는 타이틀 화면(버튼 4개가 있는 패널).
        private GameObject titlePanel;

        // "조작법" 버튼을 누르면 뜨는 팝업.
        private GameObject controlsPanel;

        // "게임 설명" 버튼을 누르면 뜨는 팝업.
        private GameObject aboutPanel;

        // "설정" 버튼을 누르면 뜨는 팝업(볼륨/전체화면).
        private GameObject settingsPanel;

        // "게임 시작" 버튼을 누르면 뜨는 난이도 선택 팝업(쉬움/보통/어려움).
        private GameObject difficultyPanel;

        private void Awake()
        {
            // 자식 오브젝트를 이름으로 찾아서 각 패널 변수에 저장해둔다.
            // (인스펙터에서 하나하나 드래그해서 연결할 필요 없이, 정해진 이름의
            // 자식 구조만 맞으면 자동으로 연결되게 하기 위함)
            titlePanel = transform.Find("TitlePanel").gameObject;
            controlsPanel = transform.Find("ControlsPanel").gameObject;
            aboutPanel = transform.Find("AboutPanel").gameObject;
            settingsPanel = transform.Find("SettingsPanel").gameObject;
            difficultyPanel = transform.Find("DifficultyPanel").gameObject;

            // 타이틀 화면의 버튼 4개에 각각 클릭 시 실행할 함수를 연결한다.
            // "게임 시작"은 바로 씬을 불러오지 않고, 먼저 난이도 선택 팝업을 띄운다.
            titlePanel.transform.Find("BtnStart").GetComponent<Button>().onClick.AddListener(delegate { ShowPanel(difficultyPanel); });
            titlePanel.transform.Find("BtnControls").GetComponent<Button>().onClick.AddListener(delegate { ShowPanel(controlsPanel); });
            titlePanel.transform.Find("BtnAbout").GetComponent<Button>().onClick.AddListener(delegate { ShowPanel(aboutPanel); });
            titlePanel.transform.Find("BtnSettings").GetComponent<Button>().onClick.AddListener(delegate { ShowPanel(settingsPanel); });

            // 팝업 4개 모두 "닫기/취소" 버튼을 누르면 똑같이 타이틀 화면으로 돌아간다.
            controlsPanel.transform.Find("BtnClose").GetComponent<Button>().onClick.AddListener(ShowTitle);
            aboutPanel.transform.Find("BtnClose").GetComponent<Button>().onClick.AddListener(ShowTitle);
            settingsPanel.transform.Find("BtnClose").GetComponent<Button>().onClick.AddListener(ShowTitle);
            difficultyPanel.transform.Find("BtnClose").GetComponent<Button>().onClick.AddListener(ShowTitle);

            // 난이도 선택 팝업의 버튼 3개: 누르면 그 난이도로 정하고 바로 게임을 시작한다.
            difficultyPanel.transform.Find("BtnEasy").GetComponent<Button>().onClick.AddListener(delegate { StartGame(DifficultyLevel.Easy); });
            difficultyPanel.transform.Find("BtnNormal").GetComponent<Button>().onClick.AddListener(delegate { StartGame(DifficultyLevel.Normal); });
            difficultyPanel.transform.Find("BtnHard").GetComponent<Button>().onClick.AddListener(delegate { StartGame(DifficultyLevel.Hard); });

            // 게임을 처음 켰을 때는 항상 타이틀 화면부터 보이도록 초기화.
            ShowTitle();
        }

        // 팝업 패널 하나만 켜고 나머지(타이틀 포함)는 모두 끈다.
        private void ShowPanel(GameObject panelToShow)
        {
            titlePanel.SetActive(false);
            controlsPanel.SetActive(panelToShow == controlsPanel);
            aboutPanel.SetActive(panelToShow == aboutPanel);
            settingsPanel.SetActive(panelToShow == settingsPanel);
            difficultyPanel.SetActive(panelToShow == difficultyPanel);
        }

        // 팝업을 닫고 처음 타이틀 화면(버튼 4개)으로 돌아간다.
        private void ShowTitle()
        {
            titlePanel.SetActive(true);
            controlsPanel.SetActive(false);
            aboutPanel.SetActive(false);
            settingsPanel.SetActive(false);
            difficultyPanel.SetActive(false);
        }

        // 난이도 선택 팝업에서 버튼을 눌렀을 때 실행. 선택한 난이도를 저장하고
        // 현재 메뉴 씬을 내린 뒤 실제 플레이 씬을 불러온다.
        private void StartGame(DifficultyLevel level)
        {
            GameDifficulty.Current = level;
            SceneManager.LoadScene(gameplaySceneName);
        }
    }
}
