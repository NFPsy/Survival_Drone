using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 락온 뽑기 화면. 뽑기 화면(Gacha 씬) 안의 "LockOnPanel"에 붙어서, 화면 전체를 덮으며 나타난다.
    // 실제 규칙 처리는 LockOnSession이 하고, 이 스크립트는 버튼 입력을 넘기고 결과를 화면에 그리기만 한다.
    //
    // 흐름 (LockOnPhase):
    //   Idle   : 카드 3장이 뒷면("?")으로 놓여 있고 "락온 시작" 버튼만 보인다.
    //   Active : 3칸이 공개됨. SR 이상 칸에 "잠금" 버튼이 나타난다. 아래에 "재뽑기"(잠그지 않은 칸만 다시)와 "확정" 버튼.
    //   Done   : 확정한 결과(받은 칸 NEW/승급/조각, 못 받은 칸의 환산 조각)를 카드에 보여주고 "한 번 더" / "나가기" 버튼.
    //
    // 안전장치: 잠근 칸이 없는데 확정하려 하거나, 진행 중인 판을 두고 나가려 하면 먼저 확인창을 띄운다.
    //   (나가려 할 때 "예"를 누르면 확정하고 나간다 — 쓴 코어가 아무것도 못 받은 채 사라지지 않게 하려고)
    //
    // 구조: LockOnSceneBuilder가 아래 이름의 자식을 만든다. 이름을 바꾸면 연결이 끊긴다:
    //   BtnBack, CoreText, SubtitleText, BtnRates, SlotContainer/Slot_0.._N(Back, Face/{RarityText,DroneNameText,TagText}, BtnLock),
    //   InfoText, MessageText, BtnStart, BtnReroll, BtnConfirm, BtnAgain, BtnClose,
    //   RatesPopup(Box/RatesText, Box/RulesText, Box/BtnClose), ConfirmPopup(Box/MessageText, Box/BtnYes, Box/BtnNo)
    public class LockOnUI : MonoBehaviour
    {
        // 규칙 수치(확률·가격·사용 여부)가 들어 있는 데이터 파일. 씬 빌더가 연결한다.
        [SerializeField] private LockOnTable _table;
        [SerializeField] private AudioClip clickSound;

        private static readonly Color CyanColor = new Color(0.310f, 0.847f, 0.910f);
        private static readonly Color MutedColor = new Color(0.588f, 0.706f, 0.714f);
        private static readonly Color BackOutlineColor = new Color(0.300f, 0.360f, 0.460f, 0.9f);
        private static readonly Color ShortColor = new Color(1f, 0.35f, 0.35f);
        private static readonly Color LockColor = new Color(1f, 0.62f, 0.2f); // 주황 = 잠금 (공통 UI 색 규칙)
        private static readonly Color SimColor = new Color(1f, 0.45f, 0.75f); // 시뮬레이터 안내 글자 (일반 뽑기 시뮬레이터와 같은 분홍색)

        private class SlotView
        {
            public GameObject root;
            public Outline outline;
            public GameObject back;
            public GameObject face;
            public Text rarityText;
            public Text nameText;
            public Text tagText;
            public Button lockButton;
            public Text lockLabel;
        }

        private readonly List<SlotView> _slots = new List<SlotView>();

        // 지금 화면이 보여주고 있는 판. 실제 판(_realSession)이거나 시뮬레이터 판(_simSession) 중 하나다.
        private LockOnSession _session;
        private LockOnSession _realSession;   // 실제 코어를 쓰고 보유 드론에 반영되는 판
        private LockOnSession _simSession;    // 시뮬레이터 판: 코어·보유 드론·저장 데이터·테스트 로그와 무관 (처음 켤 때 만든다)
        private bool _simMode;

        // 시뮬레이터 전환 버튼 / 안내 글자 / 누적 초기화 버튼. (시뮬레이터 UI가 없는 옛 씬이면 조용히 건너뛴다)
        private Button _simButton, _simResetButton;
        private Text _simLabel, _simInfoText;

        private Text _coreText;
        private Text _infoText;
        private Text _messageText;

        private Button _startButton, _rerollButton, _confirmButton, _againButton, _closeButton;
        private Text _startLabel, _rerollLabel, _confirmLabel, _againLabel;
        private Color _startNormalColor, _rerollNormalColor, _againNormalColor;

        private GameObject _ratesPopup;
        private Text _ratesText, _rulesText;
        private GameObject _confirmPopup;
        private Text _confirmMessage;
        private Action _onConfirmYes;

        // 뽑기 화면(GachaUI)이 "락온 뽑기" 버튼을 보여줄지 정할 때 읽는다. 패널이 꺼져 있어도(Awake 전이어도) 읽을 수 있다.
        public bool IsEnabled => _table != null && _table.Enabled;

        public void Open() => gameObject.SetActive(true);

        private void Awake()
        {
            WireButton("BtnBack", RequestClose);
            WireButton("BtnRates", OpenRates);
            _coreText = FindText("CoreText");
            _infoText = FindText("InfoText");
            _messageText = FindText("MessageText");

            var subtitle = FindText("SubtitleText");
            if (subtitle != null && _table != null) subtitle.text = LockOnRateFormatter.BuildSubtitle(_table);

            // 칸 카드: Slot_0, Slot_1, ... 이름이 있는 만큼 연결한다.
            for (int i = 0; ; i++)
            {
                var slot = transform.Find($"SlotContainer/Slot_{i}");
                if (slot == null) break;

                var view = new SlotView
                {
                    root = slot.gameObject,
                    outline = slot.GetComponent<Outline>(),
                    back = slot.Find("Back").gameObject,
                    face = slot.Find("Face").gameObject,
                    rarityText = slot.Find("Face/RarityText").GetComponent<Text>(),
                    nameText = slot.Find("Face/DroneNameText").GetComponent<Text>(),
                    tagText = slot.Find("Face/TagText").GetComponent<Text>(),
                };
                var lockTransform = slot.Find("BtnLock");
                view.lockButton = lockTransform != null ? lockTransform.GetComponent<Button>() : null;
                view.lockLabel = lockTransform != null ? lockTransform.Find("Text").GetComponent<Text>() : null;
                if (view.lockButton != null)
                {
                    int index = i; // 반복문 변수를 그대로 쓰면 모든 버튼이 마지막 번호를 보게 되므로 복사해 둔다
                    view.lockButton.onClick.AddListener(() => OnLockClicked(index));
                }
                _slots.Add(view);
            }
            if (_slots.Count == 0) Debug.LogWarning("[LockOn] 락온 화면에서 'SlotContainer/Slot_0' 카드를 찾지 못했습니다.");

            _startButton = WireButton("BtnStart", OnStart);
            _rerollButton = WireButton("BtnReroll", OnReroll);
            _confirmButton = WireButton("BtnConfirm", OnConfirmClicked);
            _againButton = WireButton("BtnAgain", OnAgain);
            _closeButton = WireButton("BtnClose", RequestClose);
            _startLabel = ButtonLabel(_startButton);
            _rerollLabel = ButtonLabel(_rerollButton);
            _confirmLabel = ButtonLabel(_confirmButton);
            _againLabel = ButtonLabel(_againButton);
            if (_startLabel != null) _startNormalColor = _startLabel.color;
            if (_rerollLabel != null) _rerollNormalColor = _rerollLabel.color;
            if (_againLabel != null) _againNormalColor = _againLabel.color;

            _simButton = WireButton("BtnSim", ToggleSim, false);
            _simLabel = ButtonLabel(_simButton);
            _simResetButton = WireButton("BtnSimReset", ResetSimStats, false);
            var simInfo = transform.Find("SimInfoText");
            _simInfoText = simInfo != null ? simInfo.GetComponent<Text>() : null;

            // 팝업 두 개: 확률·규칙 공개, 확인창.
            var rates = transform.Find("RatesPopup");
            if (rates != null)
            {
                _ratesPopup = rates.gameObject;
                _ratesText = FindText("RatesPopup/Box/RatesText");
                _rulesText = FindText("RatesPopup/Box/RulesText");
                var close = rates.Find("Box/BtnClose");
                var closeButton = close != null ? close.GetComponent<Button>() : null;
                if (closeButton != null) closeButton.onClick.AddListener(() => { PlayClick(); _ratesPopup.SetActive(false); });
                _ratesPopup.SetActive(false);
            }
            else Debug.LogWarning("[LockOn] 락온 화면에서 'RatesPopup'을 찾지 못했습니다.");

            var confirm = transform.Find("ConfirmPopup");
            if (confirm != null)
            {
                _confirmPopup = confirm.gameObject;
                _confirmMessage = FindText("ConfirmPopup/Box/MessageText");
                var yes = confirm.Find("Box/BtnYes");
                var no = confirm.Find("Box/BtnNo");
                var yesButton = yes != null ? yes.GetComponent<Button>() : null;
                var noButton = no != null ? no.GetComponent<Button>() : null;
                if (yesButton != null) yesButton.onClick.AddListener(() =>
                {
                    PlayClick();
                    _confirmPopup.SetActive(false);
                    var action = _onConfirmYes;
                    _onConfirmYes = null;
                    action?.Invoke();
                });
                if (noButton != null) noButton.onClick.AddListener(() => { PlayClick(); _onConfirmYes = null; _confirmPopup.SetActive(false); });
                _confirmPopup.SetActive(false);
            }
            else Debug.LogWarning("[LockOn] 락온 화면에서 'ConfirmPopup'을 찾지 못했습니다.");
        }

        private void OnEnable()
        {
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.OnCoreChanged += HandleCoreChanged;

            EnsureSession();
            // 화면을 열 때마다 시뮬레이터는 꺼진 상태로 시작한다. (일반 뽑기 시뮬레이터와 같은 방식)
            _simMode = false;
            _session = _realSession;
            // 지난번에 끝낸 판이 남아 있으면 치우고 처음 상태로 연다.
            if (_realSession != null && _realSession.Phase == LockOnPhase.Done) _realSession.Reset();
            if (_simSession != null)
            {
                if (_simSession.Phase == LockOnPhase.Done) _simSession.Reset();
                else _simSession.AbandonSimulation();
            }
            _messageText?.gameObject.SetActive(false);
            Refresh();
        }

        private void OnDisable()
        {
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.OnCoreChanged -= HandleCoreChanged;
        }

        private void HandleCoreChanged(int value) => Refresh();

        // 화면이 열릴 때 한 번 판 관리자를 만든다. 재화·보유 드론 매니저가 없으면(메인 메뉴를 거치지 않고 시작) 경고만 남긴다.
        private void EnsureSession()
        {
            if (_realSession != null) return;
            var currency = CurrencyManager.Instance;
            var inventory = DroneInventory.Instance;
            var data = SaveManager.Data;
            if (_table == null || currency == null || inventory == null || data == null)
            {
                Debug.LogWarning("[LockOn] LockOnTable, CurrencyManager, DroneInventory 중 준비되지 않은 것이 있어 락온 뽑기를 사용할 수 없습니다. 메인 메뉴 씬부터 시작했는지 확인해주세요.");
                return;
            }
            _realSession = new LockOnSession(_table, currency, inventory, data, new System.Random(), true);
            _realSession.Changed += Refresh;
        }

        // ---------------- 시뮬레이터 ----------------
        // 시뮬레이터를 켜고 끈다. 켜면 코어를 쓰지 않고 보유 드론·저장 데이터·테스트 로그에 아무것도 남기지 않는 가상의 판으로 연습할 수 있다.
        // 진행 중인 판이 있을 때는 바꿀 수 없다(버튼이 비활성). 처음 켤 때 시뮬레이터 판을 만들고, 껐다 켜도 같은 판(누적 숫자)을 이어서 쓴다.
        private void ToggleSim()
        {
            if (_session != null && _session.Phase == LockOnPhase.Active) return;
            PlayClick();
            ClearMessage();
            if (_session != null && _session.Phase == LockOnPhase.Done) _session.Reset();

            _simMode = !_simMode;
            if (_simMode)
            {
                if (_simSession == null && _table != null)
                {
                    _simSession = new LockOnSession(_table, new System.Random());
                    _simSession.Changed += Refresh;
                }
                _session = _simSession;
            }
            else _session = _realSession;
            Refresh();
        }

        // 시뮬레이터의 누적 숫자를 0으로 되돌린다.
        private void ResetSimStats()
        {
            if (_simSession == null) return;
            PlayClick();
            _simSession.ResetStats(); // Changed 이벤트로 화면이 다시 그려진다
        }

        // 시뮬레이터 전환 버튼·안내 글자·초기화 버튼을 지금 상태에 맞게 보이고 숨긴다.
        private void RefreshSimUI()
        {
            bool sim = _simMode && _session != null && _session.IsSimulation;
            bool busy = _session != null && _session.Phase == LockOnPhase.Active;

            if (_simButton != null) _simButton.interactable = !busy; // 진행 중인 판이 있으면 바꿀 수 없다
            if (_simLabel != null) _simLabel.text = sim ? "시뮬레이터: 켜짐" : "시뮬레이터: 꺼짐";
            if (_simInfoText != null)
            {
                _simInfoText.gameObject.SetActive(sim);
                if (sim)
                {
                    string average = _session.ConfirmedCount > 0 ? $"{(float)_session.TotalSpentCore / _session.ConfirmedCount:N0}" : "-";
                    _simInfoText.text = $"SIMULATION   누적 {_session.ConfirmedCount}판  ·  판당 평균 사용 코어 {average}  ·  받은 칸 {_session.TotalReceivedCount}개   (코어·보유 드론·로그에 영향 없음)";
                }
            }
            if (_simResetButton != null) _simResetButton.gameObject.SetActive(sim && !busy);
        }

        // ---------------- 그리기 ----------------
        private void Refresh()
        {
            var currency = CurrencyManager.Instance;
            if (_coreText != null) _coreText.text = $"코어  {(currency != null ? currency.Core : 0):N0}";

            if (_session == null || !_session.IsReady)
            {
                if (_infoText != null) _infoText.text = "락온 뽑기를 사용할 수 없습니다 (메인 메뉴부터 시작했는지 확인해주세요)";
                SetActive(_startButton, false);
                SetActive(_rerollButton, false);
                SetActive(_confirmButton, false);
                SetActive(_againButton, false);
                SetActive(_closeButton, false);
                RefreshSlots();
                RefreshSimUI(); // 실제 락온은 못 쓰는 상태여도 시뮬레이터는 켤 수 있다
                return;
            }

            var phase = _session.Phase;
            RefreshSlots();

            // 안내 글자
            if (_infoText != null)
            {
                switch (phase)
                {
                    case LockOnPhase.Idle:
                        _infoText.text = _session.IsSimulation
                            ? $"시뮬레이터: 코어를 쓰지 않아요  ·  확률과 가격 규칙은 실제와 같아요 (처음 공개 {_session.FirstCost:N0}코어로 계산)"
                            : $"처음 공개 {_session.FirstCost:N0}코어  ·  공개된 칸 중 {_table.LockMinRarity} 이상을 잠그고 나머지만 다시 뽑을 수 있어요";
                        break;
                    case LockOnPhase.Active:
                        _infoText.text = $"{(_session.IsSimulation ? "이번 판에서 가상으로 쓴 코어" : "이번 판에서 쓴 코어")} {_session.SpentCore:N0}  ·  재뽑기 {_session.RerollCount}번  ·  잠근 칸 {_session.LockedCount} / {_session.SlotCount}";
                        break;
                    default:
                        _infoText.text = $"{(_session.IsSimulation ? "이번 판에서 가상으로 쓴 코어" : "이번 판에서 쓴 코어")} {_session.SpentCore:N0}  (재뽑기 {_session.RerollCount}번)";
                        break;
                }
            }

            // 버튼: 단계에 맞는 것만 보인다. 코어가 모자라면 글자만 빨갛게 (눌러서 "부족" 안내를 볼 수 있게 켜 둔다).
            SetActive(_startButton, phase == LockOnPhase.Idle);
            SetActive(_rerollButton, phase == LockOnPhase.Active);
            SetActive(_confirmButton, phase == LockOnPhase.Active);
            SetActive(_againButton, phase == LockOnPhase.Done);
            SetActive(_closeButton, phase == LockOnPhase.Done);

            // 시뮬레이터에서는 코어를 쓰지 않으므로 "무료 (시뮬레이션)"으로 보여준다. (재뽑기는 가상 비용을 알려준다)
            bool sim = _session.IsSimulation;
            SetLabel(_startLabel, sim ? "락온 시작\n무료 (시뮬레이션)" : $"락온 시작\n{_session.FirstCost:N0} 코어", _session.CanAffordStart ? _startNormalColor : ShortColor);
            SetLabel(_againLabel, sim ? "한 번 더\n무료 (시뮬레이션)" : $"한 번 더\n{_session.FirstCost:N0} 코어", _session.CanAffordStart ? _againNormalColor : ShortColor);

            if (phase == LockOnPhase.Active)
            {
                bool canReroll = _session.UnlockedCount > 0;
                string rerollPrice = sim ? $"{_session.NextRerollCost:N0} 코어 (가상)" : $"{_session.NextRerollCost:N0} 코어";
                SetLabel(_rerollLabel,
                         canReroll ? $"재뽑기  ({_session.UnlockedCount}칸)\n{rerollPrice}" : "모두 잠금\n다시 뽑을 칸 없음",
                         canReroll && !_session.CanAffordReroll ? ShortColor : _rerollNormalColor);
                if (_rerollButton != null) _rerollButton.interactable = canReroll;
                SetLabel(_confirmLabel, $"확정\n잠근 {_session.LockedCount}칸 받기", DarkColor);
            }

            RefreshSimUI();
        }

        private static readonly Color DarkColor = new Color(0.043f, 0.055f, 0.078f); // 시안 버튼 위의 어두운 글자

        private void RefreshSlots()
        {
            int count = _session != null && _session.IsReady ? _session.SlotCount : 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                var view = _slots[i];
                view.root.SetActive(i < count);
                if (i >= count) continue;

                switch (_session.Phase)
                {
                    case LockOnPhase.Active: ShowActiveSlot(view, i); break;
                    case LockOnPhase.Done: ShowDoneSlot(view, i); break;
                    default: ShowBackSlot(view); break;
                }
            }
        }

        // 시작 전: 뒷면.
        private void ShowBackSlot(SlotView view)
        {
            view.back.SetActive(true);
            view.face.SetActive(false);
            if (view.lockButton != null) view.lockButton.gameObject.SetActive(false);
            SetOutline(view, BackOutlineColor, 1f);
        }

        // 진행 중: 공개된 칸. 잠글 수 있는 칸에는 잠금 버튼.
        private void ShowActiveSlot(SlotView view, int index)
        {
            var slot = _session.Slots[index];
            Color rarityColor = RarityColors.Get(slot.rarity);
            view.back.SetActive(false);
            view.face.SetActive(true);
            view.rarityText.text = slot.rarity.ToString();
            view.rarityText.color = rarityColor;
            view.nameText.text = DroneNames.Get(slot.drone);

            bool lockable = _session.CanLock(index);
            if (slot.locked)
            {
                view.tagText.text = "잠김";
                view.tagText.color = LockColor;
                SetOutline(view, LockColor, 4f);
            }
            else
            {
                view.tagText.text = lockable ? "잠글 수 있음" : "재뽑기 대상";
                view.tagText.color = MutedColor;
                SetOutline(view, rarityColor, 2f);
            }

            if (view.lockButton != null)
            {
                view.lockButton.gameObject.SetActive(lockable);
                if (view.lockLabel != null) view.lockLabel.text = slot.locked ? "잠금 해제" : "잠금";
            }
        }

        // 확정 후: 받은 칸은 NEW / 승급 / 중복 조각, 못 받은 칸은 환산된 조각을 알려준다.
        private void ShowDoneSlot(SlotView view, int index)
        {
            var result = _session.LastReport.slots[index];
            Color rarityColor = RarityColors.Get(result.rarity);
            view.back.SetActive(false);
            view.face.SetActive(true);
            view.rarityText.text = result.rarity.ToString();
            view.rarityText.color = rarityColor;
            view.nameText.text = DroneNames.Get(result.drone);
            if (view.lockButton != null) view.lockButton.gameObject.SetActive(false);

            if (result.received)
            {
                switch (result.outcome.outcome)
                {
                    case PullOutcome.New:
                        view.tagText.text = "NEW";
                        view.tagText.color = CyanColor;
                        break;
                    case PullOutcome.Promoted:
                        view.tagText.text = $"승급  {result.outcome.previousRarity} → {result.rarity}";
                        view.tagText.color = CyanColor;
                        break;
                    case PullOutcome.Simulated:
                        // 시뮬레이터: 보유 목록과 무관하므로 NEW/중복 대신 "시뮬레이션"만 보여준다.
                        view.tagText.text = "받음 (시뮬레이션)";
                        view.tagText.color = SimColor;
                        break;
                    default:
                        view.tagText.text = $"중복  조각 +{result.outcome.shardsGained}";
                        view.tagText.color = MutedColor;
                        break;
                }
                SetOutline(view, rarityColor, 3f);
            }
            else
            {
                view.tagText.text = result.convertedShards > 0
                    ? $"받지 않음\n조각 +{result.convertedShards} → {DroneNames.Get(result.convertedTarget)}"
                    : "받지 않음";
                view.tagText.color = MutedColor;
                SetOutline(view, BackOutlineColor, 1f);
            }
        }

        // ---------------- 버튼 ----------------
        private void OnStart()
        {
            if (_session == null) return;
            PlayClick();
            ClearMessage();
            if (_session.Start() == LockOnFailure.InsufficientCore) ShowMessage("코어가 부족합니다");
            Refresh();
        }

        private void OnLockClicked(int index)
        {
            if (_session == null) return;
            PlayClick();
            ClearMessage();
            _session.SetLocked(index, !_session.Slots[index].locked); // 바뀌면 Changed 이벤트로 화면이 다시 그려진다
        }

        private void OnReroll()
        {
            if (_session == null) return;
            PlayClick();
            ClearMessage();
            if (_session.Reroll() == LockOnFailure.InsufficientCore) ShowMessage("코어가 부족합니다");
            Refresh();
        }

        // "확정": 잠근 칸이 하나도 없으면 먼저 확인창을 띄운다.
        private void OnConfirmClicked()
        {
            if (_session == null || _session.Phase != LockOnPhase.Active) return;
            PlayClick();
            ClearMessage();
            if (_session.LockedCount == 0)
            {
                string message = _session.IsSimulation
                    ? "잠근 칸이 없습니다.\n이대로 확정하면 아무것도 받지 못합니다.\n(시뮬레이션이라 코어는 쓰지 않아요)\n\n확정할까요?"
                    : $"잠근 칸이 없습니다.\n이대로 확정하면 드론을 하나도 받지 못하고\n쓴 코어 {_session.SpentCore:N0}는 돌려받지 못합니다.\n(모든 칸이 조각으로 바뀝니다)\n\n확정할까요?";
                ShowConfirm(message, () => DoConfirm("확정"));
            }
            else DoConfirm("확정");
        }

        private void DoConfirm(string reason)
        {
            if (_session == null) return;
            _session.Confirm(reason);
            Refresh();
        }

        // "한 번 더": 끝난 판을 치우고 바로 새 판을 시작한다.
        private void OnAgain()
        {
            if (_session == null) return;
            _session.Reset();
            OnStart();
        }

        // 뒤로 / 나가기. 진행 중인 판이 있으면 확정하고 나갈지 먼저 묻는다.
        private void RequestClose()
        {
            PlayClick();
            // 시뮬레이터 판은 코어를 쓰지 않았으니 묻지 않고 그냥 버리고 나간다.
            if (_session != null && _session.IsSimulation && _session.Phase == LockOnPhase.Active) _session.AbandonSimulation();
            if (_session != null && _session.Phase == LockOnPhase.Active)
            {
                ShowConfirm($"진행 중인 락온 뽑기가 있습니다.\n지금 확정하고 나갈까요?\n(잠근 {_session.LockedCount}칸만 받고, 쓴 코어는 돌려받지 못합니다)",
                            () => { DoConfirm("나가기"); gameObject.SetActive(false); });
                return;
            }
            gameObject.SetActive(false);
        }

        private void OpenRates()
        {
            PlayClick();
            if (_ratesPopup == null || _table == null) return;
            if (_ratesText != null) _ratesText.text = LockOnRateFormatter.BuildRatesRichText(_table);
            if (_rulesText != null) _rulesText.text = LockOnRateFormatter.BuildRulesText(_table);
            _ratesPopup.SetActive(true);
        }

        // ---------------- 도우미 ----------------
        private void ShowConfirm(string message, Action onYes)
        {
            if (_confirmPopup == null)
            {
                onYes?.Invoke(); // 확인창이 없는 옛 씬이면 바로 진행한다
                return;
            }
            if (_confirmMessage != null) _confirmMessage.text = message;
            _onConfirmYes = onYes;
            _confirmPopup.SetActive(true);
        }

        private void ShowMessage(string text)
        {
            if (_messageText == null) return;
            _messageText.text = text;
            _messageText.gameObject.SetActive(true);
        }

        private void ClearMessage()
        {
            if (_messageText != null) _messageText.gameObject.SetActive(false);
        }

        private static void SetActive(Button button, bool active)
        {
            if (button != null) button.gameObject.SetActive(active);
        }

        private static void SetLabel(Text label, string text, Color color)
        {
            if (label == null) return;
            label.text = text;
            label.color = color;
        }

        private static void SetOutline(SlotView view, Color color, float distance)
        {
            if (view.outline == null) return;
            view.outline.effectColor = color;
            view.outline.effectDistance = new Vector2(distance, distance);
        }

        private static Text ButtonLabel(Button button)
        {
            if (button == null) return null;
            var label = button.transform.Find("Text");
            return label != null ? label.GetComponent<Text>() : null;
        }

        private void PlayClick() => AudioManager.Instance?.PlaySfx(clickSound);

        private Text FindText(string path)
        {
            var child = transform.Find(path);
            var text = child != null ? child.GetComponent<Text>() : null;
            if (text == null) Debug.LogWarning($"[LockOn] 락온 화면에서 '{path}' 글자 칸을 찾지 못했습니다.");
            return text;
        }

        // warnIfMissing을 false로 주면 버튼이 없어도 경고하지 않는다 (시뮬레이터 UI가 없는 옛 씬용).
        private Button WireButton(string path, UnityEngine.Events.UnityAction action, bool warnIfMissing = true)
        {
            var child = transform.Find(path);
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
            {
                if (warnIfMissing) Debug.LogWarning($"[LockOn] 락온 화면에서 '{path}' 버튼을 찾지 못했습니다.");
                return null;
            }
            button.onClick.AddListener(action);
            return button;
        }
    }
}
