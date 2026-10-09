using System;
using UnityEngine;
using SurvivalDrone.Drones;

namespace SurvivalDrone.Meta
{
    // 드론을 강화하려고 했을 때의 결과. 격납고 화면이 "왜 안 되는지"를 안내할 때 쓴다.
    public enum UpgradeResult { Success, NotOwned, MaxLevel, NotEnoughShards, NotEnoughCredit }

    // 조각 교환(크레딧 → 조각) 결과.
    public enum ExchangeResult { Success, NotOwned, MaxLevel, DailyLimit, NotEnoughCredit }

    // 내가 가진 드론(설계도)을 관리하는 매니저.
    //  - 보유: 드론 종류당 1개만 가진다. 뽑기 결과를 받아서 신규 / 승급 / 중복(조각)으로 처리한다.
    //  - 강화: 조각 + 크레딧을 내고 레벨을 올린다 (최대 5).
    //  - 장착: 출격 슬롯(2개)에 드론을 끼운다.
    //  - 전투력: 장착한 드론의 등급·레벨을 합산한 수치. 로비에서 스테이지 권장 전투력과 비교해 보여준다.
    //
    // CurrencyManager와 같은 방식: 메인 메뉴 씬에 한 번 놓아두면 DontDestroyOnLoad로 씬이 바뀌어도 유지된다.
    //
    // 아직 하지 않는 일: 장착한 드론을 실제 InGame 판에 반영하는 것 (지금 InGame은 근접 드론 하나로 시작한다).
    // 그건 격납고를 만들 때 함께 연결한다.
    public class DroneInventory : MonoBehaviour
    {
        public static DroneInventory Instance { get; private set; }

        // 등급 배율·강화 비용·장착 슬롯 수가 들어있는 데이터 파일, 중복 조각 환산이 들어있는 뽑기 데이터 파일. 인스펙터에서 연결한다.
        [SerializeField] private DroneGrowthTable _growthTable;
        [SerializeField] private GachaTable _gachaTable;

        private SaveData _data;
        private bool _saveOnChange;
        private bool _isReady;

        // 보유 목록·강화·장착이 바뀔 때마다 발생 (격납고·로비 UI가 화면을 다시 그리려고 구독한다).
        public event Action OnInventoryChanged;

        public int EquipSlotCount => _isReady ? _growthTable.EquipSlotCount : 0;
        public int MaxLevel => _isReady ? _growthTable.MaxLevel : 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize(SaveManager.Data, _growthTable, _gachaTable, true);
            SaveManager.DataReplaced += HandleDataReplaced; // 저장 슬롯을 새로 고르면 새 데이터로 다시 연결한다
        }

        private void OnDestroy()
        {
            if (Instance == this) SaveManager.DataReplaced -= HandleDataReplaced;
        }

        private void HandleDataReplaced() => Rebind(SaveManager.Data);

        // 저장 슬롯을 바꿨을 때 새 저장 데이터로 다시 연결한다. (새 데이터가 처음이면 시작 드론을 지급한다)
        public void Rebind(SaveData data) => Initialize(data, _growthTable, _gachaTable, _saveOnChange);

