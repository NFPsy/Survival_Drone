using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 락온 뽑기의 "규칙 수치"를 전부 모아둔 데이터 상자 (ScriptableObject). GachaTable과 같은 방식이다.
    // 확률·가격·환산 비율을 코드에 적지 않고 이 파일(.asset) 한 곳에서만 관리한다. ("표기 = 실제" 원칙)
    // 화면에 적히는 확률·가격 문장도 이 값을 읽어서 만들기 때문에, 화면과 실제 규칙이 어긋날 수 없다.
    //
    // 락온 뽑기가 뭔가요? (10/8 설계 합의, 노션 BM 기획 문서 2.2)
    //  - 기본 뽑기와 별개의 뽑기. 한 번 시작하면 드론 3칸이 한꺼번에 공개된다.
    //  - SR 이상인 칸만 "잠글" 수 있고, 잠그지 않은 칸만 코어를 내고 다시 뽑는다(재뽑기).
    //  - 확정하면 잠근 칸만 받는다. 안 가져간 칸은 조각으로 바뀐다. 쓴 코어는 돌려받지 못한다.
    //  - SSR은 나오지 않는다 (락온 뽑기 = SR 확보 상품, SSR은 기본 뽑기에서만).
    //
    // 만드는 법: 프로젝트 창에서 우클릭 → Create → SurvivalDrone → Lock-On Table
    // (지금은 Assets/Data/Meta/LockOnTable.asset 에 이미 만들어져 있다)
    [CreateAssetMenu(menuName = "SurvivalDrone/Lock-On Table", fileName = "LockOnTable")]
    public class LockOnTable : ScriptableObject
    {
        // ---- 사용 여부 ----
        // 꺼 두면 뽑기 화면에 "락온 뽑기" 버튼이 나타나지 않는다. CBT 1차(기준선 0.1.1)에서는 꺼 두고, 2차 테스트부터 켠다.
        [Header("사용 여부 (꺼 두면 뽑기 화면에 락온 뽑기 버튼이 나오지 않는다)")]
        [SerializeField] private bool _enabled = false;

        // ---- 슬롯 등급 확률 (단위: %, 합계 100) ----
        // SSR은 0이다. 이유: 칸당 단가(100)가 기본 뽑기(300)보다 싸서, SSR이 나오면 "첫 공개만 하기" 같은 전략이
        // 기본 뽑기보다 SSR을 훨씬 싸게 얻어 BM 목표(30일 무과금 기대 SSR 0.5~1.5)를 넘는다. (시뮬레이터 Tools/EconSim 검증)
        [Header("칸 하나의 등급 확률 (%) — 합계 100")]
        [SerializeField] private float _rateN = 55f;
        [SerializeField] private float _rateR = 30f;
        [SerializeField] private float _rateSR = 15f;
        [SerializeField] private float _rateSSR = 0f;

        // ---- 슬롯 ----
        [Header("슬롯")]
        [SerializeField] private int _slotCount = 3;
        // 이 등급 이상인 칸만 잠글 수 있다.
        [SerializeField] private GachaRarity _lockMinRarity = GachaRarity.SR;

        // ---- 비용 (단위: 코어) ----
        // 처음 공개(시작) 비용 / 재뽑기 칸당 단가 / 잠근 칸 하나당 재뽑기가 비싸지는 비율.
        // 재뽑기 비용 = (다시 뽑는 칸 수) × 칸당 단가 × (1 + 프리미엄 × 잠근 칸 수)
        [Header("비용 (코어)")]
        [SerializeField] private int _firstCost = 450;
        [SerializeField] private int _rerollUnitCost = 100;
        [SerializeField] private float _rerollPremium = 0.5f;

        // ---- 안 가져간 칸의 조각 환산 ----
        // 확정할 때 잠그지 않은 칸은 받지 못하는 대신, 기본 뽑기의 "중복 조각"(GachaTable)의 이 비율(%)만큼 조각으로 바뀐다.
        [Header("안 가져간 칸의 조각 환산 비율 (%)")]
        [Range(0, 100)]
        [SerializeField] private int _unlockedShardPercent = 25;

        public bool Enabled => _enabled;
        public int SlotCount => _slotCount;
        public GachaRarity LockMinRarity => _lockMinRarity;
        public int FirstCost => _firstCost;
        public int RerollUnitCost => _rerollUnitCost;
        public float RerollPremium => _rerollPremium;
        public int UnlockedShardPercent => _unlockedShardPercent;

        // 등급 하나의 확률(%).
        public float GetRate(GachaRarity rarity)
        {
            switch (rarity)
            {
                case GachaRarity.N: return _rateN;
                case GachaRarity.R: return _rateR;
                case GachaRarity.SR: return _rateSR;
                case GachaRarity.SSR: return _rateSSR;
                default: return 0f;
            }
        }

        public float GetRateTotal() => _rateN + _rateR + _rateSR + _rateSSR;

        // 칸 하나가 "잠글 수 있는 등급"으로 나올 확률 (0~1). 기본값은 SR 이상 15%.
        public float GetLockableProbability()
        {
            float sum = 0f;
            for (int r = (int)_lockMinRarity; r <= (int)GachaRarity.SSR; r++) sum += GetRate((GachaRarity)r);
            float total = GetRateTotal();
            return total > 0f ? sum / total : 0f;
        }

        // 칸 하나의 등급을 확률표로 굴린다. 0~합계 사이 난수를 뽑아 누적 확률표에서 해당하는 칸을 찾는다.
        // random을 밖에서 받는 이유: 검증 도구가 시드를 고정해서 같은 결과를 재현하려고.
        public GachaRarity RollRarity(System.Random random)
        {
            float roll = (float)(random.NextDouble() * GetRateTotal());
            float accumulated = 0f;
            for (int r = (int)GachaRarity.N; r <= (int)GachaRarity.SSR; r++)
            {
                accumulated += GetRate((GachaRarity)r);
                if (roll < accumulated) return (GachaRarity)r;
            }
            // 소수점 오차로 아무 칸에도 안 걸렸을 때: 확률이 0보다 큰 가장 높은 등급으로 처리한다. (SSR 확률이 0이면 SR)
            for (int r = (int)GachaRarity.SSR; r >= (int)GachaRarity.N; r--)
                if (GetRate((GachaRarity)r) > 0f) return (GachaRarity)r;
            return GachaRarity.N;
        }

        // 재뽑기 비용. unlockedCount = 이번에 다시 뽑을 칸 수, lockedCount = 지금 잠근 칸 수. 소수점은 버린다.
        public int GetRerollCost(int unlockedCount, int lockedCount)
        {
            double cost = unlockedCount * (double)_rerollUnitCost * (1.0 + _rerollPremium * lockedCount);
            return (int)System.Math.Floor(cost);
        }

        // 인스펙터에서 값을 바꿀 때마다 유니티가 불러준다. 실수를 바로 알 수 있게 경고를 띄운다.
        private void OnValidate()
        {
            float total = GetRateTotal();
            if (Mathf.Abs(total - 100f) > 0.001f)
                Debug.LogWarning($"[LockOn] LockOnTable 확률 합계가 100이 아닙니다: {total}%", this);
            if (_slotCount < 1)
                Debug.LogWarning($"[LockOn] LockOnTable 슬롯 수는 1 이상이어야 합니다: {_slotCount}", this);
            if (_firstCost < 0 || _rerollUnitCost < 0 || _rerollPremium < 0f)
                Debug.LogWarning($"[LockOn] LockOnTable 비용·프리미엄은 0 이상이어야 합니다: 첫 공개 {_firstCost} / 칸당 {_rerollUnitCost} / 프리미엄 {_rerollPremium}", this);
            if (GetLockableProbability() <= 0f)
                Debug.LogWarning("[LockOn] LockOnTable 잠글 수 있는 등급의 확률이 0이라 어떤 칸도 잠글 수 없습니다.", this);
        }
    }
}
