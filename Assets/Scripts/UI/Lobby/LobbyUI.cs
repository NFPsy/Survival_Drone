using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 로비(허브) 화면. 재화를 보여주고, 스테이지를 고르고, 출격하는 화면이다.
    //   메인 메뉴 → 로비 → (스테이지 선택) → InGame
    //
    // MainMenuController와 같은 방식으로, 정해진 이름의 자식 오브젝트를 찾아서 연결한다.
    // 못 찾아도 경고 로그만 남기고 나머지는 계속 동작한다.
    //
    // 아직 없는 것: 3D 프리뷰, 상점 버튼 (해당 화면을 만들 때 추가).
    public class LobbyUI : MonoBehaviour
    {
        // 출격 버튼을 누르면 불러올 플레이 씬, 뒤로 버튼을 누르면 돌아갈 씬.
        [SerializeField] private string gameplaySceneName = "InGame";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string gachaSceneName = "Gacha";
        [SerializeField] private string hangarSceneName = "Hangar";

        [SerializeField] private AudioClip clickSound;

        private Text _creditText;
        private Text _coreText;

        private StageCardView _cardView;
        private GameObject _stageCardObject;
        private Button _prevButton;
        private Button _nextButton;
        private Button _launchButton;
        private Button _stageListButton;
        private Button _gachaButton;
        private Button _hangarButton;
        private Button _logButton;
        private TestLogPopup _testLogPopup;
        private Button _questButton;
        private DailyQuestPopup _questPopup;
        private Button _milestoneButton;
        private MilestonePopup _milestonePopup;
        private StageSelectUI _stageSelect;

        // 로비 카드에 지금 보여주는 스테이지 위치. 화살표로 넘겨 보다가 잠긴 스테이지를 볼 수도 있어서
        // "고른 스테이지(StageProgress.SelectedIndex)"와 따로 관리한다. 고른 것은 항상 해금된 스테이지뿐이다.
        private int _viewIndex;

        private void Awake()
        {
            _creditText = FindText("TopBar/CreditText");
            _coreText = FindText("TopBar/CoreText");

            var card = transform.Find("StageCard");
            if (card == null) Debug.LogWarning("[Stage] 로비에서 'StageCard'를 찾지 못했습니다.");
            else
            {
                _stageCardObject = card.gameObject;
                _cardView = new StageCardView(card);
                _prevButton = WireButton(card, "BtnPrev", () => MoveView(-1));
                _nextButton = WireButton(card, "BtnNext", () => MoveView(+1));
            }

            _launchButton = WireButton(transform, "BtnLaunch", Launch);
            WireButton(transform, "BtnBack", () => { PlayClick(); SceneManager.LoadScene(mainMenuSceneName); });
            _stageListButton = WireButton(transform, "BtnStageList", () => { PlayClick(); ShowStageSelect(true); });
            _gachaButton = WireButton(transform, "BtnGacha", () => { PlayClick(); SceneManager.LoadScene(gachaSceneName); });
            _hangarButton = WireButton(transform, "BtnHangar", () => { PlayClick(); SceneManager.LoadScene(hangarSceneName); });
            _logButton = WireButton(transform, "BtnLog", () => { PlayClick(); if (_testLogPopup != null) _testLogPopup.Open(); });

            var logTransform = transform.Find("TestLogPopup");
            if (logTransform != null)
            {
                _testLogPopup = logTransform.GetComponent<TestLogPopup>();
                logTransform.gameObject.SetActive(false);
            }
            else Debug.LogWarning("[Save] 로비에서 'TestLogPopup'을 찾지 못했습니다.");

            _questButton = WireButton(transform, "BtnQuest", () => { PlayClick(); if (_questPopup != null) _questPopup.Open(); });
            var questTransform = transform.Find("DailyQuestPopup");
            if (questTransform != null)
            {
                _questPopup = questTransform.GetComponent<DailyQuestPopup>();
                questTransform.gameObject.SetActive(false);
            }
            else Debug.LogWarning("[Quest] 로비에서 'DailyQuestPopup'을 찾지 못했습니다.");

            _milestoneButton = WireButton(transform, "BtnMilestone", () => { PlayClick(); if (_milestonePopup != null) _milestonePopup.Open(); });
            var milestoneTransform = transform.Find("MilestonePopup");
            if (milestoneTransform != null)
            {
                _milestonePopup = milestoneTransform.GetComponent<MilestonePopup>();
                milestoneTransform.gameObject.SetActive(false);
            }
            else Debug.LogWarning("[Milestone] 로비에서 'MilestonePopup'을 찾지 못했습니다.");

            var selectTransform = transform.Find("StageSelectPanel");
            if (selectTransform != null)
            {
                _stageSelect = selectTransform.GetComponent<StageSelectUI>();
                selectTransform.gameObject.SetActive(false);
            }
            if (_stageSelect != null)
            {
                _stageSelect.OnCloseRequested += () => ShowStageSelect(false);
                _stageSelect.OnLaunchRequested += Launch;
            }
            else Debug.LogWarning("[Stage] 로비에서 'StageSelectPanel'(StageSelectUI)을 찾지 못했습니다.");
        }

        private void OnEnable()
        {
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.OnCoreChanged += HandleCoreChanged;
                CurrencyManager.Instance.OnCreditChanged += HandleCreditChanged;
            }
            else Debug.LogWarning("[Currency] 로비에서 CurrencyManager를 찾지 못했습니다. 메인 메뉴 씬부터 시작했는지 확인해주세요.");

            if (StageProgress.Instance != null) StageProgress.Instance.OnSelectionChanged += HandleSelectionChanged;
            else Debug.LogWarning("[Stage] 로비에서 StageProgress를 찾지 못했습니다. 메인 메뉴 씬부터 시작했는지 확인해주세요.");

            if (DroneInventory.Instance != null) DroneInventory.Instance.OnInventoryChanged += RefreshStageCard;
            else Debug.LogWarning("[Inventory] 로비에서 DroneInventory를 찾지 못했습니다. 내 전투력이 0으로 표시됩니다.");

            _viewIndex = StageProgress.Instance != null ? StageProgress.Instance.SelectedIndex : 0;
            RefreshCurrency();
            RefreshStageCard();
        }

        private void OnDisable()
        {
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.OnCoreChanged -= HandleCoreChanged;
                CurrencyManager.Instance.OnCreditChanged -= HandleCreditChanged;
            }
            if (StageProgress.Instance != null) StageProgress.Instance.OnSelectionChanged -= HandleSelectionChanged;
            if (DroneInventory.Instance != null) DroneInventory.Instance.OnInventoryChanged -= RefreshStageCard;
        }

        // ---- 재화 ----
        private void HandleCoreChanged(int value) => RefreshCurrency();
        private void HandleCreditChanged(int value) => RefreshCurrency();

        // 천 단위 콤마를 넣어서 표시한다 (12450 → 12,450).
        private void RefreshCurrency()
        {
            int credit = CurrencyManager.Instance != null ? CurrencyManager.Instance.Credit : 0;
            int core = CurrencyManager.Instance != null ? CurrencyManager.Instance.Core : 0;
            if (_creditText != null) _creditText.text = $"크레딧  {credit:N0}";
            if (_coreText != null) _coreText.text = $"코어  {core:N0}";
        }

        // ---- 스테이지 카드 ----
        private void HandleSelectionChanged(int selectedIndex)
        {
            _viewIndex = selectedIndex;
            RefreshStageCard();
        }

        // 카드를 한 칸씩 넘긴다. 넘긴 스테이지가 해금돼 있으면 그 스테이지를 "고른 스테이지"로도 바꾼다.
        private void MoveView(int direction)
        {
            var progress = StageProgress.Instance;
            if (progress == null || progress.StageCount == 0) return;

            PlayClick();
            _viewIndex = Mathf.Clamp(_viewIndex + direction, 0, progress.StageCount - 1);
            progress.TrySelect(_viewIndex); // 해금된 스테이지면 선택이 바뀌고 HandleSelectionChanged가 다시 그려준다
            RefreshStageCard();
        }

        private void RefreshStageCard()
        {
            var progress = StageProgress.Instance;
            if (progress == null || _cardView == null) return;

            bool unlocked = progress.IsUnlocked(_viewIndex);
            int myPower = DroneInventory.Instance != null ? DroneInventory.Instance.TotalCombatPower : 0;
            _cardView.Show(progress.GetStage(_viewIndex), progress.GetBestSurvivalSeconds(_viewIndex), unlocked, myPower);

            if (_prevButton != null) _prevButton.interactable = _viewIndex > 0;
            if (_nextButton != null) _nextButton.interactable = _viewIndex < progress.StageCount - 1;

            // 잠긴 스테이지를 구경 중일 때는 출격할 수 없다.
            if (_launchButton != null) _launchButton.interactable = unlocked;
        }

        // 스테이지 선택 패널을 열고 닫는다. 패널이 열려 있는 동안은 뒤에 있는 로비 카드와 버튼을 숨겨서
        // 출격 버튼이 두 개 보이는 일이 없게 한다.
        private void ShowStageSelect(bool show)
        {
            if (_stageSelect != null) _stageSelect.gameObject.SetActive(show);
            if (_stageCardObject != null) _stageCardObject.SetActive(!show);
            if (_launchButton != null) _launchButton.gameObject.SetActive(!show);
            if (_stageListButton != null) _stageListButton.gameObject.SetActive(!show);
            if (_gachaButton != null) _gachaButton.gameObject.SetActive(!show);
            if (_hangarButton != null) _hangarButton.gameObject.SetActive(!show);
            if (_logButton != null) _logButton.gameObject.SetActive(!show);
            if (_questButton != null) _questButton.gameObject.SetActive(!show);
            if (_milestoneButton != null) _milestoneButton.gameObject.SetActive(!show);

            // 패널을 닫고 돌아왔을 때 카드가 방금 고른 스테이지를 보여주도록 다시 그린다.
            if (!show)
            {
                if (StageProgress.Instance != null) _viewIndex = StageProgress.Instance.SelectedIndex;
                RefreshStageCard();
            }
        }

        // ---- 출격 ----
        private void Launch()
        {
            var progress = StageProgress.Instance;
            if (progress != null)
            {
                var stage = progress.CurrentStage;
                Debug.Log($"[Stage] 출격: 스테이지 {progress.SelectedIndex + 1}" +
                          (stage != null ? $" '{stage.DisplayName}' (적 배율 x{stage.EnemyMultiplier}, 시각 {Time.realtimeSinceStartup:F1}초)" : ""));
            }

            PlayClick();
            Time.timeScale = 1f; // 혹시 멈춘 채로 넘어오더라도 InGame이 멈춰서 시작하지 않게 한다
            SceneManager.LoadScene(gameplaySceneName);
        }

        // ---- 도우미 ----
        private void PlayClick() => AudioManager.Instance?.PlaySfx(clickSound);

        private Text FindText(string path)
        {
            var child = transform.Find(path);
            var text = child != null ? child.GetComponent<Text>() : null;
            if (text == null) Debug.LogWarning($"[Currency] 로비에서 '{path}' 글자 칸을 찾지 못했습니다.");
            return text;
        }

        private Button WireButton(Transform parent, string buttonName, UnityEngine.Events.UnityAction action)
        {
            var child = parent.Find(buttonName);
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogWarning($"[Stage] 로비에서 '{parent.name}/{buttonName}' 버튼을 찾지 못했습니다.");
                return null;
            }
            button.onClick.AddListener(action);
            return button;
        }
    }
}