        // 저장 데이터와 수치표를 받아서 준비한다. (검증 도구처럼 씬 없이 쓰고 싶을 때는 saveOnChange를 false로 직접 부른다)
        public void Initialize(SaveData data, DroneGrowthTable growthTable, GachaTable gachaTable, bool saveOnChange)
        {
            _data = data;
            _growthTable = growthTable;
            _gachaTable = gachaTable;
            _saveOnChange = saveOnChange;

            if (_growthTable == null || _gachaTable == null)
            {
                Debug.LogWarning("[Inventory] DroneGrowthTable 또는 GachaTable이 연결되지 않아 드론 보유 시스템을 사용할 수 없습니다.");
                _isReady = false;
                return;
            }
            _isReady = true;

            // 장착 슬롯 칸 수를 수치표에 맞춘다 (모자라면 빈 슬롯(-1)으로 채우고, 남으면 자른다).
            while (_data.equippedDrones.Count < _growthTable.EquipSlotCount) _data.equippedDrones.Add(-1);
            if (_data.equippedDrones.Count > _growthTable.EquipSlotCount)
                _data.equippedDrones.RemoveRange(_growthTable.EquipSlotCount, _data.equippedDrones.Count - _growthTable.EquipSlotCount);

            // 처음 시작하는 저장 데이터라면 시작 드론(근접 N + 저격 N)을 한 번만 지급하고 장착시킨다.
            if (!_data.isInventoryInitialized)
            {
                AddOwned(DroneType.Melee, GachaRarity.N);
                AddOwned(DroneType.Sniper, GachaRarity.N);
                _data.equippedDrones[0] = (int)DroneType.Melee;
                if (_data.equippedDrones.Count > 1) _data.equippedDrones[1] = (int)DroneType.Sniper;
                _data.isInventoryInitialized = true;
                Debug.Log($"[Inventory] 새 데이터 시작 드론 지급: 근접 N, 저격 N (전투력 {TotalCombatPower})");
                SaveIfNeeded();
            }
        }

        // ---------------- 보유 ----------------

        public bool IsOwned(DroneType type) => FindOwned(type) != null;

        public int OwnedCount => _data != null ? _data.ownedDrones.Count : 0;

        // 보유 드론의 정보를 읽는다. 미보유면 false.
        public bool TryGetInfo(DroneType type, out DroneInfo info)
        {
            var owned = FindOwned(type);
            if (owned == null)
            {
                info = default;
                return false;
            }
            info = new DroneInfo(owned.droneType, owned.rarity, owned.level, owned.shards);
            return true;
        }

        // 뽑기 결과 하나를 보유 목록에 반영한다.
        //  - 처음 얻는 드론이면 새로 추가 (New)
        //  - 이미 가진 드론인데 더 높은 등급이면 등급만 올린다. 강화 레벨과 조각은 그대로 (Promoted)
        //  - 같거나 낮은 등급이면 그 등급의 조각 수만큼 조각으로 환산 (Duplicate)
        public InventoryPullOutcome Apply(GachaPullResult pull)
        {
            if (!_isReady)
            {
                Debug.LogWarning("[Inventory] 아직 준비되지 않아 뽑기 결과를 반영하지 못했습니다.");
                return new InventoryPullOutcome(PullOutcome.Duplicate, pull.rarity, 0);
            }

            var owned = FindOwned(pull.drone);
            InventoryPullOutcome outcome;

            if (owned == null)
            {
                AddOwned(pull.drone, pull.rarity);
                outcome = new InventoryPullOutcome(PullOutcome.New, pull.rarity, 0);
                Debug.Log($"[Inventory] 신규 획득: {pull.drone} {pull.rarity}");
            }
            else if (pull.rarity > owned.rarity)
            {
                var previous = owned.rarity;
                owned.rarity = pull.rarity;
                outcome = new InventoryPullOutcome(PullOutcome.Promoted, previous, 0);
                Debug.Log($"[Inventory] 승급: {pull.drone} {previous} → {pull.rarity} (강화 레벨 {owned.level} 유지)");
            }
            else
            {
                int gained = _gachaTable.GetDuplicateShards(pull.rarity);
                owned.shards += gained;
                outcome = new InventoryPullOutcome(PullOutcome.Duplicate, pull.rarity, gained);
                Debug.Log($"[Inventory] 중복: {pull.drone} {pull.rarity} → 조각 +{gained} (누적 {owned.shards})");
            }

            Changed();
            return outcome;
        }

        // ---------------- 락온 뽑기: 안 가져간 칸의 조각 환산 ----------------

