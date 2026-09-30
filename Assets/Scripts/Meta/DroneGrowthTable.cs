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

        // ---- 출격 ----
        // 한 번에 장착해서 출격할 수 있는 드론 개수.
        [Header("출격")]
        [SerializeField] private int _equipSlotCount = 2;

        public float BasePower => _basePower;
        public float LevelBonusPerLevel => _levelBonusPerLevel;
        public int EquipSlotCount => _equipSlotCount;

        // 최대 강화 레벨 (강화 비용 칸 수 + 1).
        public int MaxLevel => _upgradeShardCosts.Length + 1;

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
            return index >= 0 && index < _upgradeShardCosts.Length ? _upgradeShardCosts[index] : 0;
        }

        // 현재 레벨에서 다음 레벨로 올리는 데 필요한 크레딧. 이미 최대 레벨이면 0.
        public int GetUpgradeCreditCost(int currentLevel)
        {
            int index = currentLevel - 1;
            return index >= 0 && index < _upgradeCreditCosts.Length ? _upgradeCreditCosts[index] : 0;
        }

        // 인스펙터에서 값을 바꿀 때마다 호출된다. 두 비용 배열의 길이가 다르거나 슬롯이 0개면 경고를 띄운다.
        private void OnValidate()
        {
            if (_upgradeShardCosts.Length != _upgradeCreditCosts.Length)
                Debug.LogWarning($"[Inventory] DroneGrowthTable 강화 조각 비용({_upgradeShardCosts.Length}칸)과 크레딧 비용({_upgradeCreditCosts.Length}칸)의 칸 수가 다릅니다.", this);

            if (_equipSlotCount < 1)
                Debug.LogWarning($"[Inventory] DroneGrowthTable 장착 슬롯은 1개 이상이어야 합니다: {_equipSlotCount}", this);
        }
    }
}
