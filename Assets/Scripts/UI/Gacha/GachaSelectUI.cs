using System;
using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 뽑기 선택 화면. 로비의 "뽑기"를 누르면 가장 먼저 보이는, 카드 두 장짜리 화면이다.
    //   왼쪽 카드 = 일반 뽑기 (SSR을 노리는 기본 뽑기)
    //   오른쪽 카드 = 락온 뽑기 (3칸을 공개하고 마음에 드는 칸을 잠그며 SR 이상을 모으는 뽑기)
    // 락온 뽑기를 처음 하는 사람이 못 보고 바로 일반 뽑기로 가 버리지 않게, 두 뽑기의 차이를 카드에 적어서 고르게 한다.
    //
    // 이 스크립트는 "고른 결과를 알려주기"만 한다. 실제로 화면을 바꾸는 일(일반 뽑기 화면 열기, 락온 화면 열기, 로비로 나가기)은
    // 이 화면을 가진 GachaUI가 이벤트를 받아서 처리한다.
    //
    // 카드에 적히는 확률·가격은 전부 GachaTable / LockOnTable에서 읽어서 만든다 ("표기 = 실제" 원칙).
    //
    // 구조: LockOnSceneBuilder 옆의 GachaSelectSceneBuilder가 아래 이름의 자식을 만든다. 이름을 바꾸면 연결이 끊긴다:
    //   BtnBack, CoreText, NormalCard(DescText, Info1Text, Info2Text, Info3Text), LockOnCard(DescText, Info1Text, Info2Text, Info3Text, BadgeBack/BadgeText)
    public class GachaSelectUI : MonoBehaviour
    {
        // 락온 뽑기 수치표. 씬 빌더가 연결한다. (카드에 적을 가격·칸 수·SSR 여부를 읽는다)
        [SerializeField] private LockOnTable _lockOnTable;
        [SerializeField] private AudioClip clickSound;

        // 일반 뽑기를 골랐을 때 / 락온 뽑기를 골랐을 때 / 뒤로(로비로)를 눌렀을 때.
        public event Action NormalChosen;
        public event Action LockOnChosen;
        public event Action BackPressed;

        private Text _coreText;
        private Text _normalDesc, _normalInfo1, _normalInfo2, _normalInfo3;
        private Text _lockDesc, _lockInfo1, _lockInfo2, _lockInfo3;

        private void Awake()
        {
            WireButton("BtnBack", () => { PlayClick(); BackPressed?.Invoke(); });
            WireButton("NormalCard", () => { PlayClick(); NormalChosen?.Invoke(); });
            WireButton("LockOnCard", () => { PlayClick(); LockOnChosen?.Invoke(); });

            _coreText = FindText("CoreText");
            _normalDesc = FindText("NormalCard/DescText");
            _normalInfo1 = FindText("NormalCard/Info1Text");
            _normalInfo2 = FindText("NormalCard/Info2Text");
            _normalInfo3 = FindText("NormalCard/Info3Text");
            _lockDesc = FindText("LockOnCard/DescText");
            _lockInfo1 = FindText("LockOnCard/Info1Text");
            _lockInfo2 = FindText("LockOnCard/Info2Text");
            _lockInfo3 = FindText("LockOnCard/Info3Text");
        }

        private void OnEnable()
        {
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.OnCoreChanged += HandleCoreChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.OnCoreChanged -= HandleCoreChanged;
        }

        private void HandleCoreChanged(int value) => Refresh();

        public void Open() => gameObject.SetActive(true);
        public void Close() => gameObject.SetActive(false);

        // 코어 잔액과 두 카드의 설명을 현재 수치표 값으로 다시 쓴다.
        private void Refresh()
        {
            var currency = CurrencyManager.Instance;
            if (_coreText != null) _coreText.text = $"코어  {(currency != null ? currency.Core : 0):N0}";

            var normal = GachaController.Instance != null ? GachaController.Instance.Table : null;
            if (normal != null)
            {
                SetText(_normalDesc, "SSR을 노리는 뽑기");
                string soft = normal.SoftPityCount > 0 ? $"  (소천장 {normal.SoftPityCount}회)" : "";
                SetText(_normalInfo1, $"SSR {normal.GetRate(GachaRarity.SSR):0.#}%  ·  SSR 확정 천장 {normal.PityCount}회{soft}");
                SetText(_normalInfo2, $"1회 {normal.SingleCost:N0}  ·  10연 {normal.TenPullCost:N0} 코어");
                SetText(_normalInfo3, "나온 결과를 그대로 받아요");
            }

            if (_lockOnTable != null)
            {
                var t = _lockOnTable;
                SetText(_lockDesc, $"{t.LockMinRarity} 이상을 확실히 모으는 뽑기");
                SetText(_lockInfo1, $"{t.SlotCount}칸을 공개하고, 마음에 드는 칸({t.LockMinRarity} 이상)을 잠근 뒤 나머지만 다시 뽑아요");
                SetText(_lockInfo2, $"처음 공개 {t.FirstCost:N0}  ·  재뽑기는 칸당 {t.RerollUnitCost:N0} 코어");
                float ssr = t.GetRate(GachaRarity.SSR);
                SetText(_lockInfo3, ssr > 0f ? $"SSR {ssr:0.#}%" : "SSR은 나오지 않아요 (SSR은 일반 뽑기에서만)");
            }
        }

        private static void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }

        private void PlayClick() => AudioManager.Instance?.PlaySfx(clickSound);

        private Text FindText(string path)
        {
            var child = transform.Find(path);
            var text = child != null ? child.GetComponent<Text>() : null;
            if (text == null) Debug.LogWarning($"[Gacha] 뽑기 선택 화면에서 '{path}' 글자 칸을 찾지 못했습니다.");
            return text;
        }

        private void WireButton(string path, UnityEngine.Events.UnityAction action)
        {
            var child = transform.Find(path);
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogWarning($"[Gacha] 뽑기 선택 화면에서 '{path}' 버튼을 찾지 못했습니다.");
                return;
            }
            button.onClick.AddListener(action);
        }
    }
}
