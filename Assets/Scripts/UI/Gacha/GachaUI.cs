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

            WireButton("BtnBack", () => { PlayClick(); SceneManager.LoadScene(lobbySceneName); });
            WireButton("RatesPanel/BtnRates", () => { PlayClick(); if (_probabilityPopup != null) _probabilityPopup.Open(); });
            _singleButton = WireButton("BtnPullSingle", () => Pull(false));
            _tenButton = WireButton("BtnPullTen", () => Pull(true));
            _singlePriceText = FindText("BtnPullSingle/PriceText");
            _tenPriceText = FindText("BtnPullTen/PriceText");
            _discountText = FindText("BtnPullTen/BadgeBack/DiscountBadge");
            if (_singlePriceText != null) _singlePriceNormalColor = _singlePriceText.color;
            if (_tenPriceText != null) _tenPriceNormalColor = _tenPriceText.color;

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

            // 천장 게이지: "SSR 확정까지  누적 42 / 70"과 채워지는 막대
            int pity = controller.PityCount;
            int limit = controller.PityLimit;
            if (_pityText != null) _pityText.text = $"SSR 확정까지    누적 {pity} / {limit}";
            if (_pityBarFill != null)
            {
                float ratio = limit > 0 ? Mathf.Clamp01((float)pity / limit) : 0f;
                _pityBarFill.anchorMax = new Vector2(ratio, 1f);
            }

            // 소천장 게이지: "SR 이상 보장까지  누적 7 / 10" (횟수가 0이면 소천장을 쓰지 않으므로 숨긴다)
            int softLimit = controller.SoftPityLimit;
            bool softOn = softLimit > 0;
            if (_softPityText != null)
            {
                _softPityText.gameObject.SetActive(softOn);
                if (softOn) _softPityText.text = $"SR 이상 보장까지    누적 {controller.SoftPityCount} / {softLimit}";
            }
            if (_softPityBarFill != null)
            {
                _softPityBarFill.transform.parent.gameObject.SetActive(softOn);
                float softRatio = softOn ? Mathf.Clamp01((float)controller.SoftPityCount / softLimit) : 0f;
                _softPityBarFill.anchorMax = new Vector2(softRatio, 1f);
            }

            // 가격 표시. 코어가 모자라면 가격 글자만 빨갛게 (버튼은 눌러서 "부족" 안내를 볼 수 있게 켜 둔다).
            SetPrice(_singlePriceText, _singlePriceNormalColor, table.SingleCost, core);
            SetPrice(_tenPriceText, _tenPriceNormalColor, table.TenPullCost, core);
            if (_discountText != null) _discountText.text = $"{GachaRateFormatter.GetTenPullDiscountPercent(table)}% 할인";
        }

        private static void SetPrice(Text priceText, Color normalColor, int cost, int core)
        {
            if (priceText == null) return;
            priceText.text = $"{cost:N0} 코어";
            priceText.color = core >= cost ? normalColor : ShortColor;
        }

        // ---- 뽑기 ----
        private void Pull(bool isTen)
        {
            var controller = GachaController.Instance;
            if (controller == null) return;

            PlayClick();
            GachaPullReport report = isTen ? controller.PullTen() : controller.PullSingle();

            if (!report.success)
            {
                if (report.failure == GachaPullFailure.InsufficientCore) ShowInsufficient();
                return;
            }

            Refresh();
            if (_resultUI != null) _resultUI.Show(report, isTen);
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

        private Button WireButton(string path, UnityEngine.Events.UnityAction action)
        {
            var child = transform.Find(path);
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogWarning($"[Gacha] 뽑기 화면에서 '{path}' 버튼을 찾지 못했습니다.");
                return null;
            }
            button.onClick.AddListener(action);
            return button;
        }
    }
}
