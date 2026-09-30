using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 뽑기 결과 화면. 뽑은 카드들을 순서대로 하나씩 공개하고, 신규 / 승급 / 중복(조각)을 표시한다.
    //
    // 흐름:
    //   1) 카드 뒷면("?")이 먼저 깔린다. 1회는 1장, 10연은 5x2 격자.
    //   2) 카드를 순서대로 한 장씩 공개한다. 공개 시간은 등급별로 다르다 (N 0.4 / R 0.6 / SR 1.0 / SSR 2.0초 — GachaTable).
    //      기다리는 동안 카드 테두리가 그 등급 색으로 맥동하고, SSR은 공개되는 순간 금색 번쩍임이 있다.
    //   3) 우상단 "스킵"을 누르면 언제든 남은 카드를 한 번에 공개한다.
    //   4) 다 공개되면 "확인"(닫기)과 "다시 뽑기"(같은 종류로 한 번 더, 코어가 모자라면 비활성) 버튼이 나타난다.
    //
    // 구조: 이 스크립트가 붙은 패널 아래에 정해진 이름의 자식이 있어야 한다 (GachaSceneBuilder가 만든다):
    //   CardContainer, CardTemplate(꺼져 있는 견본), BtnSkip, BtnConfirm, BtnAgain(LabelText, PriceText), Flash
    public class GachaResultUI : MonoBehaviour
    {
        [SerializeField] private AudioClip clickSound;

        // 카드를 뒤집는 데 걸리는 시간(초). 카드 하나의 공개 시간 중 마지막 이만큼이 뒤집는 동작이고, 나머지는 기다리는 시간이다.
        private const float FlipSeconds = 0.2f;

        private static readonly Color CyanColor = new Color(0.310f, 0.847f, 0.910f);
        private static readonly Color MutedColor = new Color(0.588f, 0.706f, 0.714f);
        private static readonly Color BackOutlineColor = new Color(0.300f, 0.360f, 0.460f, 0.9f);
        private static readonly Color ShortColor = new Color(1f, 0.35f, 0.35f);

        private class CardView
        {
            public GameObject root;
            public Outline outline;
            public GameObject back;
            public GameObject face;
            public Text rarityText;
            public Text nameText;
            public Text tagText;
            public bool revealed;
        }

        private readonly List<CardView> _cards = new List<CardView>();

        private Transform _container;
        private GridLayoutGroup _grid;
        private GameObject _template;
        private Button _skipButton;
        private Button _confirmButton;
        private Button _againButton;
        private Text _againLabel;
        private Text _againPrice;
        private Color _againPriceNormalColor;
        private Image _flash;

        private GachaPullReport _report;
        private bool _isTen;
        private Coroutine _routine;
        private Coroutine _flashRoutine;
        private bool _finished;

        private void Awake()
        {
            _container = transform.Find("CardContainer");
            _grid = _container != null ? _container.GetComponent<GridLayoutGroup>() : null;
            var template = transform.Find("CardTemplate");
            _template = template != null ? template.gameObject : null;
            if (_container == null || _grid == null || _template == null)
                Debug.LogWarning("[Gacha] 결과 화면에서 CardContainer(GridLayoutGroup) 또는 CardTemplate을 찾지 못했습니다.");
            if (_template != null) _template.SetActive(false);

            _skipButton = WireButton("BtnSkip", Skip);
            _confirmButton = WireButton("BtnConfirm", Confirm);
            _againButton = WireButton("BtnAgain", Again);
            _againLabel = FindText("BtnAgain/LabelText");
            _againPrice = FindText("BtnAgain/PriceText");
            if (_againPrice != null) _againPriceNormalColor = _againPrice.color;

            var flash = transform.Find("Flash");
            _flash = flash != null ? flash.GetComponent<Image>() : null;
        }

        private void OnEnable()
        {
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.OnCoreChanged += HandleCoreChanged;
        }

        private void OnDisable()
        {
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.OnCoreChanged -= HandleCoreChanged;
        }

        private void HandleCoreChanged(int value) => RefreshAgainButton();

        // 결과 화면을 열고 공개 연출을 시작한다.
        public void Show(GachaPullReport report, bool isTen)
        {
            if (!report.success || report.pulls.Length == 0) return;

            _report = report;
            _isTen = isTen;
            _finished = false;
            gameObject.SetActive(true);

            StopRoutines();
            BuildCards();
            SetButtons(revealing: true);
            _routine = StartCoroutine(RevealRoutine());
        }

        // ---------------- 카드 만들기 ----------------
        private void BuildCards()
        {
            // Destroy는 이번 프레임이 끝난 뒤에야 실제로 지워지므로, 먼저 꺼서 격자 배치에서 바로 빠지게 한다.
            foreach (var card in _cards)
            {
                card.root.SetActive(false);
                Destroy(card.root);
            }
            _cards.Clear();
            if (_template == null || _container == null) return;

            if (_grid != null) _grid.constraintCount = _isTen ? 5 : 1;

            for (int i = 0; i < _report.pulls.Length; i++)
            {
                var root = Instantiate(_template, _container);
                root.name = $"Card_{i + 1}";
                root.SetActive(true);

                var card = new CardView
                {
                    root = root,
                    outline = root.GetComponent<Outline>(),
                    back = root.transform.Find("Back").gameObject,
                    face = root.transform.Find("Face").gameObject,
                    rarityText = root.transform.Find("Face/RarityText").GetComponent<Text>(),
                    nameText = root.transform.Find("Face/DroneNameText").GetComponent<Text>(),
                    tagText = root.transform.Find("Face/TagText").GetComponent<Text>(),
                };
                card.back.SetActive(true);
                card.face.SetActive(false);
                if (card.outline != null)
                {
                    card.outline.effectColor = BackOutlineColor;
                    card.outline.effectDistance = new Vector2(1f, 1f);
                }
                _cards.Add(card);
            }
        }

        // ---------------- 공개 연출 ----------------
        // 카드마다 "공개 시간"이 정해져 있고(등급별), 카드들을 순서대로 이어 붙인 시간표를 따라간다.
        // 프레임마다 조금씩 늦어지는 오차가 카드 10장에 걸쳐 쌓이지 않도록, 카드별로 "지난 시간을 더하는" 대신
        // 처음 시각(startTime)을 기준으로 한 목표 시각에 맞춰 진행한다.
        private IEnumerator RevealRoutine()
        {
            var table = GachaController.Instance != null ? GachaController.Instance.Table : null;
            float startTime = Time.unscaledTime;
            float cardStart = 0f; // 지금 카드의 시작 시각 (startTime 기준 초)

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                var pull = _report.pulls[i];
                Color rarityColor = RarityColors.Get(pull.rarity);
                float duration = table != null ? table.GetRevealSeconds(pull.rarity) : 0.4f;
                float flip = Mathf.Min(FlipSeconds, duration);
                float cardEnd = cardStart + duration;
                float flipStart = cardEnd - flip;

                // 기다리는 동안: 테두리가 그 등급 색으로 맥동한다 (높은 등급일수록 오래 반짝인다)
                while (Time.unscaledTime - startTime < flipStart)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin((Time.unscaledTime - startTime - cardStart) * 14f);
                    if (card.outline != null)
                    {
                        card.outline.effectColor = Color.Lerp(BackOutlineColor, rarityColor, pulse);
                        card.outline.effectDistance = Vector2.one * Mathf.Lerp(1f, 3f, pulse);
                    }
                    yield return null;
                }

                // 뒤집기: 가로로 납작해졌다가(그 순간 앞면으로 바꾸고) 다시 펴진다
                while (Time.unscaledTime - startTime < cardEnd)
                {
                    float progress = flip > 0f ? Mathf.Clamp01((Time.unscaledTime - startTime - flipStart) / flip) : 1f;
                    if (progress < 0.5f)
                    {
                        SetCardScaleX(card, 1f - progress * 2f);
                    }
                    else
                    {
                        RevealFace(i); // 이미 공개됐다면 아무 일도 하지 않는다
                        SetCardScaleX(card, (progress - 0.5f) * 2f);
                    }
                    yield return null;
                }
                RevealFace(i);
                SetCardScaleX(card, 1f);

                cardStart = cardEnd;
            }

            Finish();
        }

        // i번째 카드를 앞면으로 바꾸고 내용을 채운다. (연출 없이 즉시)
        private void RevealFace(int index)
        {
            var card = _cards[index];
            if (card.revealed) return;
            card.revealed = true;

            var pull = _report.pulls[index];
            var outcome = _report.outcomes[index];
            Color rarityColor = RarityColors.Get(pull.rarity);

            card.back.SetActive(false);
            card.face.SetActive(true);
            card.rarityText.text = pull.rarity.ToString();
            card.rarityText.color = rarityColor;
            card.nameText.text = DroneNames.Get(pull.drone);

            switch (outcome.outcome)
            {
                case PullOutcome.New:
                    card.tagText.text = "NEW";
                    card.tagText.color = CyanColor;
                    break;
                case PullOutcome.Promoted:
                    card.tagText.text = $"승급  {outcome.previousRarity} → {pull.rarity}";
                    card.tagText.color = CyanColor;
                    break;
                default:
                    card.tagText.text = $"중복  조각 +{outcome.shardsGained}";
                    card.tagText.color = MutedColor;
                    break;
            }

            if (card.outline != null)
            {
                card.outline.effectColor = rarityColor;
                card.outline.effectDistance = pull.rarity == GachaRarity.SSR ? new Vector2(4f, 4f) : new Vector2(2f, 2f);
            }

            // SSR이 공개되는 순간 화면 전체가 금색으로 번쩍인다
            if (pull.rarity == GachaRarity.SSR && _flash != null)
            {
                if (_flashRoutine != null) StopCoroutine(_flashRoutine);
                _flashRoutine = StartCoroutine(FlashRoutine());
            }
        }

        private IEnumerator FlashRoutine()
        {
            const float duration = 0.45f;
            var color = RarityColors.Gold;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                _flash.color = new Color(color.r, color.g, color.b, 0.45f * (1f - t / duration));
                yield return null;
            }
            _flash.color = new Color(color.r, color.g, color.b, 0f);
            _flashRoutine = null;
        }

        private static void SetCardScaleX(CardView card, float x)
        {
            card.root.transform.localScale = new Vector3(Mathf.Max(0.02f, x), 1f, 1f);
        }

        // ---------------- 버튼 ----------------
        // 스킵: 남은 카드를 연출 없이 한 번에 공개한다.
        private void Skip()
        {
            if (_finished) return;
            PlayClick();

            StopRoutines();
            for (int i = 0; i < _cards.Count; i++)
            {
                SetCardScaleX(_cards[i], 1f);
                RevealFace(i);
            }
            Finish();
        }

        private void Confirm()
        {
            PlayClick();
            StopRoutines();
            gameObject.SetActive(false);
        }

        // 같은 종류(1회 / 10연)로 한 번 더 뽑는다. 코어가 모자라면 이 버튼은 비활성이다.
        private void Again()
        {
            var controller = GachaController.Instance;
            if (controller == null || !_finished) return;

            PlayClick();
            GachaPullReport report = _isTen ? controller.PullTen() : controller.PullSingle();
            if (!report.success)
            {
                RefreshAgainButton();
                return;
            }
            Show(report, _isTen);
        }

        // 모든 카드가 공개된 뒤: 스킵 버튼을 숨기고 확인 / 다시 뽑기 버튼을 보여준다.
        private void Finish()
        {
            _finished = true;
            _routine = null;
            SetButtons(revealing: false);
            RefreshAgainButton();
        }

        private void SetButtons(bool revealing)
        {
            if (_skipButton != null) _skipButton.gameObject.SetActive(revealing);
            if (_confirmButton != null) _confirmButton.gameObject.SetActive(!revealing);
            if (_againButton != null) _againButton.gameObject.SetActive(!revealing);
        }

        private void RefreshAgainButton()
        {
            var controller = GachaController.Instance;
            var currency = CurrencyManager.Instance;
            if (controller == null || currency == null || _againButton == null) return;

            int cost = _isTen ? controller.TenPullCost : controller.SingleCost;
            bool canAfford = currency.CanAffordCore(cost);

            if (_againLabel != null) _againLabel.text = _isTen ? "10연 다시 뽑기" : "1회 다시 뽑기";
            if (_againPrice != null)
            {
                _againPrice.text = $"{cost:N0} 코어";
                _againPrice.color = canAfford ? _againPriceNormalColor : ShortColor;
            }
            _againButton.interactable = canAfford;
        }

        // ---------------- 도우미 ----------------
        private void StopRoutines()
        {
            if (_routine != null) StopCoroutine(_routine);
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _routine = null;
            _flashRoutine = null;
            if (_flash != null) _flash.color = new Color(_flash.color.r, _flash.color.g, _flash.color.b, 0f);
        }

        private void PlayClick() => AudioManager.Instance?.PlaySfx(clickSound);

        private Text FindText(string path)
        {
            var child = transform.Find(path);
            var text = child != null ? child.GetComponent<Text>() : null;
            if (text == null) Debug.LogWarning($"[Gacha] 결과 화면에서 '{path}' 글자 칸을 찾지 못했습니다.");
            return text;
        }

        private Button WireButton(string path, UnityEngine.Events.UnityAction action)
        {
            var child = transform.Find(path);
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogWarning($"[Gacha] 결과 화면에서 '{path}' 버튼을 찾지 못했습니다.");
                return null;
            }
            button.onClick.AddListener(action);
            return button;
        }
    }
}
