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

        // 버튼을 누를 때마다 재생할 공용 클릭음.
        [SerializeField] private AudioClip clickSound;

        // 메인 메뉴에서 재생할 배경음악.
        [SerializeField] private AudioClip menuMusic;

        private void Awake()
        {
            // 자식 오브젝트를 이름으로 찾아서 각 패널 변수에 저장해둔다.
            // (인스펙터에서 하나하나 드래그해서 연결할 필요 없이, 정해진 이름의
            // 자식 구조만 맞으면 자동으로 연결되게 하기 위함)
            // FindChild는 못 찾아도 null만 반환하고 경고 로그를 남길 뿐, 예외를 던지지 않는다 —
            // 나중에 자식 오브젝트 이름을 실수로 바꾸거나 지워도 메인 메뉴 전체가 멈추지 않게 하기 위함.
            titlePanel = FindChild("TitlePanel");
            controlsPanel = FindChild("ControlsPanel");
            aboutPanel = FindChild("AboutPanel");
            settingsPanel = FindChild("SettingsPanel");
            difficultyPanel = FindChild("DifficultyPanel");

            // 타이틀 화면의 버튼 4개에 각각 클릭 시 실행할 함수를 연결한다.
            // "게임 시작"은 바로 씬을 불러오지 않고, 먼저 난이도 선택 팝업을 띄운다.
            WireButton(titlePanel, "BtnStart", () => ShowPanel(difficultyPanel));
            WireButton(titlePanel, "BtnControls", () => ShowPanel(controlsPanel));
            WireButton(titlePanel, "BtnAbout", () => ShowPanel(aboutPanel));
            WireButton(titlePanel, "BtnSettings", () => ShowPanel(settingsPanel));

            // 팝업 4개 모두 "닫기/취소" 버튼을 누르면 똑같이 타이틀 화면으로 돌아간다.
            WireButton(controlsPanel, "BtnClose", () => { AudioManager.Instance?.PlaySfx(clickSound); ShowTitle(); });
            WireButton(aboutPanel, "BtnClose", () => { AudioManager.Instance?.PlaySfx(clickSound); ShowTitle(); });
            WireButton(settingsPanel, "BtnClose", () => { AudioManager.Instance?.PlaySfx(clickSound); ShowTitle(); });
            WireButton(difficultyPanel, "BtnClose", () => { AudioManager.Instance?.PlaySfx(clickSound); ShowTitle(); });

            // 난이도 선택 팝업의 버튼 3개: 누르면 그 난이도로 정하고 바로 게임을 시작한다.
            WireButton(difficultyPanel, "BtnEasy", () => StartGame(DifficultyLevel.Easy));
            WireButton(difficultyPanel, "BtnNormal", () => StartGame(DifficultyLevel.Normal));
            WireButton(difficultyPanel, "BtnHard", () => StartGame(DifficultyLevel.Hard));

            // 게임을 처음 켰을 때는 항상 타이틀 화면부터 보이도록 초기화.
            ShowTitle();
        }

        // 이 오브젝트의 자식을 이름으로 찾는 함수. 못 찾으면 예외를 던지는 대신
        // 경고 로그만 남기고 null을 돌려줘서, 하나가 잘못돼도 나머지 화면은 계속 동작하게 한다.
        private GameObject FindChild(string name)
        {
            var child = transform.Find(name);
            if (child == null)
            {
                Debug.LogWarning($"[MainMenuController] '{name}' 자식 오브젝트를 찾지 못했습니다. 메인 메뉴 구조가 바뀌지 않았는지 확인해주세요.");
                return null;
            }
            return child.gameObject;
        }

        // parent 아래에서 buttonName인 버튼을 찾아 클릭 이벤트를 연결하는 함수.
        // parent나 버튼을 못 찾으면 조용히 건너뛴다(경고 로그만 남김).
        private void WireButton(GameObject parent, string buttonName, UnityEngine.Events.UnityAction action)
        {
            if (parent == null) return;

            var buttonTransform = parent.transform.Find(buttonName);
            var button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogWarning($"[MainMenuController] '{parent.name}/{buttonName}' 버튼을 찾지 못했습니다.");
                return;
            }
            button.onClick.AddListener(action);
        }

        private void Start()
        {
            // AudioManager.Instance는 AudioManager 자신의 Awake()에서 등록되는데,
            // 어느 오브젝트의 Awake()가 먼저 실행될지는 보장되지 않는다(실행 순서 미지정 시).
            // 반면 Start()는 씬의 모든 Awake()가 다 끝난 뒤에만 호출되므로,
            // 여기서 불러야 AudioManager.Instance가 확실히 준비되어 있다.
            AudioManager.Instance?.PlayMusic(menuMusic);
        }

        // 팝업 패널 하나만 켜고 나머지(타이틀 포함)는 모두 끈다.
        private void ShowPanel(GameObject panelToShow)
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            titlePanel?.SetActive(false);
            controlsPanel?.SetActive(panelToShow == controlsPanel);
            aboutPanel?.SetActive(panelToShow == aboutPanel);
            settingsPanel?.SetActive(panelToShow == settingsPanel);
            difficultyPanel?.SetActive(panelToShow == difficultyPanel);
        }

        // 팝업을 닫고 처음 타이틀 화면(버튼 4개)으로 돌아간다.
        private void ShowTitle()
        {
            titlePanel?.SetActive(true);
            controlsPanel?.SetActive(false);
            aboutPanel?.SetActive(false);
            settingsPanel?.SetActive(false);
            difficultyPanel?.SetActive(false);
        }

        // 난이도 선택 팝업에서 버튼을 눌렀을 때 실행. 선택한 난이도를 저장하고
        // 현재 메뉴 씬을 내린 뒤 실제 플레이 씬을 불러온다.
        private void StartGame(DifficultyLevel level)
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            GameDifficulty.Current = level;
            SceneManager.LoadScene(gameplaySceneName);
        }
    }
}
