using System;
using System.Collections.Generic;
using SurvivalDrone.Meta;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 뽑기 시뮬레이터(SimGachaSession)가 규칙대로 동작하는지 확인하는 에디터 전용 검증 도구.
    // 메뉴 SurvivalDrone → Meta → Verify Gacha Simulator 로 실행하면 결과가 콘솔에 [Gacha] 접두사로 찍힌다.
    // 가장 중요한 것은 "시뮬레이터가 실제 데이터(코어·보유 드론·실제 천장·저장 데이터)를 전혀 바꾸지 않는다"는 점이다.
    // 진짜 저장 파일은 건드리지 않고 임시 데이터로만 검사한다.
    public static class SimGachaVerifier
    {
        [MenuItem("SurvivalDrone/Meta/Verify Gacha Simulator")]
        public static void Verify()
        {
            if (!EditorSafety.CanRunEditModeTool("Verify Gacha Simulator")) return;

            var gachaTable = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Meta/GachaTable.asset");
            var currencyTable = AssetDatabase.LoadAssetAtPath<CurrencyTable>("Assets/Data/Meta/CurrencyTable.asset");
            var growthTable = AssetDatabase.LoadAssetAtPath<DroneGrowthTable>("Assets/Data/Meta/DroneGrowthTable.asset");
            if (gachaTable == null || currencyTable == null || growthTable == null)
            {
                Debug.LogError("[Gacha] 시뮬레이터 검증 실패: GachaTable / CurrencyTable / DroneGrowthTable 에셋을 찾을 수 없습니다.");
                return;
            }

            int fails = 0;
            fails += CheckIsolation(gachaTable, currencyTable, growthTable);
            fails += CheckSameAsRealSystem(gachaTable);
            fails += CheckPityLimits(gachaTable);
            fails += CheckCountersAndReset(gachaTable);

            if (fails == 0) Debug.Log($"[Gacha] 시뮬레이터 검증 완료: 모든 항목 통과 ({DateTime.Now:HH:mm:ss})");
            else Debug.LogError($"[Gacha] 시뮬레이터 검증 완료: {fails}개 항목 실패 ({DateTime.Now:HH:mm:ss})");
        }

        // 시뮬레이터를 많이 돌려도 실제 코어·보유 드론·실제 천장·저장 데이터가 그대로이고, 그 뒤 실제 뽑기도 정상인지.
        private static int CheckIsolation(GachaTable gachaTable, CurrencyTable currencyTable, DroneGrowthTable growthTable)
        {
            var objects = new List<GameObject>();
            GameObject Make(string name) { var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; objects.Add(go); return go; }

            try
            {
                var data = new SaveData();
                var currency = Make("SimVerifier_Currency").AddComponent<CurrencyManager>();
                currency.Initialize(data, currencyTable, false);
                data.core = 5000;
                var inventory = Make("SimVerifier_Inventory").AddComponent<DroneInventory>();
                inventory.Initialize(data, growthTable, gachaTable, false);
                var controller = Make("SimVerifier_Controller").AddComponent<GachaController>();
                controller.Initialize(data, gachaTable, currency, inventory, new System.Random(1), false);

                // 실제 뽑기를 몇 번 해서 실제 천장이 0이 아닌 상태로 만들어 둔다 (섞였는지 알아보기 쉽게).
                controller.PullSingle();
                controller.PullSingle();

                string dataBefore = JsonUtility.ToJson(data);
                int coreBefore = currency.Core;
                int ownedBefore = inventory.OwnedCount;
                int pityBefore = controller.PityCount;
                int softBefore = controller.SoftPityCount;

                var sim = new SimGachaSession(gachaTable, new System.Random(2));
                for (int i = 0; i < 1000; i++) sim.PullSingle();
                for (int i = 0; i < 200; i++) sim.PullTen();
                sim.Reset();
                for (int i = 0; i < 50; i++) sim.PullTen();

                int fails = 0;
                fails += Check(JsonUtility.ToJson(data) == dataBefore, "시뮬레이터를 돌려도 저장 데이터(SaveData)가 그대로");
                fails += Check(currency.Core == coreBefore, $"코어가 그대로 ({coreBefore} → {currency.Core})");
                fails += Check(inventory.OwnedCount == ownedBefore, $"보유 드론 수가 그대로 ({ownedBefore} → {inventory.OwnedCount})");
                fails += Check(controller.PityCount == pityBefore && controller.SoftPityCount == softBefore,
                               $"실제 천장/소천장이 그대로 ({pityBefore}/{softBefore} → {controller.PityCount}/{controller.SoftPityCount})");

                // 시뮬레이터를 쓴 뒤에도 실제 뽑기는 정상: 코어가 정확히 1회 가격만큼 줄고, 실제 천장이 1 늘어난다.
                var report = controller.PullSingle();
                fails += Check(report.success && currency.Core == coreBefore - gachaTable.SingleCost,
                               $"시뮬레이터 사용 후에도 실제 뽑기는 코어 {gachaTable.SingleCost}를 차감 (잔액 {currency.Core})");
                fails += Check(controller.PityCount == pityBefore + 1 || controller.PityCount == 0,
                               $"실제 천장이 시뮬레이터와 무관하게 이어짐 ({pityBefore} → {controller.PityCount})");
                return fails;
            }
            finally
            {
                foreach (var go in objects) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // 같은 시드면 시뮬레이터 결과가 실제 GachaSystem과 한 장도 다르지 않은지. ("표기 = 실제" 확인)
        private static int CheckSameAsRealSystem(GachaTable gachaTable)
        {
            var real = new GachaSystem(gachaTable, new System.Random(7));
            var sim = new SimGachaSession(gachaTable, new System.Random(7));

            bool same = true;
            for (int i = 0; i < 300 && same; i++)
            {
                var a = real.PullSingle();
                var b = sim.PullSingle().pulls[0];
                same = a.rarity == b.rarity && a.drone == b.drone && a.isPityGuaranteed == b.isPityGuaranteed && a.isSoftPityGuaranteed == b.isSoftPityGuaranteed;
            }
            return Check(same, "같은 시드면 시뮬레이터 결과가 실제 뽑기 로직과 300장 모두 동일");
        }

        // 시뮬레이터 안에서도 천장이 지켜지는지: SSR 사이 간격 ≤ 천장 횟수, SR 이상 사이 간격 ≤ 소천장 횟수.
        private static int CheckPityLimits(GachaTable gachaTable)
        {
            var sim = new SimGachaSession(gachaTable, new System.Random(3));
            int sinceSsr = 0, sinceSr = 0, maxSsrGap = 0, maxSrGap = 0;
            const int total = 20000;
            for (int i = 0; i < total; i++)
            {
                var pull = sim.PullSingle().pulls[0];
                sinceSsr++;
                sinceSr++;
                if (pull.rarity == GachaRarity.SSR) { maxSsrGap = Mathf.Max(maxSsrGap, sinceSsr); sinceSsr = 0; }
                if (pull.rarity >= GachaRarity.SR) { maxSrGap = Mathf.Max(maxSrGap, sinceSr); sinceSr = 0; }
            }

            int fails = Check(maxSsrGap <= gachaTable.PityCount, $"SSR 사이 최대 간격 {maxSsrGap} ≤ 천장 {gachaTable.PityCount}");
            if (gachaTable.SoftPityCount > 0)
                fails += Check(maxSrGap <= gachaTable.SoftPityCount, $"SR 이상 사이 최대 간격 {maxSrGap} ≤ 소천장 {gachaTable.SoftPityCount}");
            return fails;
        }

        // 누적 횟수, SSR 개수, 결과 모양(0코어, Simulated 표시), 변경 알림, 초기화.
        private static int CheckCountersAndReset(GachaTable gachaTable)
        {
            var sim = new SimGachaSession(gachaTable, new System.Random(5));
            int changed = 0;
            sim.Changed += () => changed++;

            int ssr = 0;
            var ten = sim.PullTen();
            var single = sim.PullSingle();
            foreach (var p in ten.pulls) if (p.rarity == GachaRarity.SSR) ssr++;
            if (single.pulls[0].rarity == GachaRarity.SSR) ssr++;

            bool outcomesSimulated = true;
            foreach (var o in ten.outcomes) if (o.outcome != PullOutcome.Simulated || o.shardsGained != 0) outcomesSimulated = false;

            int fails = 0;
            fails += Check(ten.success && ten.pulls.Length == 10 && single.pulls.Length == 1, "10연은 10장, 1회는 1장");
            fails += Check(ten.spentCore == 0 && single.spentCore == 0, "시뮬레이터는 코어를 쓰지 않음 (spentCore 0)");
            fails += Check(outcomesSimulated, "모든 결과가 Simulated 표시이고 조각이 0");
            fails += Check(sim.TotalPulls == 11 && sim.SsrCount == ssr, $"누적 횟수 {sim.TotalPulls}회 / SSR {sim.SsrCount}개가 실제 결과와 일치");
            fails += Check(changed == 2, $"뽑기마다 변경 알림 발생 (2번 기대, {changed}번)");

            sim.Reset();
            fails += Check(sim.TotalPulls == 0 && sim.SsrCount == 0 && sim.PityCount == 0 && sim.SoftPityCount == 0 && changed == 3,
                           "초기화하면 횟수·SSR·천장이 모두 0이고 변경 알림이 한 번 더 발생");
            return fails;
        }

        private static int Check(bool ok, string description)
        {
            if (ok) Debug.Log($"[Gacha] ✔ {description}");
            else Debug.LogError($"[Gacha] ✘ {description}");
            return ok ? 0 : 1;
        }
    }
}
