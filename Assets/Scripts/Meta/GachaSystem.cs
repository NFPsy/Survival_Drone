using System;
using SurvivalDrone.Drones;
using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 뽑기의 "두뇌". 확률을 굴리고 천장을 처리한다.
    //
    // MonoBehaviour가 아니라 그냥 C# 클래스로 만든 이유:
    //  - 씬에 붙일 필요 없이 코드만으로 만들 수 있어서, UI 없이도 순수하게 로직만 검증할 수 있다.
    //  - 난수 생성기(System.Random)를 밖에서 넣어줄 수 있어서, 같은 시드(seed)를 주면
    //    항상 같은 결과가 나온다 → "100,000번 뽑아서 통계 확인" 같은 검증이 가능하다.
    //
    // 이 클래스가 "하지 않는 일" (다음 단계에서 붙일 예정):
    //  - 코어(재화) 차감과 "코어 부족" 처리 → CurrencyManager. 뽑기 전에 그쪽에서 먼저 차감한 뒤 여기를 부른다.
    //  - 신규/중복 판단, 조각 환산 → DroneInventory
    //  - 천장 카운트 저장/불러오기 → SaveData (여기서는 PityCount 값을 읽고 쓸 수 있게만 열어둔다)
    public class GachaSystem
    {
        // 뽑기 규칙(확률/비용/천장)이 들어있는 데이터 파일.
        private readonly GachaTable _table;

        // 난수 생성기. 테스트에서는 시드를 고정한 것을 넣어준다.
        private readonly System.Random _random;

        // 뽑을 수 있는 드론 종류 목록 (DroneType 열거형에 있는 값 전부).
        private readonly DroneType[] _droneTypes;

        // 등급 판정을 위해 미리 만들어둔 "누적 확률표". (N, R, SR, SSR 순서)
        // 예: 55 / 85 / 97.5 / 100  → 0~100 사이 난수가 55 미만이면 N, 85 미만이면 R ...
        private readonly float[] _cumulativeRates;

        // 이 10연은 "1회 뽑기를 10번 순서대로" 처리한다는 뜻의 횟수.
        public const int TenPullCount = 10;

        // 현재 누적 뽑기 횟수(천장 카운트). SSR이 나오면 0으로 돌아간다.
        // 저장된 값을 불러올 때 SaveData가 이 값을 넣어준다.
        public int PityCount { get; private set; }

        public GachaSystem(GachaTable table, System.Random random, int startPityCount = 0)
        {
            if (table == null) throw new ArgumentNullException(nameof(table), "[Gacha] GachaTable이 비어 있습니다.");
            if (random == null) throw new ArgumentNullException(nameof(random), "[Gacha] 난수 생성기가 비어 있습니다.");

            _table = table;
            _random = random;
            PityCount = Mathf.Max(0, startPityCount);
            _droneTypes = (DroneType[])Enum.GetValues(typeof(DroneType));

            // 확률표를 한 번만 계산해둔다 (등급 순서대로 누적).
            var rarities = (GachaRarity[])Enum.GetValues(typeof(GachaRarity));
            _cumulativeRates = new float[rarities.Length];
            float sum = 0f;
            for (int i = 0; i < rarities.Length; i++)
            {
                sum += table.GetRate(rarities[i]);
                _cumulativeRates[i] = sum;
            }
        }

        // 1회 뽑기.
        //  1) 누적 횟수를 1 올린다.
        //  2) 누적 횟수가 천장에 도달했으면 SSR 확정, 아니면 확률표로 등급을 굴린다.
        //  3) SSR이 나오면 누적 횟수를 0으로 되돌린다.
        //  4) 드론 종류는 5종 중 균등 확률로 고른다. (등급별 드론 확률 차이는 아직 정해진 게 없다)
        public GachaPullResult PullSingle()
        {
            PityCount++;

            bool isPityGuaranteed = PityCount >= _table.PityCount;
            GachaRarity rarity = isPityGuaranteed ? GachaRarity.SSR : RollRarity();

            if (rarity == GachaRarity.SSR) PityCount = 0;

            DroneType drone = _droneTypes[_random.Next(_droneTypes.Length)];

            return new GachaPullResult(rarity, drone, isPityGuaranteed, PityCount);
        }

        // 10연 뽑기. 1회 뽑기를 순서대로 10번 처리한다 (중간에 SSR이 나오면 천장 카운트도 그 자리에서 0이 된다).
        public GachaPullResult[] PullTen()
        {
            var results = new GachaPullResult[TenPullCount];
            for (int i = 0; i < TenPullCount; i++)
                results[i] = PullSingle();
            return results;
        }

        // 확률표로 등급 하나를 굴린다. 0~100 사이 난수를 뽑아 누적 확률표에서 해당하는 칸을 찾는다.
        private GachaRarity RollRarity()
        {
            float roll = (float)(_random.NextDouble() * _cumulativeRates[_cumulativeRates.Length - 1]);
            for (int i = 0; i < _cumulativeRates.Length; i++)
            {
                if (roll < _cumulativeRates[i]) return (GachaRarity)i;
            }
            // 소수점 계산 오차로 아무 칸에도 안 걸렸을 때를 대비한 안전장치: 마지막 칸(SSR)으로 처리한다.
            return (GachaRarity)(_cumulativeRates.Length - 1);
        }
    }
}
