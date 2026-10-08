using System;
using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 저장 슬롯 선택 화면. 메인 메뉴에서 "게임 시작"을 누르면 (슬롯 기능이 켜져 있을 때) 나오는, 카드 N장짜리 화면이다.
    //   - 이미 쓰는 슬롯: 코어·크레딧·해금 스테이지·보유 드론·플레이한 판·마지막 저장 시각을 보여 주고, 누르면 이어서 한다.
    //   - 비어 있는 슬롯: "새로 시작"이라고 보여 주고, 누르면 확인창을 거쳐 처음부터 시작한다. (시작 코어 2,700 등을 새로 받는다)
    // 슬롯은 삭제할 수 없다. 그래서 새로 시작하기 전에 한 번 확인한다.
    //
    // 이 스크립트는 "고르기"만 한다. 고른 뒤에 어떤 씬으로 갈지는 이 화면을 가진 MainMenuController가 SlotChosen 이벤트를 받아서 처리한다.
    // 실제 저장·불러오기는 SaveManager.SelectSlot이 하고, 재화·보유 드론 같은 매니저들은 SaveManager.DataReplaced로 새 데이터에 다시 연결된다.
    //
    // 구조: SaveSlotSceneBuilder가 아래 이름의 자식을 만든다. 이름을 바꾸면 연결이 끊긴다:
    //   BtnBack, Slot_1 .. Slot_N (각각 TitleText, StatusText, InfoText), ConfirmPopup(Box/MessageText, Box/BtnYes, Box/BtnNo)
    public class SaveSlotSelectUI : MonoBehaviour
    {
        [SerializeField] private AudioClip clickSound;

        // 뒤로(타이틀 화면으로)를 눌렀을 때 / 슬롯을 골라서 불러오기·새로 시작이 끝났을 때(슬롯 번호, 새로 시작했는지).
        public event Action BackPressed;
        public event Action<int, bool> SlotChosen;

        private static readonly Color CyanColor = new Color(0.310f, 0.847f, 0.910f);
        private static readonly Color MutedColor = new Color(0.588f, 0.706f, 0.714f);
        private static readonly Color NewColor = new Color(1f, 0.82f, 0.25f); // 금색: 새로 시작

        private class SlotView
        {
            public Text titleText;
            public Text statusText;
            public Text infoText;
            public Text enterText;
        }

        private readonly System.Collections.Generic.List<SlotView> _slots = new System.Collections.Generic.List<SlotView>();
        private GameObject _confirmPopup;
        private Text _confirmMessage;
        private int _pendingSlot;

        private void Awake()
        {
            WireButton("BtnBack", () => { PlayClick(); BackPressed?.Invoke(); });

            for (int i = 1; ; i++)
            {
                var slot = transform.Find($"Slot_{i}");
                if (slot == null) break;
                var button = slot.GetComponent<Button>();
                int slotNumber = i; // 반복문 변수를 그대로 쓰면 모든 버튼이 마지막 번호를 보게 되므로 복사해 둔다
                if (button != null) button.onClick.AddListener(() => OnSlotClicked(slotNumber));

                var enter = slot.Find("EnterLabel");
                _slots.Add(new SlotView
                {
                    titleText = slot.Find("TitleText").GetComponent<Text>(),
                    statusText = slot.Find("StatusText").GetComponent<Text>(),
                    infoText = slot.Find("InfoText").GetComponent<Text>(),
                    enterText = enter != null ? enter.GetComponent<Text>() : null,
                });
            }
            if (_slots.Count == 0) Debug.LogWarning("[Save] 슬롯 선택 화면에서 'Slot_1' 카드를 찾지 못했습니다.");

            var confirm = transform.Find("ConfirmPopup");
            if (confirm != null)
            {
                _confirmPopup = confirm.gameObject;
                var message = confirm.Find("Box/MessageText");
                _confirmMessage = message != null ? message.GetComponent<Text>() : null;
                WireButtonAt(confirm, "Box/BtnYes", () =>
                {
                    PlayClick();
                    _confirmPopup.SetActive(false);
                    Choose(_pendingSlot);
                });
                WireButtonAt(confirm, "Box/BtnNo", () => { PlayClick(); _confirmPopup.SetActive(false); });
                _confirmPopup.SetActive(false);
            }
            else Debug.LogWarning("[Save] 슬롯 선택 화면에서 'ConfirmPopup'을 찾지 못했습니다.");
        }

        // 화면이 열릴 때마다 슬롯 내용을 다시 읽어서 채운다.
        private void OnEnable()
        {
            if (_confirmPopup != null) _confirmPopup.SetActive(false);
            Refresh();
        }

        public void Open() => gameObject.SetActive(true);
        public void Close() => gameObject.SetActive(false);

        private void Refresh()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var view = _slots[i];
                int slotNumber = i + 1;
                view.titleText.text = $"슬롯 {slotNumber}";

                // 지금 막 이 화면에 들어온 것이므로 파일에서 읽는다. (SaveManager가 메모리의 최신 데이터를 우선 돌려준다)
                var data = SaveManager.PeekSlot(slotNumber);
                if (data == null)
                {
                    view.statusText.text = "비어 있음";
                    view.statusText.color = NewColor;
                    view.infoText.text = "처음부터 새로 시작해요\n(시작 코어 2,700)";
                    if (view.enterText != null) { view.enterText.text = "새로 시작  >"; view.enterText.color = NewColor; }
                }
                else
                {
                    view.statusText.text = SaveManager.CurrentSlot == slotNumber ? "플레이 중" : "저장됨";
                    view.statusText.color = CyanColor;
                    view.infoText.text = BuildSummary(data);
                    if (view.enterText != null) { view.enterText.text = "이어서 하기  >"; view.enterText.color = CyanColor; }
                }
            }
        }

        // 슬롯 하나의 요약 글. 예: "코어 2,700  ·  크레딧 300\n해금 스테이지 2  ·  드론 3종  ·  플레이 5판\n마지막 저장 10-08 12:30"
        public static string BuildSummary(SaveData data)
        {
            string saved = string.IsNullOrEmpty(data.lastSavedAt) ? "-" : data.lastSavedAt;
            return $"코어 {data.core:N0}  ·  크레딧 {data.credit:N0}\n" +
                   $"해금 스테이지 {Mathf.Max(1, data.unlockedStageCount)}  ·  드론 {data.ownedDrones.Count}종  ·  플레이 {data.logStats.matches}판\n" +
                   $"마지막 저장 {saved}";
        }

        private void OnSlotClicked(int slot)
        {
            PlayClick();
            var data = SaveManager.PeekSlot(slot);
            if (data != null)
            {
                Choose(slot); // 이미 쓰는 슬롯은 바로 이어서 한다
                return;
            }

            // 비어 있는 슬롯: 삭제할 수 없으니 새로 시작하기 전에 한 번 확인한다.
            _pendingSlot = slot;
            if (_confirmPopup == null)
            {
                Choose(slot);
                return;
            }
            if (_confirmMessage != null)
                _confirmMessage.text = $"슬롯 {slot}을 새로 시작할까요?\n\n시작 코어 2,700으로 처음부터 시작해요.\n슬롯은 삭제할 수 없어요.";
            _confirmPopup.SetActive(true);
        }

        private void Choose(int slot)
        {
            if (SaveManager.SelectSlot(slot, out bool created)) SlotChosen?.Invoke(slot, created);
            else Debug.LogWarning($"[Save] 슬롯 {slot}을 열지 못했습니다. (슬롯 기능이 꺼져 있거나 번호가 범위 밖입니다)");
        }

        private void PlayClick() => AudioManager.Instance?.PlaySfx(clickSound);

        private void WireButton(string path, UnityEngine.Events.UnityAction action) => WireButtonAt(transform, path, action);

        private void WireButtonAt(Transform parent, string path, UnityEngine.Events.UnityAction action)
        {
            var child = parent.Find(path);
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogWarning($"[Save] 슬롯 선택 화면에서 '{path}' 버튼을 찾지 못했습니다.");
                return;
            }
            button.onClick.AddListener(action);
        }
    }
}
