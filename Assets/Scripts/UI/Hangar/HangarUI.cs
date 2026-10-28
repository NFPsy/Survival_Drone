using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SurvivalDrone.Core;
using SurvivalDrone.Drones;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 격납고 화면. 가진 드론을 보고, 강화하고, 출격할 드론을 장착하는 화면이다.
    //   왼쪽: 드론 5종 목록 (등급·레벨, 장착 중 표시, 미획득은 흐리게)
    //   오른쪽: 고른 드론의 상세 (전투력, 성능 배율, 조각 진행바, 장착 버튼, 강화 버튼, 크레딧으로 조각을 사는 교환 버튼)
    //   아래: 출격 장착 슬롯 2개와 내 전투력
    //
    // 장착하는 방법은 두 가지다.
    //   1) 상세 화면의 "장착" 버튼: 슬롯 1·2 중 어디에 장착할지 고르는 창이 뜬다. 고른 슬롯에 있던 드론과 자리가 바뀐다.
    //      같은 드론이 두 슬롯에 들어가지는 않는다 (이미 다른 슬롯에 있는 드론이면 두 슬롯의 드론이 서로 바뀐다).
    //   2) 아래 슬롯 버튼: 원하는 슬롯을 직접 눌러 그 자리에 넣는다. 이미 그 자리에 있는 드론이면 장착이 해제된다.
    //
    // 실제 규칙(보유·강화·장착·전투력 계산)은 DroneInventory가 처리하고, 이 스크립트는 버튼 입력을 넘기고 결과를 화면에 그리기만 한다.
    // MainMenuController처럼 정해진 이름의 자식 오브젝트를 찾아서 연결하며, 못 찾아도 경고만 남기고 나머지는 계속 동작한다.
    //
    // 아직 없는 것: 3D 프리뷰. 그리고 장착한 드론을 실제 InGame 판에 반영하는 것 (지금 InGame은 여전히 근접 드론 하나로 시작한다).
    public class HangarUI : MonoBehaviour
    {
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private AudioClip clickSound;

        private static readonly Color CyanColor = new Color(0.310f, 0.847f, 0.910f);
        private static readonly Color LightColor = new Color(0.890f, 0.961f, 0.961f);
        private static readonly Color MutedColor = new Color(0.588f, 0.706f, 0.714f);
        private static readonly Color DeadColor = new Color(0.35f, 0.40f, 0.45f);
        private static readonly Color ShortColor = new Color(1f, 0.35f, 0.35f);
        private static readonly Color NormalOutline = new Color(0.300f, 0.360f, 0.460f, 0.9f);

        private class Row
        {
            public DroneType type;
            public Outline outline;
            public Text nameText;
            public Text tagText;
            public Text infoText;
            public Image icon; // 줄 왼쪽의 드론 종류 아이콘 (견본에 "Icon"이 없으면 null)
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly List<Button> _slotButtons = new List<Button>();
        private readonly List<Text> _slotTexts = new List<Text>();
        private readonly List<Outline> _slotOutlines = new List<Outline>();

        private Text _creditText;
        private Text _coreText;
        private Text _nameText;
        private Text _rarityLevelText;
        private Text _powerText;
        private Text _multiplierText;
        private Text _shardsText;
        private RectTransform _shardBarFill;
        private Text _costText;
        private Image _nameIcon;  // 상세 패널 제목(드론 이름) 왼쪽의 종류 아이콘
        private Image _costIcon;  // "크레딧 500"처럼 크레딧이 드는 줄에서만 보이는 크레딧 아이콘
        private Button _equipButton;  // 상세 화면의 "장착" 버튼 (누르면 슬롯 고르는 창이 뜬다)
        private GameObject _equipPopup;  // 슬롯 1·2 중 어디에 장착할지 고르는 창 (평소엔 꺼 둔다)
        private Text _equipPopupTitle;
        private readonly List<Button> _popupSlotButtons = new List<Button>();
        private readonly List<Text> _popupSlotTexts = new List<Text>();
        private Button _upgradeButton;
        private Text _upgradeLabel;
        private Button _exchangeButton;
        private Text _exchangeLabel;
        private Text _statusText;
        private Text _totalPowerText;

        private DroneType _selected = DroneType.Melee;
        private bool _built;

        private void Awake()
        {
            _creditText = FindText("TopBar/CreditText");
            _coreText = FindText("TopBar/CoreText");
            _nameText = FindText("DetailPanel/NameText");
            _rarityLevelText = FindText("DetailPanel/RarityLevelText");
            _powerText = FindText("DetailPanel/PowerText");
            _multiplierText = FindText("DetailPanel/MultiplierText");
            _shardsText = FindText("DetailPanel/ShardsText");
            _costText = FindText("DetailPanel/CostText");
            _statusText = FindText("DetailPanel/StatusText");
            _totalPowerText = FindText("TotalPowerText");
            _upgradeLabel = FindText("DetailPanel/BtnUpgrade/Text");

            // 아이콘은 없어도(씬에 아직 안 붙였어도) 예전처럼 동작하므로 못 찾아도 경고하지 않는다.
            var nameIcon = transform.Find("DetailPanel/NameText/Icon");
            _nameIcon = nameIcon != null ? nameIcon.GetComponent<Image>() : null;
            var costIcon = transform.Find("DetailPanel/CostText/Icon");
            _costIcon = costIcon != null ? costIcon.GetComponent<Image>() : null;

            var fill = transform.Find("DetailPanel/ShardBar/Fill");
            _shardBarFill = fill != null ? fill.GetComponent<RectTransform>() : null;
            if (_shardBarFill == null) Debug.LogWarning("[Inventory] 격납고에서 'DetailPanel/ShardBar/Fill'을 찾지 못했습니다.");

            WireButton("BtnBack", () => { PlayClick(); SceneManager.LoadScene(lobbySceneName); });
            _equipButton = WireButton("DetailPanel/BtnEquip", OpenEquipPopup);
            _upgradeButton = WireButton("DetailPanel/BtnUpgrade", Upgrade);
            _exchangeButton = WireButton("DetailPanel/BtnExchange", Exchange);
            _exchangeLabel = FindText("DetailPanel/BtnExchange/Text");

            // 장착 슬롯 버튼: 슬롯 번호는 0부터 (BtnSlot1 = 0번)
            int slotCount = DroneInventory.Instance != null ? DroneInventory.Instance.EquipSlotCount : 2;
            for (int i = 0; i < slotCount; i++)
            {
                int slot = i; // 반복문 변수가 계속 바뀌는 문제를 피하려고 복사해서 클릭 함수에 넘긴다
                var button = WireButton($"BtnSlot{i + 1}", () => OnSlotClicked(slot));
                if (button == null) continue;
                _slotButtons.Add(button);
                _slotTexts.Add(button.transform.Find("Text").GetComponent<Text>());
                _slotOutlines.Add(button.GetComponent<Outline>());
            }

            // 슬롯 고르는 창: 씬에 아직 없어도 나머지 화면은 그대로 동작하도록 못 찾으면 경고만 남긴다.
            var popup = transform.Find("EquipPopup");
            if (popup != null)
            {
                _equipPopup = popup.gameObject;
                _equipPopupTitle = FindText("EquipPopup/Box/TitleText");
                for (int i = 0; i < slotCount; i++)
                {
                    int slot = i;
                    var button = WireButton($"EquipPopup/Box/BtnPopupSlot{i + 1}", () => EquipToSlot(slot));
                    if (button == null) continue;
                    _popupSlotButtons.Add(button);
                    _popupSlotTexts.Add(button.transform.Find("Text").GetComponent<Text>());
                }
                WireButton("EquipPopup/Box/BtnCancel", CloseEquipPopup);
                _equipPopup.SetActive(false);
            }
            else Debug.LogWarning("[Inventory] 격납고에서 'EquipPopup'을 찾지 못했습니다.");
        }

        private void OnEnable()
        {
            if (DroneInventory.Instance != null) DroneInventory.Instance.OnInventoryChanged += Refresh;
            else Debug.LogWarning("[Inventory] 격납고에서 DroneInventory를 찾지 못했습니다. 메인 메뉴 씬부터 시작했는지 확인해주세요.");

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.OnCreditChanged += HandleCurrencyChanged;
                CurrencyManager.Instance.OnCoreChanged += HandleCurrencyChanged;
            }

            BuildRowsIfNeeded();

            // 처음 열 때는 첫 번째 슬롯에 장착된 드론을 골라 둔다 (없으면 근접 드론).
            var first = DroneInventory.Instance != null ? DroneInventory.Instance.GetEquipped(0) : null;
            _selected = first ?? DroneType.Melee;
            if (_equipPopup != null) _equipPopup.SetActive(false);
            SetStatus("", CyanColor);
            Refresh();
        }

        private void OnDisable()
        {
            if (DroneInventory.Instance != null) DroneInventory.Instance.OnInventoryChanged -= Refresh;
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.OnCreditChanged -= HandleCurrencyChanged;
                CurrencyManager.Instance.OnCoreChanged -= HandleCurrencyChanged;
            }
        }

        private void HandleCurrencyChanged(int value) => Refresh();

        // ---------------- 목록 만들기 ----------------
        // 드론 종류 개수만큼 견본(RowTemplate)을 복제해서 목록 줄을 만든다.
        private void BuildRowsIfNeeded()
        {
            if (_built) return;

            var container = transform.Find("ListPanel/RowContainer");
            var template = transform.Find("ListPanel/RowTemplate");
            if (container == null || template == null)
            {
                Debug.LogWarning("[Inventory] 격납고에서 ListPanel/RowContainer 또는 RowTemplate을 찾지 못했습니다.");
                return;
            }

            foreach (DroneType type in Enum.GetValues(typeof(DroneType)))
            {
                DroneType rowType = type; // 클릭 함수에 넘길 복사본
                var rowObject = Instantiate(template.gameObject, container);
                rowObject.name = $"Row_{type}";
                rowObject.SetActive(true);

                var row = new Row
                {
                    type = type,
                    outline = rowObject.GetComponent<Outline>(),
                    nameText = rowObject.transform.Find("NameText").GetComponent<Text>(),
                    tagText = rowObject.transform.Find("TagText").GetComponent<Text>(),
                    infoText = rowObject.transform.Find("InfoText").GetComponent<Text>(),
                };
                var iconTransform = rowObject.transform.Find("Icon");
                if (iconTransform != null)
                {
                    row.icon = iconTransform.GetComponent<Image>();
                    UiIcons.SetDroneIcon(row.icon, type);
                }
                rowObject.GetComponent<Button>().onClick.AddListener(() => OnRowClicked(rowType));
                _rows.Add(row);
            }
            _built = true;
        }

        // ---------------- 화면 갱신 ----------------
        private void Refresh()
        {
            var inventory = DroneInventory.Instance;
            var currency = CurrencyManager.Instance;
            if (inventory == null) return;

            if (_creditText != null) _creditText.text = $"크레딧  {(currency != null ? currency.Credit : 0):N0}";
            if (_coreText != null) _coreText.text = $"코어  {(currency != null ? currency.Core : 0):N0}";
            if (_totalPowerText != null) _totalPowerText.text = $"내 전투력  {inventory.TotalCombatPower:N0}";

            RefreshRows(inventory);
            RefreshDetail(inventory, currency);
            RefreshEquipButton(inventory);
            RefreshSlots(inventory);
        }

        // 장착 버튼의 켜짐 상태. 보유한 드론이면 켜 두고, 미보유 드론은 장착할 수 없으니 끈다.
        private void RefreshEquipButton(DroneInventory inventory)
        {
            if (_equipButton == null) return;
            _equipButton.interactable = inventory.IsOwned(_selected);
        }

        private void RefreshRows(DroneInventory inventory)
        {
            foreach (var row in _rows)
            {
                bool owned = inventory.TryGetInfo(row.type, out var info);

                row.nameText.text = DroneNames.Get(row.type);
                row.nameText.color = owned ? LightColor : DeadColor;
                if (row.icon != null) row.icon.color = owned ? UiIcons.DroneColor(row.type) : DeadColor; // 미획득은 흐리게

                if (owned)
                {
                    row.infoText.text = $"{info.rarity}   Lv{info.level}";
                    row.infoText.color = RarityColors.Get(info.rarity);
                }
                else
                {
                    row.infoText.text = "미획득";
                    row.infoText.color = DeadColor;
                }

                int slot = FindEquippedSlot(inventory, row.type);
                row.tagText.text = slot >= 0 ? $"장착 중  (슬롯 {slot + 1})" : "";
                row.tagText.color = CyanColor;

                if (row.outline != null)
                {
                    bool selected = row.type == _selected;
                    row.outline.effectColor = selected ? CyanColor : NormalOutline;
                    row.outline.effectDistance = selected ? new Vector2(3f, 3f) : new Vector2(1f, 1f);
                }
            }
        }

        private void RefreshDetail(DroneInventory inventory, CurrencyManager currency)
        {
            bool owned = inventory.TryGetInfo(_selected, out var info);
            SetText(_nameText, DroneNames.Get(_selected), owned ? LightColor : DeadColor);
            UiIcons.SetDroneIcon(_nameIcon, _selected, owned ? (Color?)null : DeadColor);

            // 크레딧 아이콘은 아래에서 "크레딧  N"을 실제로 보여 줄 때만 켠다. (미획득·최대 레벨일 때는 끈다)
            if (_costIcon != null) _costIcon.gameObject.SetActive(false);

            if (!owned)
            {
                SetText(_rarityLevelText, "미획득", DeadColor);
                SetText(_powerText, "뽑기에서 드론을 얻으면 사용할 수 있습니다", MutedColor);
                SetText(_multiplierText, "", MutedColor);
                SetText(_shardsText, "", MutedColor);
                SetText(_costText, "", MutedColor);
                SetShardBar(0f);
                if (_upgradeButton != null) _upgradeButton.interactable = false;
                if (_upgradeLabel != null) _upgradeLabel.text = "강화";
                RefreshExchange(inventory, false, false);
                return;
            }

            // 각성 단계(Lv6 이상)에 들어선 드론은 레벨 글자 뒤에 "각성"을 붙인다. (각성이 꺼져 있으면 붙을 일이 없다)
            string awakenedMark = inventory.IsAwakeningLevel(info.level) ? "  각성" : "";
            SetText(_rarityLevelText, $"{info.rarity}   Lv{info.level} / {inventory.MaxLevel}{awakenedMark}", RarityColors.Get(info.rarity));
            SetText(_powerText, $"전투력  {Mathf.RoundToInt(inventory.GetPower(_selected))}", LightColor);
            SetText(_multiplierText,
                $"성능 ×{inventory.GetStatMultiplier(_selected):0.00}   (등급 ×{inventory.GetGradeMultiplier(_selected):0.00} · 강화 ×{inventory.GetLevelMultiplier(_selected):0.00})",
                MutedColor);

            int shardCost = inventory.GetUpgradeShardCost(_selected);
            int creditCost = inventory.GetUpgradeCreditCost(_selected);
            bool maxLevel = info.level >= inventory.MaxLevel;

            if (maxLevel)
            {
                SetText(_shardsText, $"조각  {info.shards}", MutedColor);
                SetText(_costText, "최대 레벨입니다", CyanColor);
                SetShardBar(1f);
                if (_upgradeButton != null) _upgradeButton.interactable = false;
                if (_upgradeLabel != null) _upgradeLabel.text = "최대 레벨";
                RefreshExchange(inventory, true, true);
                return;
            }

            SetText(_shardsText, $"조각  {info.shards} / {shardCost}", info.shards >= shardCost ? CyanColor : LightColor);
            SetShardBar(shardCost > 0 ? Mathf.Clamp01((float)info.shards / shardCost) : 0f);

            int credit = currency != null ? currency.Credit : 0;
            SetText(_costText, $"크레딧  {creditCost:N0}", credit >= creditCost ? LightColor : ShortColor);
            if (_costIcon != null) _costIcon.gameObject.SetActive(true);
            if (_upgradeButton != null) _upgradeButton.interactable = true; // 모자라도 눌러서 이유를 안내받을 수 있게 켜 둔다
            // 다음 강화가 각성 단계(예: Lv5 → 6)이면 버튼 글자를 "각성"으로 보여 준다.
            if (_upgradeLabel != null) _upgradeLabel.text = inventory.IsAwakeningLevel(info.level + 1) ? "각성" : "강화";
            RefreshExchange(inventory, true, false);
        }

        // 조각 교환 버튼의 글자와 켜짐 상태. 보유한 드론이고 최대 레벨이 아니면 켜 두고(모자라도 눌러서 이유를 안내받게),
        // 미보유·최대 레벨이면 끈다. 글자에는 받는 조각·내는 크레딧·오늘 남은 횟수를 보여준다.
        private void RefreshExchange(DroneInventory inventory, bool owned, bool maxLevel)
        {
            if (_exchangeButton == null || _exchangeLabel == null) return;

            int used = inventory.GetExchangeCountToday();
            int limit = inventory.ExchangeDailyLimit;
            _exchangeLabel.text = maxLevel
                ? "조각 교환\n최대 레벨은 불가"
                : $"조각 +{inventory.ExchangeShards} 교환\n크레딧 {inventory.ExchangeCredit:N0}  (오늘 {used}/{limit})";
            _exchangeButton.interactable = owned && !maxLevel;
        }

        private void RefreshSlots(DroneInventory inventory)
        {
            for (int slot = 0; slot < _slotButtons.Count; slot++)
            {
                var equipped = inventory.GetEquipped(slot);
                string content = "비어 있음";
                Color textColor = DeadColor;
                if (equipped.HasValue && inventory.TryGetInfo(equipped.Value, out var info))
                {
                    content = $"{DroneNames.Get(equipped.Value)}  {info.rarity}";
                    textColor = LightColor;
                }
                _slotTexts[slot].text = $"슬롯 {slot + 1}\n{content}";
                _slotTexts[slot].color = textColor;

                // 지금 고른 드론이 이 슬롯에 들어 있으면 시안 테두리로 표시
                bool holdsSelected = equipped.HasValue && equipped.Value == _selected;
                if (_slotOutlines[slot] != null)
                {
                    _slotOutlines[slot].effectColor = holdsSelected ? CyanColor : NormalOutline;
                    _slotOutlines[slot].effectDistance = holdsSelected ? new Vector2(3f, 3f) : new Vector2(1f, 1f);
                }
            }
        }

        // ---------------- 입력 처리 ----------------
        private void OnRowClicked(DroneType type)
        {
            PlayClick();
            _selected = type;
            SetStatus("", CyanColor);
            Refresh();
        }

        // 강화 버튼: 결과에 따라 이유를 안내한다.
        private void Upgrade()
        {
            var inventory = DroneInventory.Instance;
            if (inventory == null) return;

            PlayClick();
            switch (inventory.TryUpgrade(_selected, CurrencyManager.Instance))
            {
                case UpgradeResult.Success:
                    inventory.TryGetInfo(_selected, out var info);
                    SetStatus($"강화 완료!  Lv{info.level}", CyanColor);
                    break;
                case UpgradeResult.NotEnoughShards:
                    SetStatus("조각이 부족합니다  (같은 드론을 다시 뽑으면 조각을 얻습니다)", ShortColor);
                    break;
                case UpgradeResult.NotEnoughCredit:
                    SetStatus("크레딧이 부족합니다  (판을 플레이하면 크레딧을 얻습니다)", ShortColor);
                    break;
                case UpgradeResult.MaxLevel:
                    SetStatus("이미 최대 레벨입니다", MutedColor);
                    break;
                default:
                    SetStatus("보유하지 않은 드론입니다", ShortColor);
                    break;
            }
        }

        // 조각 교환 버튼: 크레딧을 내고 고른 드론의 조각을 산다. 결과에 따라 이유를 안내한다.
        private void Exchange()
        {
            var inventory = DroneInventory.Instance;
            if (inventory == null) return;

            PlayClick();
            switch (inventory.TryExchangeShards(_selected, CurrencyManager.Instance))
            {
                case ExchangeResult.Success:
                    SetStatus($"조각 +{inventory.ExchangeShards}을 샀습니다  (오늘 {inventory.GetExchangeCountToday()}/{inventory.ExchangeDailyLimit})", CyanColor);
                    break;
                case ExchangeResult.DailyLimit:
                    SetStatus($"오늘은 더 교환할 수 없습니다  (하루 {inventory.ExchangeDailyLimit}번, 내일 다시 가능)", ShortColor);
                    break;
                case ExchangeResult.NotEnoughCredit:
                    SetStatus("크레딧이 부족합니다  (판을 플레이하면 크레딧을 얻습니다)", ShortColor);
                    break;
                case ExchangeResult.MaxLevel:
                    SetStatus("최대 레벨 드론은 조각이 필요 없습니다", MutedColor);
                    break;
                default:
                    SetStatus("보유하지 않은 드론입니다", ShortColor);
                    break;
            }
        }

        // 장착 버튼: 어느 슬롯에 장착할지 고르는 창을 연다.
        private void OpenEquipPopup()
        {
            var inventory = DroneInventory.Instance;
            if (inventory == null || _equipPopup == null) return;

            PlayClick();
            if (!inventory.IsOwned(_selected))
            {
                SetStatus("보유하지 않은 드론은 장착할 수 없습니다", ShortColor);
                return;
            }

            SetText(_equipPopupTitle, $"{DroneNames.Get(_selected)}을(를) 장착할 슬롯", LightColor);
            for (int slot = 0; slot < _popupSlotButtons.Count; slot++)
            {
                var current = inventory.GetEquipped(slot);
                bool alreadyHere = current.HasValue && current.Value == _selected;

                string content = "비어 있음";
                if (alreadyHere) content = "이미 장착 중";
                else if (current.HasValue && inventory.TryGetInfo(current.Value, out var info)) content = $"{DroneNames.Get(current.Value)}  {info.rarity}";

                _popupSlotTexts[slot].text = $"슬롯 {slot + 1}\n{content}";
                _popupSlotTexts[slot].color = alreadyHere ? DeadColor : LightColor;
                _popupSlotButtons[slot].interactable = !alreadyHere; // 이미 그 자리에 있는 드론이면 고를 필요가 없다
            }
            _equipPopup.SetActive(true);
        }

        private void CloseEquipPopup()
        {
            PlayClick();
            if (_equipPopup != null) _equipPopup.SetActive(false);
        }

        // 창에서 슬롯을 골랐을 때: 그 슬롯에 고른 드론을 넣는다. 원래 그 슬롯에 있던 드론은 밀려난다.
        // 고른 드론이 다른 슬롯에 이미 들어 있으면 DroneInventory가 두 슬롯을 서로 바꿔 주므로 같은 드론이 두 개가 되지 않는다.
        private void EquipToSlot(int slot)
        {
            var inventory = DroneInventory.Instance;
            if (inventory == null) return;

            PlayClick();
            if (_equipPopup != null) _equipPopup.SetActive(false);

            var replaced = inventory.GetEquipped(slot);
            bool movedFromOtherSlot = FindEquippedSlot(inventory, _selected) >= 0;
            if (!inventory.TryEquip(slot, _selected))
            {
                SetStatus("보유하지 않은 드론은 장착할 수 없습니다", ShortColor);
                return;
            }

            string name = DroneNames.Get(_selected);
            if (movedFromOtherSlot)
                SetStatus(replaced.HasValue ? $"{name}와(과) {DroneNames.Get(replaced.Value)}의 자리를 바꿨습니다" : $"{name}을(를) 슬롯 {slot + 1}로 옮겼습니다", CyanColor);
            else
                SetStatus(replaced.HasValue ? $"슬롯 {slot + 1}: {DroneNames.Get(replaced.Value)} → {name} 교체했습니다" : $"{name}을(를) 슬롯 {slot + 1}에 장착했습니다", CyanColor);
        }

        // 슬롯을 누르면: 이 슬롯에 지금 고른 드론을 장착한다. 이미 그 자리에 있는 드론이면 장착을 해제한다.
        private void OnSlotClicked(int slot)
        {
            var inventory = DroneInventory.Instance;
            if (inventory == null) return;

            PlayClick();
            if (!inventory.IsOwned(_selected))
            {
                SetStatus("보유하지 않은 드론은 장착할 수 없습니다", ShortColor);
                return;
            }

            var current = inventory.GetEquipped(slot);
            if (current.HasValue && current.Value == _selected)
            {
                inventory.Unequip(slot);
                SetStatus($"슬롯 {slot + 1} 장착을 해제했습니다", MutedColor);
            }
            else if (inventory.TryEquip(slot, _selected))
            {
                SetStatus($"{DroneNames.Get(_selected)}을(를) 슬롯 {slot + 1}에 장착했습니다", CyanColor);
            }
        }

        // ---------------- 도우미 ----------------
        private static int FindEquippedSlot(DroneInventory inventory, DroneType type)
        {
            for (int slot = 0; slot < inventory.EquipSlotCount; slot++)
            {
                var equipped = inventory.GetEquipped(slot);
                if (equipped.HasValue && equipped.Value == type) return slot;
            }
            return -1;
        }

        private void SetStatus(string message, Color color)
        {
            if (_statusText == null) return;
            _statusText.text = message;
            _statusText.color = color;
        }

        private void SetShardBar(float ratio)
        {
            if (_shardBarFill != null) _shardBarFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }

        private static void SetText(Text target, string value, Color color)
        {
            if (target == null) return;
            target.text = value;
            target.color = color;
        }

        private void PlayClick() => AudioManager.Instance?.PlaySfx(clickSound);

        private Text FindText(string path)
        {
            var child = transform.Find(path);
            var text = child != null ? child.GetComponent<Text>() : null;
            if (text == null) Debug.LogWarning($"[Inventory] 격납고에서 '{path}' 글자 칸을 찾지 못했습니다.");
            return text;
        }

        private Button WireButton(string path, UnityEngine.Events.UnityAction action)
        {
            var child = transform.Find(path);
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogWarning($"[Inventory] 격납고에서 '{path}' 버튼을 찾지 못했습니다.");
                return null;
            }
            button.onClick.AddListener(action);
            return button;
        }
    }
}
