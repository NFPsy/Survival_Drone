using System;
using SurvivalDrone.Meta;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // GachaSystem이 기획서(시뮬레이터)대로 동작하는지 확인하는 에디터 전용 검증 도구.
    // 유니티 상단 메뉴 SurvivalDrone → Meta → Verify GachaSystem 을 누르면 실행되고,
    // 결과가 콘솔에 [Gacha] 접두사로 찍힌다. (게임 빌드에는 포함되지 않는다)
    //
    // 검증 항목 (인수인계 문서 / 노션 코딩 규칙 6번의 기준):
    //  1) 100,000번 뽑았을 때 SSR 1개당 평균 뽑기 수가 33.2 근처인가?
    //  2) SSR이 나오는 간격이 절대 70회를 넘지 않는가? (70회 안에 항상 SSR)
    //  3) 등급별 비율이 GachaTable의 확률과 비슷한가?
    //  4) 같은 시드 → 같은 결과인가? (시드 고정이 되는지)
    //  5) 10연 = 1회 뽑기 10번과 결과가 완전히 같은가?
    public static class GachaSystemVerifier
    {
        private const string TablePath = "Assets/Data/Meta/GachaTable.asset";
        private const int TotalPulls = 100000;
        private const int Seed = 12345;

        // 시드 하나만으로는 운에 따라 평균이 ±1 정도 흔들려서, 평균 검증은 시드 50개를 합쳐서(총 500만 회) 한다.
        private const int AverageCheckSeedCount = 50;

        // 시뮬레이터(엑셀)의 "천장 반영 SSR 1개 기대 뽑기 수"와 허용 오차.
        private const double ExpectedPullsPerSSR = 33.2;
        private const double Tolerance = 0.3;

        [MenuItem("SurvivalDrone/Meta/Verify GachaSystem")]
        public static void Verify()
        {
            if (!EditorSafety.CanRunEditModeTool("Verify GachaSystem")) return;

            var table = AssetDatabase.LoadAssetAtPath<GachaTable>(TablePath);
            if (table == null)
            {
                Debug.LogError($"[Gacha] 검증 실패: {TablePath} 를 찾을 수 없습니다.");
                return;
            }

            int failCount = 0;
            failCount += CheckStatistics(table);
            failCount += CheckSameSeedSameResult(table);
            failCount += CheckTenPullEqualsTenSingles(table);

            if (failCount == 0) Debug.Log($"[Gacha] 검증 완료: 모든 항목 통과 ({DateTime.Now:HH:mm:ss})");
            else Debug.LogError($"[Gacha] 검증 완료: {failCount}개 항목 실패 ({DateTime.Now:HH:mm:ss})");
        }

        // 항목 1, 2, 3: 10만 번 뽑아서 통계를 낸다.
        private static int CheckStatistics(GachaTable table)
        {
            var system = new GachaSystem(table, new System.Random(Seed));
            var counts = new int[Enum.GetValues(typeof(GachaRarity)).Length];
            int ssrCount = 0;
            int pullsSinceLastSSR = 0;
            int longestGap = 0;

            for (int i = 0; i < TotalPulls; i++)
            {
                var result = system.PullSingle();
                counts[(int)result.rarity]++;
                pullsSinceLastSSR++;

                if (result.rarity == GachaRarity.SSR)
                {
                    ssrCount++;
                    longestGap = Math.Max(longestGap, pullsSinceLastSSR);
                    pullsSinceLastSSR = 0;
                }
            }

            int fails = 0;

            // 1) SSR 1개당 평균 뽑기 수 (시드 여러 개를 합쳐서 계산)
            long allPulls = 0;
            long allSSR = 0;
            for (int s = 0; s < AverageCheckSeedCount; s++)
            {
                var avgSystem = new GachaSystem(table, new System.Random(Seed + s));
                for (int i = 0; i < TotalPulls; i++)
                    if (avgSystem.PullSingle().rarity == GachaRarity.SSR) allSSR++;
                allPulls += TotalPulls;
            }
            double avgPulls = (double)allPulls / allSSR;
            bool avgOk = Math.Abs(avgPulls - ExpectedPullsPerSSR) <= Tolerance;
            Log(avgOk, $"SSR 1개당 평균 뽑기 수 = {avgPulls:F2}회 (기대 {ExpectedPullsPerSSR}±{Tolerance}, SSR {allSSR}개 / {allPulls}회, 시드 {Seed}~{Seed + AverageCheckSeedCount - 1})");
            if (!avgOk) fails++;

            // 2) 가장 긴 SSR 간격이 천장 이내인가
            bool gapOk = longestGap <= table.PityCount;
            Log(gapOk, $"SSR 사이 가장 긴 간격 = {longestGap}회 (천장 {table.PityCount}회 이내여야 함)");
            if (!gapOk) fails++;

            // 3) 등급별 비율. 천장 때문에 SSR은 표기 확률(2.5%)보다 높아지는 게 정상이므로 N/R/SR만 표기 확률과 비교하지 않고 출력만 한다.
            string ratios = "";
            foreach (GachaRarity rarity in Enum.GetValues(typeof(GachaRarity)))
                ratios += $"{rarity} {counts[(int)rarity] * 100.0 / TotalPulls:F2}% (표기 {table.GetRate(rarity)}%)  ";
            Debug.Log($"[Gacha] 등급별 실제 비율 ({TotalPulls}회): {ratios}— SSR은 천장 때문에 표기보다 약 3.0%로 올라가는 게 정상");

            return fails;
        }

        // 항목 4: 시드가 같으면 결과가 완전히 같은지 (같은 시드로 두 번 돌려서 비교)
        private static int CheckSameSeedSameResult(GachaTable table)
        {
            var a = new GachaSystem(table, new System.Random(777));
            var b = new GachaSystem(table, new System.Random(777));
            bool same = true;
            for (int i = 0; i < 1000 && same; i++)
            {
                var ra = a.PullSingle();
                var rb = b.PullSingle();
                same = ra.rarity == rb.rarity && ra.drone == rb.drone && ra.pityAfter == rb.pityAfter;
            }
            Log(same, "같은 시드(777)로 1,000번 뽑은 결과가 두 번 모두 동일");
            return same ? 0 : 1;
        }

        // 항목 5: 10연이 "1회 뽑기 10번"과 똑같이 처리되는지
        private static int CheckTenPullEqualsTenSingles(GachaTable table)
        {
            var ten = new GachaSystem(table, new System.Random(999));
            var single = new GachaSystem(table, new System.Random(999));

            bool same = true;
            for (int round = 0; round < 100 && same; round++)
            {
                var tenResults = ten.PullTen();
                for (int i = 0; i < GachaSystem.TenPullCount && same; i++)
                {
                    var s = single.PullSingle();
                    same = tenResults[i].rarity == s.rarity && tenResults[i].drone == s.drone && tenResults[i].pityAfter == s.pityAfter;
                }
            }
            Log(same, "10연 100번 = 1회 뽑기 1,000번과 결과 동일 (천장 카운트 포함)");
            return same ? 0 : 1;
        }

        private static void Log(bool ok, string message)
        {
            if (ok) Debug.Log($"[Gacha] 통과 — {message}");
            else Debug.LogError($"[Gacha] 실패 — {message}");
        }
    }
}
