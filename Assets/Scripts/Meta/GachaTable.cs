using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 뽑기의 "규칙 수치"를 전부 모아둔 데이터 상자 (ScriptableObject).
    // 확률·가격·천장 횟수를 코드에 직접 적지 않고 이 파일(.asset) 한 곳에서만 관리한다.
    //
    // 왜 이렇게 하나? ("표기 = 실제" 원칙)
    //  - 나중에 만들 "확률 공개 팝업"도 이 값을 그대로 읽어서 화면에 그린다.
    //    그러면 화면에 적힌 확률과 실제 뽑기 확률이 어긋날 수가 없다.
    //  - 9/6에 씬/프리팹에 저장된 값이 코드 기본값을 덮어써서 밸런스가 반영되지 않은 적이 있었다.
    //    수치의 출처를 이 파일 하나로 못 박아서 같은 실수를 막는다.
    //
    // 만드는 법: 프로젝트 창에서 우클릭 → Create → SurvivalDrone → Gacha Table
    // (지금은 Assets/Data/Meta/GachaTable.asset 에 이미 만들어져 있다)
    [CreateAssetMenu(menuName = "SurvivalDrone/Gacha Table", fileName = "GachaTable")]
    public class GachaTable : ScriptableObject
    {
        // ---- 등급별 확률 (단위: %, 네 개를 더하면 100이 되어야 한다) ----
        // 시뮬레이터(엑셀) 가정 시트의 값과 동일: N 55 / R 30 / SR 12.5 / SSR 2.5
        [Header("등급별 확률 (%) — 합계 100")]
        [SerializeField] private float _rateN = 55f;
        [SerializeField] private float _rateR = 30f;
        [SerializeField] private float _rateSR = 12.5f;
        [SerializeField] private float _rateSSR = 2.5f;

        // ---- 비용 (단위: 코어) ----
        [Header("비용 (코어)")]
        [SerializeField] private int _singleCost = 300;    // 1회 뽑기
        [SerializeField] private int _tenPullCost = 2700;  // 10연 뽑기 (9회 가격 = 10% 할인)

        // ---- 중복 시 조각 환산 ----
        // 이미 가진 드론과 같거나 낮은 등급이 또 나오면, 그 등급에 해당하는 만큼 설계도 조각을 준다.
        // 조각은 드론 강화에 쓴다. (N 10 / R 20 / SR 30 / SSR 50)
        [Header("중복 시 조각 환산")]
        [SerializeField] private int _duplicateShardsN = 10;
        [SerializeField] private int _duplicateShardsR = 20;
        [SerializeField] private int _duplicateShardsSR = 30;
        [SerializeField] private int _duplicateShardsSSR = 50;

        // ---- 천장 ----
        // 누적 뽑기 횟수가 이 값에 도달하는 순간 SSR을 확정으로 준다. SSR을 얻으면 누적 횟수는 0으로 돌아간다.
        [Header("천장")]
        [SerializeField] private int _pityCount = 70;

        // 바깥(다른 스크립트, UI)에서는 읽기만 가능하고 바꿀 수는 없게 프로퍼티로 열어둔다.
        public int SingleCost => _singleCost;
        public int TenPullCost => _tenPullCost;
        public int PityCount => _pityCount;

        // 등급 하나의 확률(%)을 돌려준다. 확률 공개 팝업이 이 함수를 그대로 사용할 예정.
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

        // 같은 등급이 중복으로 나왔을 때 받는 조각 수.
        public int GetDuplicateShards(GachaRarity rarity)
        {
            switch (rarity)
            {
                case GachaRarity.N: return _duplicateShardsN;
                case GachaRarity.R: return _duplicateShardsR;
                case GachaRarity.SR: return _duplicateShardsSR;
                case GachaRarity.SSR: return _duplicateShardsSSR;
                default: return 0;
            }
        }

        // 네 등급의 확률 합계(%). 정상이라면 100이다.
        public float GetRateTotal() => _rateN + _rateR + _rateSR + _rateSSR;

        // 인스펙터에서 값을 바꿀 때마다 유니티가 자동으로 호출해주는 함수.
        // 확률 합계가 100이 아니거나 천장·비용이 0 이하이면 콘솔에 경고를 띄워 실수를 바로 알 수 있게 한다.
        private void OnValidate()
        {
            float total = GetRateTotal();
            if (Mathf.Abs(total - 100f) > 0.001f)
                Debug.LogWarning($"[Gacha] GachaTable 확률 합계가 100이 아닙니다: {total}%  (N{_rateN}/R{_rateR}/SR{_rateSR}/SSR{_rateSSR})", this);

            if (_pityCount < 1)
                Debug.LogWarning($"[Gacha] GachaTable 천장 횟수는 1 이상이어야 합니다: {_pityCount}", this);

            if (_singleCost < 0 || _tenPullCost < 0)
                Debug.LogWarning($"[Gacha] GachaTable 비용은 0 이상이어야 합니다: 1회 {_singleCost} / 10연 {_tenPullCost}", this);
        }
    }
}
