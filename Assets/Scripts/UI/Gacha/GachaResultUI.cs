using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 뽑기 결과 화면. 뽑은 카드들을 플레이어가 직접 눌러서 한 장씩 개봉하고, 신규 / 승급 / 중복(조각)을 표시한다.
    //
    // 흐름:
    //   1) 카드 뒷면("?")이 먼저 깔린다. 1회는 1장, 10연은 5x2 격자. 안 연 카드의 테두리는 등급과 무관하게 똑같은 색으로 은은하게 맥동한다.
    //      (등급 색으로 맥동하면 열기 전에 등급이 보이기 때문)
    //   2) 카드를 누르면 그 카드만 뒤집힌다. 뒤집히는 순간 등급별 소리·화면 번쩍임·카드 팝이 나온다.
    //   3) 우상단 "모두 개봉"을 누르면 아직 안 연 카드를 한 번에 연다.
    //   4) 다 열리면 "확인"(닫기)과 "다시 뽑기"(같은 종류로 한 번 더, 코어가 모자라면 비활성) 버튼이 나타난다.
    //      (코어는 뽑는 순간 이미 쓰였고 결과도 정해져 있으므로, 열어 보는 순서는 결과에 영향이 없다)
    //
    // 구조: 이 스크립트가 붙은 패널 아래에 정해진 이름의 자식이 있어야 한다 (GachaSceneBuilder가 만든다):
    //   CardContainer, CardTemplate(꺼져 있는 견본), BtnSkip("모두 개봉" 버튼), BtnConfirm, BtnAgain(LabelText, PriceText), Flash
    public class GachaResultUI : MonoBehaviour
    {
        [SerializeField] private AudioClip clickSound;

        // 뽑기 연출 소리. 비워 두면(씬에 아직 연결하지 않았으면) 소리 없이 예전처럼 동작한다.
        // 효과음 볼륨 슬라이더에 따라 크기가 조절되는 AudioManager.PlaySfx로 재생한다.
        [SerializeField] private AudioClip revealSoundN;    // 등급별 공개음
        [SerializeField] private AudioClip revealSoundR;
        [SerializeField] private AudioClip revealSoundSR;
        [SerializeField] private AudioClip revealSoundSSR;

        // 카드를 뒤집는 데 걸리는 시간(초).
        private const float FlipSeconds = 0.2f;

        // 안 연 카드가 남아 있는 동안 제목 자리에 보여주는 안내 문구.
        private const string HintTitle = "카드를 눌러 개봉하세요";

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
            public Image droneIcon; // 드론 이름 왼쪽의 종류 아이콘 (견본에 "Icon"이 없으면 null)
            public bool revealed;
            public bool opening;          // 지금 뒤집히는 중인지 (같은 카드를 두 번 누르는 것을 막는다)
            public Coroutine flipRoutine; // 뒤집는 동작 코루틴 ("모두 개봉"이 중간에 끊을 수 있게 들고 있는다)
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
        private Text _titleText;
        private string _titleNormal;

        private GachaPullReport _report;
        private bool _isTen;

        // 시뮬레이터 결과를 보여주는 중이면 그 시뮬레이터 객체, 실제 뽑기 결과면 null.
        // "다시 뽑기"가 실제 뽑기가 아니라 시뮬레이터로 이어지도록 구분하는 데 쓴다.
        private SimGachaSession _sim;
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

            _skipButton = WireButton("BtnSkip", OpenAll); // 글자는 씬에서 "모두 개봉"
            _confirmButton = WireButton("BtnConfirm", Confirm);
            _againButton = WireButton("BtnAgain", Again);
            _againLabel = FindText("BtnAgain/LabelText");
            _againPrice = FindText("BtnAgain/PriceText");
            if (_againPrice != null) _againPriceNormalColor = _againPrice.color;

            // 제목 글자: 안 연 카드가 남아 있는 동안은 안내 문구로 바꿔 보여주고, 다 열리면 원래 글자로 되돌린다.
            _titleText = FindText("TitleText");
            if (_titleText != null) _titleNormal = _titleText.text;

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
        // sim: 시뮬레이터 결과일 때만 넘긴다. (실제 뽑기는 비워 둔다)
        public void Show(GachaPullReport report, bool isTen, SimGachaSession sim = null)
        {
            if (!report.success || report.pulls.Length == 0) return;

            _report = report;
            _isTen = isTen;
            _sim = sim;
            _finished = false;
            gameObject.SetActive(true);

            StopRoutines();
            BuildCards();
            SetButtons(revealing: true);
            if (_titleText != null) _titleText.text = HintTitle;
            _routine = StartCoroutine(HintRoutine());
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
                var droneIcon = root.transform.Find("Face/DroneNameText/Icon");
                card.droneIcon = droneIcon != null ? droneIcon.GetComponent<Image>() : null;
                card.back.SetActive(true);
                card.face.SetActive(false);
                if (card.outline != null)
                {
                    card.outline.effectColor = BackOutlineColor;
                    card.outline.effectDistance = new Vector2(1f, 1f);
                }
                // 카드를 누르면 그 카드를 개봉한다. 모양이 변하지 않게 전환 효과는 끈다.
                var button = root.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                int index = i; // 반복문 변수를 그대로 쓰면 모든 카드가 마지막 번호를 보게 되므로 복사해 둔다
                button.onClick.AddListener(() => OpenCard(index));

                _cards.Add(card);
            }
        }

        // ---------------- 개봉 ----------------
        // 안 연 카드의 테두리를 등급과 무관한 똑같은 색으로 은은하게 맥동시켜 "눌러 보라"는 신호를 준다.
        private IEnumerator HintRoutine()
        {
            while (true)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                foreach (var card in _cards)
                {
                    if (card.revealed || card.opening || card.outline == null) continue;
                    card.outline.effectColor = Color.Lerp(BackOutlineColor, CyanColor, pulse * 0.6f);
                    card.outline.effectDistance = Vector2.one * Mathf.Lerp(1f, 2.5f, pulse);
                }
                yield return null;
            }
        }

        // 카드 한 장을 누르면: 가로로 납작해졌다가(그 순간 앞면으로 바꾸고) 다시 펴지며 열린다.
        private void OpenCard(int index)
        {
            if (_finished || index < 0 || index >= _cards.Count) return;
            var card = _cards[index];
            if (card.revealed || card.opening) return;

            card.opening = true;
            card.flipRoutine = StartCoroutine(FlipRoutine(index));
        }

        private IEnumerator FlipRoutine(int index)
        {
            var card = _cards[index];
            float start = Time.unscaledTime;
            while (true)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - start) / FlipSeconds);
                if (progress < 0.5f)
                {
                    SetCardScaleX(card, 1f - progress * 2f);
                }
                else
                {
                    RevealFace(index); // 이미 공개됐다면 아무 일도 하지 않는다
                    SetCardScaleX(card, (progress - 0.5f) * 2f);
                }
                if (progress >= 1f) break;
                yield return null;
            }
            RevealFace(index);
            SetCardScaleX(card, 1f);
            card.opening = false;
            card.flipRoutine = null;
            StartCoroutine(PopRoutine(card, _report.pulls[index].rarity)); // 뒤집기가 끝난 카드가 "톡" 튀어 오른다

            if (AllOpened()) Finish();
        }

        // 모든 카드가 완전히 열렸는지 (뒤집는 중인 카드는 아직 아니다).
        private bool AllOpened()
        {
            foreach (var card in _cards)
                if (!card.revealed || card.opening) return false;
            return true;
        }

        // i번째 카드를 앞면으로 바꾸고 내용을 채운다. (연출 없이 즉시)
        // playEffects: 등급별 공개음과 번쩍임을 낼지. "모두 개봉"으로 한꺼번에 열 때는 끈다(소리가 겹쳐 시끄러워지는 것을 막으려고).
        //              단, SSR의 금색 번쩍임은 "모두 개봉"에서도 낸다.
        private void RevealFace(int index, bool playEffects = true)
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
            UiIcons.SetDroneIcon(card.droneIcon, pull.drone);

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
                case PullOutcome.Simulated:
                    // 시뮬레이터: 보유 목록과 무관하므로 NEW/중복 대신, 천장이 발동한 결과인지만 알려준다.
                    card.tagText.text = pull.isPityGuaranteed ? "SSR 천장 확정"
                                      : pull.isSoftPityGuaranteed ? "소천장 발동"
                                      : "시뮬레이션";
                    card.tagText.color = pull.isPityGuaranteed || pull.isSoftPityGuaranteed ? CyanColor : MutedColor;
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

            // 등급이 공개되는 순간의 소리와 화면 번쩍임.
            if (playEffects) PlayRevealSound(pull.rarity);
            if (playEffects || pull.rarity == GachaRarity.SSR) StartFlash(pull.rarity, rarityColor);
        }

        // 등급에 맞는 공개음을 낸다. 높은 등급일수록 소리가 화려하다.
        private void PlayRevealSound(GachaRarity rarity)
        {
            AudioClip clip;
            switch (rarity)
            {
                case GachaRarity.SSR: clip = revealSoundSSR; break;
                case GachaRarity.SR: clip = revealSoundSR; break;
                case GachaRarity.R: clip = revealSoundR; break;
                default: clip = revealSoundN; break;
            }
            AudioManager.Instance?.PlaySfx(clip, 0.75f);
        }

        // 화면 전체 번쩍임: SSR은 금색으로 강하게(예전과 같음), SR은 보라색으로 중간, R은 파란색으로 은은하게. N은 없다.
        private void StartFlash(GachaRarity rarity, Color color)
        {
            if (_flash == null || rarity == GachaRarity.N) return;

            float alpha = rarity == GachaRarity.SSR ? 0.45f : rarity == GachaRarity.SR ? 0.25f : 0.10f;
            float duration = rarity == GachaRarity.SSR ? 0.45f : rarity == GachaRarity.SR ? 0.35f : 0.25f;

            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine(color, alpha, duration));
        }

        private IEnumerator FlashRoutine(Color color, float alpha, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                _flash.color = new Color(color.r, color.g, color.b, alpha * (1f - t / duration));
                yield return null;
            }
            _flash.color = new Color(color.r, color.g, color.b, 0f);
            _flashRoutine = null;
        }

        // 카드가 뒤집힌 직후 살짝 커졌다 돌아오는 "톡" 효과. SSR은 그 뒤에도 테두리가 금색으로 천천히 맥동한다.
        // 카드가 지워지면(다시 뽑기) 스스로 끝난다.
        private IEnumerator PopRoutine(CardView card, GachaRarity rarity)
        {
            const float popDuration = 0.25f;
            for (float t = 0f; t < popDuration; t += Time.unscaledDeltaTime)
            {
                if (card.root == null) yield break;
                float k = 1f + 0.12f * Mathf.Sin(Mathf.PI * t / popDuration);
                card.root.transform.localScale = new Vector3(k, k, 1f);
                yield return null;
            }
            if (card.root == null) yield break;
            card.root.transform.localScale = Vector3.one;

            if (rarity != GachaRarity.SSR || card.outline == null) yield break;

            Color gold = RarityColors.Gold;
            Color bright = Color.Lerp(gold, Color.white, 0.6f);
            while (card.root != null && card.outline != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
                card.outline.effectColor = Color.Lerp(gold, bright, pulse);
                card.outline.effectDistance = Vector2.one * Mathf.Lerp(3f, 6f, pulse);
                yield return null;
            }
        }

        private static void SetCardScaleX(CardView card, float x)
        {
            card.root.transform.localScale = new Vector3(Mathf.Max(0.02f, x), 1f, 1f);
        }

        // ---------------- 버튼 ----------------
        // 모두 개봉: 아직 안 연 카드를 연출 없이 한 번에 연다.
        private void OpenAll()
        {
            if (_finished) return;
            PlayClick();

            StopRoutines();

            // 카드마다 소리를 내면 한꺼번에 겹쳐 시끄러우므로, 이번에 새로 연 카드 중 가장 높은 등급의 공개음 하나만 낸다.
            GachaRarity highest = GachaRarity.N;
            bool openedAny = false;
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card.revealed && !card.opening) continue; // 이미 다 열린 카드

                if (card.flipRoutine != null) StopCoroutine(card.flipRoutine); // 뒤집는 도중이면 거기서 끊고 바로 연다
                card.flipRoutine = null;
                card.opening = false;
                SetCardScaleX(card, 1f);

                bool wasRevealed = card.revealed;
                RevealFace(i, false);
                if (!wasRevealed)
                {
                    openedAny = true;
                    if (_report.pulls[i].rarity > highest) highest = _report.pulls[i].rarity;
                }
            }
            if (openedAny) PlayRevealSound(highest);
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
            if (!_finished) return;

            // 시뮬레이터 결과였다면 시뮬레이터로 한 번 더 (코어를 쓰지 않는다).
            if (_sim != null)
            {
                PlayClick();
                Show(_isTen ? _sim.PullTen() : _sim.PullSingle(), _isTen, _sim);
                return;
            }

            var controller = GachaController.Instance;
            if (controller == null) return;

            PlayClick();
            GachaPullReport report = _isTen ? controller.PullTen() : controller.PullSingle();
            if (!report.success)
            {
                RefreshAgainButton();
                return;
            }
            Show(report, _isTen);
        }

        // 모든 카드가 열린 뒤: "모두 개봉" 버튼을 숨기고 확인 / 다시 뽑기 버튼을 보여준다.
        private void Finish()
        {
            _finished = true;
            // 안 연 카드 테두리 맥동을 끝낸다. (방금 낸 화면 번쩍임은 끊기지 않게 StopRoutines는 쓰지 않는다)
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            if (_titleText != null) _titleText.text = _titleNormal;
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
            // 시뮬레이터: 코어를 쓰지 않으므로 항상 누를 수 있다.
            if (_sim != null && _againButton != null)
            {
                if (_againLabel != null) _againLabel.text = _isTen ? "10연 다시 뽑기" : "1회 다시 뽑기";
                if (_againPrice != null)
                {
                    _againPrice.text = "무료 (시뮬레이션)";
                    _againPrice.color = _againPriceNormalColor;
                }
                _againButton.interactable = true;
                return;
            }

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