        // 락온 뽑기에서 확정할 때 "안 가져간 칸" 하나를 조각으로 바꿔 보유 드론에 넣는다.
        //  - 받는 조각 = 그 등급의 중복 조각(GachaTable) × percent / 100 (소수점은 버림)
        //  - 같은 종류를 보유 중이고 최대 레벨이 아니면 그 드론에 넣는다.
        //  - 아니면 "다음 강화까지 조각이 가장 조금 모자란" 보유 드론에 넣는다. (시뮬레이터 Tools/EconSim과 같은 규칙)
        //  - 조각을 받을 드론이 없으면(전부 최대 레벨) 0을 돌려준다.
        // target = 조각이 실제로 들어간 드론 (아무 데도 못 넣었으면 slotDrone 그대로).
        public int AddConvertedShards(DroneType slotDrone, GachaRarity rarity, int percent, out DroneType target)
        {
            target = slotDrone;
            if (!_isReady) return 0;

            int shards = _gachaTable.GetDuplicateShards(rarity) * Mathf.Clamp(percent, 0, 100) / 100;
            if (shards <= 0) return 0;

            var destination = FindOwned(slotDrone);
            if (destination == null || destination.level >= _growthTable.MaxLevel) destination = FindConversionTarget();
            if (destination == null) return 0;

            destination.shards += shards;
            target = destination.droneType;
            Debug.Log($"[Inventory] 락온 뽑기 조각 환산: {slotDrone} {rarity} → {destination.droneType} 조각 +{shards} (누적 {destination.shards})");
            Changed();
            return shards;
        }

        // 다음 강화까지 필요한 조각이 가장 적게 남은(이미 넘치면 가장 많이 넘치는) 보유 드론. 최대 레벨 드론은 제외. 없으면 null.
        private OwnedDroneData FindConversionTarget()
        {
            OwnedDroneData best = null;
            int bestNeed = int.MaxValue;
            foreach (var owned in _data.ownedDrones)
            {
                if (owned.level >= _growthTable.MaxLevel) continue;
                int need = _growthTable.GetUpgradeShardCost(owned.level) - owned.shards;
                if (best == null || need < bestNeed)
                {
                    best = owned;
                    bestNeed = need;
                }
            }
            return best;
        }

        // ---------------- 강화 ----------------

        // 다음 레벨로 올리는 데 필요한 조각 / 크레딧. 미보유이거나 이미 최대 레벨이면 0.
        public int GetUpgradeShardCost(DroneType type)
        {
            var owned = FindOwned(type);
            return owned != null && _isReady ? _growthTable.GetUpgradeShardCost(owned.level) : 0;
        }

        public int GetUpgradeCreditCost(DroneType type)
        {
            var owned = FindOwned(type);
            return owned != null && _isReady ? _growthTable.GetUpgradeCreditCost(owned.level) : 0;
        }

        // 드론을 한 단계 강화한다. 조각과 크레딧이 모두 충분해야 하고, 둘 다 한 번에 차감된다.
        // 조각이 모자라면 크레딧은 건드리지 않는다.
        public UpgradeResult TryUpgrade(DroneType type, CurrencyManager currency)
        {
            var owned = FindOwned(type);
            if (!_isReady || owned == null) return UpgradeResult.NotOwned;
            if (owned.level >= _growthTable.MaxLevel) return UpgradeResult.MaxLevel;

            int shardCost = _growthTable.GetUpgradeShardCost(owned.level);
            int creditCost = _growthTable.GetUpgradeCreditCost(owned.level);

            if (owned.shards < shardCost)
            {
                Debug.Log($"[Inventory] 강화 실패(조각 부족): {type} Lv{owned.level} 필요 {shardCost}, 보유 {owned.shards}");
                return UpgradeResult.NotEnoughShards;
            }

            // 크레딧 차감이 성공해야 강화한다 (모자라면 CurrencyManager가 "부족" 이벤트를 알려준다).
            if (currency == null || !currency.TrySpendCredit(creditCost))
                return UpgradeResult.NotEnoughCredit;

            owned.shards -= shardCost;
            owned.level++;
            Debug.Log($"[Inventory] 강화 성공: {type} Lv{owned.level - 1} → Lv{owned.level} (조각 -{shardCost}, 크레딧 -{creditCost}, 전투력 {TotalCombatPower})");
            PlayLog.RecordUpgrade(_data, type.ToString(), owned.level, TotalCombatPower);
            Changed();
            return UpgradeResult.Success;
        }

