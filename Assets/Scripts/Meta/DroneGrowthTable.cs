using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 뽑기로 얻은 드론이 "얼마나 강한가"와 "어떻게 강화하는가"에 관한 수치를 모아둔 데이터 상자 (ScriptableObject).
    // GachaTable, CurrencyTable과 같은 이유로 수치를 코드가 아니라 이 파일(.asset) 한 곳에서만 관리한다.
    //
    // 여기서 말하는 "등급 배율·강화 레벨"은 판 시작 때 드론의 기본 스탯에 곱해지는 값이다.
    // 판 안에서 레벨업으로 오르는 드론 레벨(레벨당 +16%)과는 별개이고, 그 위에 한 번 더 곱해진다.
    //
    // 만드는 법: 프로젝트 창에서 우클릭 → Create → SurvivalDrone → Drone Growth Table
    // (지금은 Assets/Data/Meta/DroneGrowthTable.asset 에 이미 만들어져 있다)
    [CreateAssetMenu(menuName = "SurvivalDrone/Drone Growth Table", fileName = "DroneGrowthTable")]
    public class DroneGrowthTable : ScriptableObject
    {
        // ---- 전투력 ----
        // 드론 1개의 전투력 = 기본 전투력 × 등급 배율 × (1 + 레벨당 보너스 × (레벨 - 1)).
        // 기본 전투력 50 → 시작 드론(N 등급 2개)의 합이 100이 되어 스테이지 1의 권장 전투력과 같아진다.
        [Header("전투력")]
        [SerializeField] private float _basePower = 50f;

        // ---- 등급 배율 ----
        [Header("등급 배율")]
        [SerializeField] private float _gradeMultiplierN = 1f;
        [SerializeField] private float _gradeMultiplierR = 1.1f;
        [SerializeField] private float _gradeMultiplierSR = 1.25f;
        [SerializeField] private float _gradeMultiplierSSR = 1.45f;

        // ---- 강화 ----
        // 강화 레벨이 1 오를 때마다 늘어나는 비율 (0.06 = +6%). 레벨 5에서는 ×1.24.
        [Header("강화")]
        [SerializeField] private float _levelBonusPerLevel = 0.06f;

        // 강화에 필요한 조각과 크레딧. 0번 칸이 "레벨 1 → 2", 1번 칸이 "2 → 3" ... 이다.
        // 칸 수 + 1 이 곧 최대 강화 레벨이다 (지금은 4칸 → 최대 레벨 5). 두 배열의 길이는 같아야 한다.
        [SerializeField] private int[] _upgradeShardCosts = { 30, 50, 80, 120 };
        [SerializeField] private int[] _upgradeCreditCosts = { 300, 500, 800, 1200 };

        // ---- 각성 단계 (OBT 2단계, 기본 꺼짐) ----
        // 드론 강화 레벨 상한을 5에서 더 늘리는 "각성" 단계. Lv6, Lv7, Lv8처럼 위 강화 비용 배열 뒤에 이어 붙는다.
        // 꺼져 있으면(기본) 지금 게임과 똑같이 최대 레벨이 5다. 켜면 최대 레벨 = 5 + 각성 칸 수가 된다.
        // 레벨당 전투력 보너스(위 _levelBonusPerLevel)는 각성 레벨에도 그대로 이어진다.
        // 값은 시험용 초안(상점 설계 10절): 조각 150/200/250 + 크레딧 1,500/2,000/2,500. CBT 로그를 본 뒤 여기서 숫자만 고친다.
        // 두 배열의 길이는 같아야 한다.
        [Header("각성 단계 (OBT 2단계)")]
        [SerializeField] private bool _awakeningEnabled = false;
        [SerializeField] private int[] _awakeningShardCosts = { 150, 200, 250 };
        [SerializeField] private int[] _awakeningCreditCosts = { 1500, 2000, 2500 };

        // ---- 조각 교환 (크레딧 → 조각) ----
        // 격납고에서 크레딧을 내고 고른 드론의 조각을 살 수 있다. 조각은 원래 뽑기에서 같은 드론이 또 나올 때만 생겨서
        // 강화의 진짜 병목이었고, 크레딧은 쓸 곳이 강화 비용뿐이라 남아돌았다. 그래서 남는 크레딧을 조각으로 바꿔 준다.
        // 한 번 교환할 때마다 _shardExchangeShards개의 조각을 _shardExchangeCredit 크레딧에 판다.
        // 뽑기(코어)를 대신하지 않도록 하루에 _shardExchangeDailyLimit번까지만 가능하고, 일일 퀘스트와 같은 시각(기기 날짜 기준)에 초기화된다.
        [Header("조각 교환 (크레딧 → 조각)")]
        [SerializeField] private int _shardExchangeShards = 10;
        [SerializeField] private int _shardExchangeCredit = 500;
        [SerializeField] private int _shardExchangeDailyLimit = 3;

        // ---- 출격 ----
        // 한 번에 장착해서 출격할 수 있는 드론 개수.
        [Header("출격")]
        [SerializeField] private int _equipSlotCount = 2;

        public float BasePower => _basePower;
        public float LevelBonusPerLevel => _levelBonusPerLevel;
        public int EquipSlotCount => _equipSlotCount;
        public int ShardExchangeShards => _shardExchangeShards;
        public int ShardExchangeCredit => _shardExchangeCredit;
        public int ShardExchangeDailyLimit => _shardExchangeDailyLimit;

        // 각성 단계가 켜져 있는지.
        public bool AwakeningEnabled => _awakeningEnabled;

        // 각성 단계를 뺀 기본 최대 레벨 (강화 비용 칸 수 + 1). 지금 게임은 5다.
        public int BaseMaxLevel => _upgradeShardCosts.Length + 1;

        // 최대 강화 레벨. 각성이 꺼져 있으면 기본 최대 레벨, 켜져 있으면 각성 칸 수만큼 더 높다.
        public int MaxLevel => BaseMaxLevel + (_awakeningEnabled ? _awakeningShardCosts.Length : 0);

        // 이 레벨이 각성 단계(기본 최대 레벨을 넘는 레벨)인지. 각성이 꺼져 있으면 항상 false.
        public bool IsAwakeningLevel(int level) => _awakeningEnabled && level > BaseMaxLevel && level <= MaxLevel;

        public float GetGradeMultiplier(GachaRarity rarity)
        {
            switch (rarity)
            {
                case GachaRarity.N: return _gradeMultiplierN;
                case GachaRarity.R: return _gradeMultiplierR;
                case GachaRarity.SR: return _gradeMultiplierSR;
                case GachaRarity.SSR: return _gradeMultiplierSSR;
                default: return 1f;
            }
        }

        // 현재 레벨에서 다음 레벨로 올리는 데 필요한 조각 수. 이미 최대 레벨이면 0.
        public int GetUpgradeShardCost(int currentLevel)
        {
            int index = currentLevel - 1;
            if (index >= 0 && index < _upgradeShardCosts.Length) return _upgradeShardCosts[index];

            // 기본 최대 레벨을 넘으면 각성 비용 (각성이 켜져 있을 때만)
            int awakeningIndex = index - _upgradeShardCosts.Length;
            return _awakeningEnabled && awakeningIndex >= 0 && awakeningIndex < _awakeningShardCosts.Length ? _awakeningShardCosts[awakeningIndex] : 0;
        }

        // 현재 레벨에서 다음 레벨로 올리는 데 필요한 크레딧. 이미 최대 레벨이면 0.
        public int GetUpgradeCreditCost(int currentLevel)
        {
            int index = currentLevel - 1;
            if (index >= 0 && index < _upgradeCreditCosts.Length) return _upgradeCreditCosts[index];

            int awakeningIndex = index - _upgradeCreditCosts.Length;
            return _awakeningEnabled && awakeningIndex >= 0 && awakeningIndex < _awakeningCreditCosts.Length ? _awakeningCreditCosts[awakeningIndex] : 0;
        }

        // 인스펙터에서 값을 바꿀 때마다 호출된다. 두 비용 배열의 길이가 다르거나 슬롯이 0개면 경고를 띄운다.
        private void OnValidate()
        {
            if (_upgradeShardCosts.Length != _upgradeCreditCosts.Length)
                Debug.LogWarning($"[Inventory] DroneGrowthTable 강화 조각 비용({_upgradeShardCosts.Length}칸)과 크레딧 비용({_upgradeCreditCosts.Length}칸)의 칸 수가 다릅니다.", this);

            if (_awakeningShardCosts.Length != _awakeningCreditCosts.Length)
                Debug.LogWarning($"[Inventory] DroneGrowthTable 각성 조각 비용({_awakeningShardCosts.Length}칸)과 크레딧 비용({_awakeningCreditCosts.Length}칸)의 칸 수가 다릅니다.", this);

            if (_equipSlotCount < 1)
                Debug.LogWarning($"[Inventory] DroneGrowthTable 장착 슬롯은 1개 이상이어야 합니다: {_equipSlotCount}", this);

            if (_shardExchangeShards < 1 || _shardExchangeCredit < 1 || _shardExchangeDailyLimit < 0)
                Debug.LogWarning($"[Inventory] DroneGrowthTable 조각 교환 값이 잘못됐습니다: 조각 {_shardExchangeShards} / 크레딧 {_shardExchangeCredit} / 하루 {_shardExchangeDailyLimit}번", this);
        }
    }
}
