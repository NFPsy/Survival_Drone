using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 뽑기 화면. 배너, 등급 확률 요약, 천장 게이지, 1회/10연 버튼, 코어 잔액을 보여준다.
    // 실제 뽑기 처리는 GachaController가 하고, 이 스크립트는 버튼 입력을 넘기고 결과를 화면에 반영하기만 한다.
    //
    // MainMenuController처럼 정해진 이름의 자식 오브젝트를 찾아서 연결한다. 못 찾아도 경고만 남기고 나머지는 계속 동작한다.
    //
    // 화면에 적히는 확률·가격·천장 횟수는 전부 GachaTable에서 읽는다 ("표기 = 실제" 원칙).
    // 뽑기가 성공하면 결과 연출 화면(GachaResultUI)을 띄운다.
    public class GachaUI : MonoBehaviour
    {
        // 뒤로 버튼을 누르면 돌아갈 씬.
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private AudioClip clickSound;

        // 재화 부족일 때 가격 글자를 빨갛게 바꾸는 색. (충분할 때의 색은 화면을 만든 원래 색을 그대로 쓴다)
        private static readonly Color ShortColor = new Color(1f, 0.35f, 0.35f);

        private Color _singlePriceNormalColor;
        private Color _tenPriceNormalColor;

        private Text _coreText;
        private Text _ratesText;
        private Text _pityText;
        private RectTransform _pityBarFill;
        private Text _softPityText;
        private RectTransform _softPityBarFill;
        private GachaResultUI _resultUI;

        private Button _singleButton;
        private Text _singlePriceText;
        private Button _tenButton;
        private Text _tenPriceText;
        private Text _discountText;

        private ProbabilityPopup _probabilityPopup;
        private GameObject _insufficientPopup;
        private Text _insufficientMessage;

        // ---- 뽑기 시뮬레이터 ----
        // 켜면 코어·보유 드론·실제 천장과 무관하게 무한으로 뽑아볼 수 있다. (SimGachaSession 참고)
        // 화면에 들어올 때마다 꺼진 상태로 시작하고, 처음 켤 때 새로 만들어서 천장도 0에서 시작한다.
        private SimGachaSession _sim;
        private bool _simMode;
        private Text _simToggleLabel;
        private Text _simInfoText;
        private Button _simResetButton;

        // ---- 락온 뽑기 ----
        // 기본 뽑기와 별개의 뽑기 화면(LockOnPanel)을 여는 버튼. LockOnTable의 "사용 여부"가 꺼져 있으면 버튼이 아예 보이지 않는다.
        private LockOnUI _lockOn;
        private Button _lockOnButton;

        // ---- 뽑기 선택 화면 ----
        // 락온 뽑기가 켜져 있으면, 이 씬에 들어오자마자 "일반 뽑기 / 락온 뽑기" 선택 화면이 먼저 열린다.
        // 일반 뽑기를 고르면 선택 화면이 닫히고 아래 일반 뽑기 화면이 보이며, 락온을 고르면 락온 화면이 선택 화면 위로 열린다.
        // (락온 화면을 닫으면 아래에 남아 있던 선택 화면이 다시 보인다.) 락온 뽑기가 꺼져 있으면 선택 화면 없이 예전처럼 바로 일반 뽑기가 열린다.
        private GachaSelectUI _select;
        private bool _selectEnabled;

        private void Awake()
        {
            _coreText = FindText("TopBar/CoreText");
            _ratesText = FindText("RatesPanel/RatesText");
            _pityText = FindText("PityText");

            var fill = transform.Find("PityBar/Fill");
            _pityBarFill = fill != null ? fill.GetComponent<RectTransform>() : null;
            if (_pityBarFill == null) Debug.LogWarning("[Gacha] 뽑기 화면에서 'PityBar/Fill'을 찾지 못했습니다.");

            // 소천장(SR 이상 보장) 게이지: 큰 천장 게이지 아래에 한 줄 더 있다.
            _softPityText = FindText("SoftPityText");
            var softFill = transform.Find("SoftPityBar/Fill");
            _softPityBarFill = softFill != null ? softFill.GetComponent<RectTransform>() : null;
            if (_softPityBarFill == null) Debug.LogWarning("[Gacha] 뽑기 화면에서 'SoftPityBar/Fill'을 찾지 못했습니다.");

            WireButton("BtnBack", OnBackClicked);
            WireButton("RatesPanel/BtnRates", () => { PlayClick(); if (_probabilityPopup != null) _probabilityPopup.Open(); });
            _singleButton = WireButton("BtnPullSingle", () => Pull(false));
            _tenButton = WireButton("BtnPullTen", () => Pull(true));
            _singlePriceText = FindText("BtnPullSingle/PriceText");
            _tenPriceText = FindText("BtnPullTen/PriceText");
            _discountText = FindText("BtnPullTen/BadgeBack/DiscountBadge");
            if (_singlePriceText != null) _singlePriceNormalColor = _singlePriceText.color;
            if (_tenPriceText != null) _tenPriceNormalColor = _tenPriceText.color;

            // 시뮬레이터 전환 버튼 / 안내 글자 / 초기화 버튼. (시뮬레이터 UI가 없는 옛 씬이면 경고만 남기고 일반 뽑기는 그대로 동작한다)
            WireButton("BtnSim", ToggleSim);
            _simToggleLabel = FindText("BtnSim/Text");
            _simInfoText = FindText("SimInfoText");
            _simResetButton = WireButton("BtnSimReset", ResetSim);

            // 락온 뽑기 버튼 / 화면. (락온 UI가 없는 옛 씬이면 조용히 건너뛴다. 사용 여부가 꺼져 있으면 버튼을 숨긴다)
            _lockOnButton = WireButton("BtnLockOn", () => { PlayClick(); if (_lockOn != null) _lockOn.Open(); }, false);
            var lockOnTransform = transform.Find("LockOnPanel");
            if (lockOnTransform != null)
            {
                _lockOn = lockOnTransform.GetComponent<LockOnUI>();
                lockOnTransform.gameObject.SetActive(false);
            }

            // 선택 화면: 락온 뽑기가 켜져 있을 때만 쓴다. (선택 화면이 락온으로 가는 길이 되므로 옛 "락온 뽑기" 버튼은 숨긴다)
            var selectTransform = transform.Find("GachaSelectPanel");
            if (selectTransform != null)
            {
                _select = selectTransform.GetComponent<GachaSelectUI>();
                _selectEnabled = _select != null && _lockOn != null && _lockOn.IsEnabled;
                if (_select != null)
                {
                    _select.NormalChosen += () => _select.Close();
                    _select.LockOnChosen += () => { if (_lockOn != null) _lockOn.Open(); };
                    _select.BackPressed += () => SceneManager.LoadScene(lobbySceneName);
                }
                selectTransform.gameObject.SetActive(_selectEnabled);
            }
            if (_lockOnButton != null) _lockOnButton.gameObject.SetActive(_lockOn != null && _lockOn.IsEnabled && !_selectEnabled);

            var popupTransform = transform.Find("ProbabilityPopup");
            if (popupTransform != null)
            {
                _probabilityPopup = popupTransform.GetComponent<ProbabilityPopup>();
                popupTransform.gameObject.SetActive(false);
            }
            else Debug.LogWarning("[Gacha] 뽑기 화면에서 'ProbabilityPopup'을 찾지 못했습니다.");

            var resultTransform = transform.Find("ResultPanel");
            if (resultTransform != null)
            {
                _resultUI = resultTransform.GetComponent<GachaResultUI>();
                resultTransform.gameObject.SetActive(false);
            }
            else Debug.LogWarning("[Gacha] 뽑기 화면에서 'ResultPanel'을 찾지 못했습니다.");

            var insufficient = transform.Find("InsufficientPopup");
            if (insufficient != null)
            {
                _insufficientPopup = insufficient.gameObject;
                _insufficientMessage = FindText("InsufficientPopup/Box/MessageText");
                var ok = insufficient.Find("Box/BtnOk");
                var okButton = ok != null ? ok.GetComponent<Button>() : null;
                if (okButton != null) okButton.onClick.AddListener(() => { PlayClick(); _insufficientPopup.SetActive(false); });
                _insufficientPopup.SetActive(false);
            }
            else Debug.LogWarning("[Gacha] 뽑기 화면에서 'InsufficientPopup'을 찾지 못했습니다.");
        }

        private void OnEnable()
        {
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.OnCoreChanged += HandleCoreChanged;
            else Debug.LogWarning("[Currency] 뽑기 화면에서 CurrencyManager를 찾지 못했습니다. 메인 메뉴 씬부터 시작했는지 확인해주세요.");

            if (GachaController.Instance == null)
                Debug.LogWarning("[Gacha] 뽑기 화면에서 GachaController를 찾지 못했습니다. 메인 메뉴 씬부터 시작했는지 확인해주세요.");

            Refresh();
        }

        private void OnDisable()
        {
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.OnCoreChanged -= HandleCoreChanged;
        }

        private void HandleCoreChanged(int value) => Refresh();

        // 화면의 모든 숫자를 현재 상태에 맞게 다시 그린다.
        private void Refresh()
        {
            var controller = GachaController.Instance;
            var currency = CurrencyManager.Instance;
            var table = controller != null ? controller.Table : null;
            int core = currency != null ? currency.Core : 0;

            if (_coreText != null) _coreText.text = $"코어  {core:N0}";
            if (table == null) return;

            if (_ratesText != null) _ratesText.text = GachaRateFormatter.BuildRatesRichText(table);

            // 시뮬레이터 모드면 게이지·가격을 시뮬레이터 기준으로 보여준다.
            bool sim = _simMode && _sim != null;

            // 천장 게이지: "SSR 확정까지  누적 42 / 70"과 채워지는 막대
            int pity = sim ? _sim.PityCount : controller.PityCount;
            int limit = sim ? _sim.PityLimit : controller.PityLimit;
            if (_pityText != null) _pityText.text = $"SSR 확정까지    누적 {pity} / {limit}";
            if (_pityBarFill != null)
            {
                float ratio = limit > 0 ? Mathf.Clamp01((float)pity / limit) : 0f;
                _pityBarFill.anchorMax = new Vector2(ratio, 1f);
            }

            // 소천장 게이지: "SR 이상 보장까지  누적 7 / 10" (횟수가 0이면 소천장을 쓰지 않으므로 숨긴다)
            int softLimit = sim ? _sim.SoftPityLimit : controller.SoftPityLimit;
            int softCount = sim ? _sim.SoftPityCount : controller.SoftPityCount;
            bool softOn = softLimit > 0;
            if (_softPityText != null)
            {
                _softPityText.gameObject.SetActive(softOn);
                if (softOn) _softPityText.text = $"SR 이상 보장까지    누적 {softCount} / {softLimit}";
            }
            if (_softPityBarFill != null)
            {
                _softPityBarFill.transform.parent.gameObject.SetActive(softOn);
                float softRatio = softOn ? Mathf.Clamp01((float)softCount / softLimit) : 0f;
                _softPityBarFill.anchorMax = new Vector2(softRatio, 1f);
            }

            // 가격 표시. 코어가 모자라면 가격 글자만 빨갛게 (버튼은 눌러서 "부족" 안내를 볼 수 있게 켜 둔다).
            // 시뮬레이터에서는 코어를 쓰지 않으므로 "무료"로 보여준다.
            if (sim)
            {
                if (_singlePriceText != null) { _singlePriceText.text = "무료 (시뮬레이션)"; _singlePriceText.color = _singlePriceNormalColor; }
                if (_tenPriceText != null) { _tenPriceText.text = "무료 (시뮬레이션)"; _tenPriceText.color = _tenPriceNormalColor; }
            }
            else
            {
                SetPrice(_singlePriceText, _singlePriceNormalColor, table.SingleCost, core);
                SetPrice(_tenPriceText, _tenPriceNormalColor, table.TenPullCost, core);
            }
            if (_discountText != null) _discountText.text = $"{GachaRateFormatter.GetTenPullDiscountPercent(table)}% 할인";

            // 시뮬레이터 전환 버튼 글자 / 안내 글자 / 초기화 버튼은 모드에 맞게 보이고 숨긴다.
            if (_simToggleLabel != null) _simToggleLabel.text = sim ? "시뮬레이터: 켜짐" : "시뮬레이터: 꺼짐";
            if (_simInfoText != null)
            {
                _simInfoText.gameObject.SetActive(sim);
                if (sim) _simInfoText.text = $"SIMULATION   누적 {_sim.TotalPulls}회  ·  SSR {_sim.SsrCount}개   (코어·보유 드론·실제 천장에 영향 없음)";
            }
            if (_simResetButton != null) _simResetButton.gameObject.SetActive(sim);
        }

        private static void SetPrice(Text priceText, Color normalColor, int cost, int core)
        {
            if (priceText == null) return;
            priceText.text = $"{cost:N0} 코어";
            priceText.color = core >= cost ? normalColor : ShortColor;
        }

        // 뒤로: 선택 화면이 있으면 선택 화면으로, 없으면 로비로 나간다. (선택 화면에서 뒤로를 누르면 로비)
        private void OnBackClicked()
        {
            PlayClick();
            if (_selectEnabled && _select != null) _select.Open();
            else SceneManager.LoadScene(lobbySceneName);
        }

        // ---- 뽑기 ----
        private void Pull(bool isTen)
        {
            var controller = GachaController.Instance;
            if (controller == null) return;

            PlayClick();

            // 시뮬레이터 모드: 코어 차감·보유 목록·저장 없이 시뮬레이터에서만 뽑는다.
            // (게이지는 시뮬레이터의 Changed 이벤트로 이미 갱신된다)
            if (_simMode && _sim != null)
            {
                if (_resultUI != null) _resultUI.Show(isTen ? _sim.PullTen() : _sim.PullSingle(), isTen, _sim);
                return;
            }

            GachaPullReport report = isTen ? controller.PullTen() : controller.PullSingle();

            if (!report.success)
            {
                if (report.failure == GachaPullFailure.InsufficientCore) ShowInsufficient();
                return;
            }

            Refresh();
            if (_resultUI != null) _resultUI.Show(report, isTen);
        }

        // ---- 시뮬레이터 ----
        // 시뮬레이터 모드를 켜고 끈다. 처음 켤 때 새 시뮬레이터(천장 0)를 만들고, 껐다 켜도 같은 시뮬레이터를 이어서 쓴다.
        private void ToggleSim()
        {
            var controller = GachaController.Instance;
            if (controller == null || controller.Table == null) return;

            PlayClick();
            _simMode = !_simMode;
            if (_simMode && _sim == null)
            {
                _sim = new SimGachaSession(controller.Table);
                _sim.Changed += Refresh;
            }
            Refresh();
        }

        // 시뮬레이터의 천장과 누적 숫자를 0으로 되돌린다.
        private void ResetSim()
        {
            if (_sim == null) return;
            PlayClick();
            _sim.Reset(); // Changed 이벤트로 화면이 다시 그려진다
        }

        private void ShowInsufficient()
        {
            if (_insufficientMessage != null) _insufficientMessage.text = "코어가 부족합니다";
            if (_insufficientPopup != null) _insufficientPopup.SetActive(true);
        }

        // ---- 도우미 ----
        private void PlayClick() => AudioManager.Instance?.PlaySfx(clickSound);

        private Text FindText(string path)
        {
            var child = transform.Find(path);
            var text = child != null ? child.GetComponent<Text>() : null;
            if (text == null) Debug.LogWarning($"[Gacha] 뽑기 화면에서 '{path}' 글자 칸을 찾지 못했습니다.");
            return text;
        }

        // warnIfMissing을 false로 주면 버튼이 없어도 경고하지 않는다 (락온 뽑기처럼 일부러 없을 수 있는 버튼용).
        private Button WireButton(string path, UnityEngine.Events.UnityAction action, bool warnIfMissing = true)
        {
            var child = transform.Find(path);
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
            {
                if (warnIfMissing) Debug.LogWarning($"[Gacha] 뽑기 화면에서 '{path}' 버튼을 찾지 못했습니다.");
                return null;
            }
            button.onClick.AddListener(action);
            return button;
        }
    }
}