        // ---------------- 조각 교환 ----------------

        // 한 번 교환할 때 받는 조각 수 / 내는 크레딧 / 하루 한도. (수치는 DroneGrowthTable)
        public int ExchangeShards => _isReady ? _growthTable.ShardExchangeShards : 0;
        public int ExchangeCredit => _isReady ? _growthTable.ShardExchangeCredit : 0;
        public int ExchangeDailyLimit => _isReady ? _growthTable.ShardExchangeDailyLimit : 0;

        // 오늘 이미 교환한 횟수. 저장된 날짜가 오늘이 아니면 0으로 새로 시작한다. today는 검증용(비우면 기기의 오늘 날짜).
        public int GetExchangeCountToday(string today = null)
        {
            if (_data == null) return 0;
            today ??= DailyQuests.Today();
            if (_data.shardExchangeDate != today)
            {
                _data.shardExchangeDate = today;
                _data.shardExchangeCount = 0;
            }
            return _data.shardExchangeCount;
        }

        // 크레딧을 내고 고른 드론의 조각을 산다. 크레딧이 모자라거나 하루 한도를 넘었거나 이미 최대 레벨(조각이 더 필요 없음)이면 아무것도 바뀌지 않는다.
        public ExchangeResult TryExchangeShards(DroneType type, CurrencyManager currency, string today = null)
        {
            var owned = FindOwned(type);
            if (!_isReady || owned == null) return ExchangeResult.NotOwned;
            if (owned.level >= _growthTable.MaxLevel) return ExchangeResult.MaxLevel;
            if (GetExchangeCountToday(today) >= ExchangeDailyLimit) return ExchangeResult.DailyLimit;

            // 크레딧 차감이 성공해야 조각을 준다 (모자라면 CurrencyManager가 "부족" 이벤트를 알려준다).
            if (currency == null || !currency.TrySpendCredit(ExchangeCredit)) return ExchangeResult.NotEnoughCredit;

            owned.shards += ExchangeShards;
            _data.shardExchangeCount++;
            Debug.Log($"[Inventory] 조각 교환: {type} 조각 +{ExchangeShards} (크레딧 -{ExchangeCredit}, 오늘 {_data.shardExchangeCount}/{ExchangeDailyLimit}, 누적 {owned.shards})");
            PlayLog.RecordShardExchange(_data, type.ToString(), ExchangeShards, ExchangeCredit, _data.shardExchangeCount, ExchangeDailyLimit);
            Changed();
            return ExchangeResult.Success;
        }

        // ---------------- 장착 ----------------

        // 슬롯에 장착된 드론. 비어 있으면 null.
        public DroneType? GetEquipped(int slot)
        {
            if (_data == null || slot < 0 || slot >= _data.equippedDrones.Count) return null;
            int value = _data.equippedDrones[slot];
            return value < 0 ? (DroneType?)null : (DroneType)value;
        }

        // 슬롯에 드론을 장착한다. 보유하지 않은 드론이나 없는 슬롯이면 false.
        // 이미 다른 슬롯에 장착 중인 드론이면 두 슬롯의 내용을 서로 바꾼다 (같은 드론이 두 슬롯에 들어가지 않게).
        public bool TryEquip(int slot, DroneType type)
        {
            if (!_isReady || slot < 0 || slot >= _data.equippedDrones.Count) return false;
            if (!IsOwned(type)) return false;

            int existingSlot = _data.equippedDrones.IndexOf((int)type);
            if (existingSlot == slot) return true; // 이미 그 자리에 있음

            if (existingSlot >= 0) _data.equippedDrones[existingSlot] = _data.equippedDrones[slot];
            _data.equippedDrones[slot] = (int)type;

            Debug.Log($"[Inventory] 장착: 슬롯 {slot + 1} = {type} (전투력 {TotalCombatPower})");
            PlayLog.RecordEquip(_data, slot + 1, type.ToString(), TotalCombatPower);
            Changed();
            return true;
        }

        // 슬롯을 비운다. 없는 슬롯이거나 이미 비어 있으면 false.
        public bool Unequip(int slot)
        {
            if (_data == null || slot < 0 || slot >= _data.equippedDrones.Count) return false;
            if (_data.equippedDrones[slot] < 0) return false;

            _data.equippedDrones[slot] = -1;
            Debug.Log($"[Inventory] 장착 해제: 슬롯 {slot + 1} (전투력 {TotalCombatPower})");
            PlayLog.RecordEquip(_data, slot + 1, null, TotalCombatPower);
            Changed();
            return true;
        }

        // 장착한 드론을 "Melee SR Lv3 / Sniper N Lv1" 모양의 한 줄로 만든다. 테스트 기록(판 시작)에 쓴다.
        // 빈 슬롯은 "비움". (드론 이름은 장착·강화 기록과 같은 영문 종류 이름을 쓴다)
        public string BuildLoadoutSummary()
        {
            if (!_isReady) return "-";
            var parts = new System.Collections.Generic.List<string>();
            for (int slot = 0; slot < _data.equippedDrones.Count; slot++)
            {
                var equipped = GetEquipped(slot);
                if (equipped.HasValue && TryGetInfo(equipped.Value, out var info))
                    parts.Add($"{info.droneType} {info.rarity} Lv{info.level}");
                else
                    parts.Add("비움");
            }
            return string.Join(" / ", parts);
        }

        // ---------------- 전투력 ----------------

        // 등급 배율 (N ×1.00 ~ SSR ×1.45). 미보유면 0. 격납고가 "성능 = 등급 × 강화"로 나눠 보여줄 때 쓴다.
        public float GetGradeMultiplier(DroneType type)
        {
            var owned = FindOwned(type);
            return _isReady && owned != null ? _growthTable.GetGradeMultiplier(owned.rarity) : 0f;
        }

        // 강화 배율 = 1 + 레벨당 보너스 × (레벨 - 1) (Lv1 ×1.00 ~ Lv5 ×1.24). 미보유면 0.
        public float GetLevelMultiplier(DroneType type)
        {
            var owned = FindOwned(type);
            return _isReady && owned != null ? 1f + _growthTable.LevelBonusPerLevel * (owned.level - 1) : 0f;
        }

        // 드론 하나가 판 시작 때 기본 스탯에 곱해지는 배율 = 등급 배율 × 강화 배율. 미보유면 0.
        public float GetStatMultiplier(DroneType type) => GetGradeMultiplier(type) * GetLevelMultiplier(type);

        // 드론 하나의 전투력 = 기본 전투력 × 스탯 배율 (소수점 포함).
        public float GetPower(DroneType type) => _isReady ? _growthTable.BasePower * GetStatMultiplier(type) : 0f;

        // 장착한 드론들의 전투력 합계 (마지막에 한 번만 반올림).
        public int TotalCombatPower
        {
            get
            {
                if (!_isReady) return 0;
                float total = 0f;
                for (int slot = 0; slot < _data.equippedDrones.Count; slot++)
                {
                    var equipped = GetEquipped(slot);
                    if (equipped.HasValue) total += GetPower(equipped.Value);
                }
                return Mathf.RoundToInt(total);
            }
        }

        // ---------------- 도우미 ----------------

        private OwnedDroneData FindOwned(DroneType type)
        {
            if (_data == null) return null;
            foreach (var owned in _data.ownedDrones)
                if (owned.droneType == type) return owned;
            return null;
        }

        private void AddOwned(DroneType type, GachaRarity rarity)
        {
            _data.ownedDrones.Add(new OwnedDroneData { droneType = type, rarity = rarity, level = 1, shards = 0 });
        }

        private void Changed()
        {
            OnInventoryChanged?.Invoke();
            SaveIfNeeded();
        }

        private void SaveIfNeeded()
        {
            if (_saveOnChange) SaveManager.Save();
        }
    }
}
