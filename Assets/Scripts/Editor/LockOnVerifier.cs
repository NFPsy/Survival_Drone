using System;
using System.Collections.Generic;
using SurvivalDrone.Drones;
using SurvivalDrone.Meta;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 락온 뽑기(LockOnTable / LockOnSession)가 규칙대로 동작하는지 확인하는 에디터 전용 검증 도구.
    // 메뉴 SurvivalDrone → Meta → Verify Lock-On 으로 실행하면 결과가 콘솔에 [LockOn] 접두사로 찍힌다.
    // 진짜 저장 파일은 건드리지 않고 임시 데이터(SaveData)와 시드를 고정한 난수로만 검사한다.
    //
    // 검사 항목:
    //  1) 수치표: 확률 합계 100, SSR 확률 0이면 SSR이 한 번도 안 나옴, 굴린 분포가 확률표와 맞음
    //  2) 시작: 코어 부족이면 아무것도 안 바뀌고 로그만 남음 / 시작하면 첫 공개 비용만큼 차감
    //  3) 잠금 규칙: 잠글 수 있는 등급(SR 이상)만 잠김, 풀기는 언제나 됨
    //  4) 재뽑기: 비용 공식, 잠근 칸은 그대로, 다시 뽑을 칸이 없으면 거절
    //  5) 확정: 잠근 칸은 NEW/승급/중복 규칙대로 보유 목록에 반영, 안 가져간 칸은 환산 조각, 조각 총량 일치
    //  6) 잠근 칸이 0개여도 확정 가능 (드론 수 그대로, 조각만 증가)
    //  7) 테스트 로그와 요약, 기본 뽑기 천장 카운트가 그대로인지
    //  8) 평균 비용: "SR 이상을 잠그며 끝까지 채우는" 봇 4,000판의 평균 코어가 수학적 기대값과 같은지
    //     (파이썬 시뮬레이터 Tools/EconSim과 같은 규칙인지 확인하는 가장 중요한 검사)
    public static class LockOnVerifier
    {
        [MenuItem("SurvivalDrone/Meta/Verify Lock-On")]
        public static void Verify()
        {
            if (!EditorSafety.CanRunEditModeTool("Verify Lock-On")) return;

            var gachaTable = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Meta/GachaTable.asset");
            var currencyTable = AssetDatabase.LoadAssetAtPath<CurrencyTable>("Assets/Data/Meta/CurrencyTable.asset");
            var growthTable = AssetDatabase.LoadAssetAtPath<DroneGrowthTable>("Assets/Data/Meta/DroneGrowthTable.asset");
            if (gachaTable == null || currencyTable == null || growthTable == null)
            {
                Debug.LogError("[LockOn] 검증 실패: GachaTable / CurrencyTable / DroneGrowthTable 에셋을 찾을 수 없습니다.");
                return;
            }

            // 수치표는 실제 에셋을 쓰고, 아직 없으면(씬 빌더를 아직 안 돌렸으면) 기본값으로 만든 임시 수치표로 검사한다.
            var lockOn = AssetDatabase.LoadAssetAtPath<LockOnTable>("Assets/Data/Meta/LockOnTable.asset");
            bool temporary = lockOn == null;
            if (temporary)
            {
                lockOn = ScriptableObject.CreateInstance<LockOnTable>();
                Debug.LogWarning("[LockOn] LockOnTable.asset이 아직 없어 기본값 임시 수치표로 검증합니다. (메뉴 Build → Add Lock-On UI To Gacha Scene을 실행하면 만들어집니다)");
            }

            try
            {
                var tables = new Tables { gacha = gachaTable, currency = currencyTable, growth = growthTable, lockOn = lockOn };
                int fails = 0;
                fails += CheckTable(lockOn);
                fails += CheckStartAndInsufficient(tables);
                fails += CheckLockRules(tables);
                fails += CheckReroll(tables);
                fails += CheckConfirmMixed(tables);
                fails += CheckConfirmWithoutLock(tables);
                fails += CheckLogsAndIsolation(tables);
                fails += CheckAverageCost(tables);

                if (fails == 0) Debug.Log($"[LockOn] 락온 뽑기 검증 완료: 모든 항목 통과 ({DateTime.Now:HH:mm:ss})");
                else Debug.LogError($"[LockOn] 락온 뽑기 검증 완료: {fails}개 항목 실패 ({DateTime.Now:HH:mm:ss})");
            }
            finally
            {
                if (temporary) UnityEngine.Object.DestroyImmediate(lockOn);
            }
        }

        // ---------------- 준비물 ----------------
        private sealed class Tables
        {
            public GachaTable gacha;
            public CurrencyTable currency;
            public DroneGrowthTable growth;
            public LockOnTable lockOn;
        }

        // 임시 저장 데이터 + 재화 + 보유 드론을 묶은 한 판 분량의 환경. 다 쓰면 만든 오브젝트를 지운다.
        private sealed class Env : IDisposable
        {
            public readonly SaveData data = new SaveData();
            public readonly CurrencyManager currency;
            public readonly DroneInventory inventory;
            private readonly Tables _tables;
            private readonly List<GameObject> _objects = new List<GameObject>();

            public Env(Tables tables, int core)
            {
                _tables = tables;
                currency = Make("LockOnVerifier_Currency").AddComponent<CurrencyManager>();
                currency.Initialize(data, tables.currency, false);
                data.core = core;
                inventory = Make("LockOnVerifier_Inventory").AddComponent<DroneInventory>();
                inventory.Initialize(data, tables.growth, tables.gacha, false);
            }

            public LockOnSession NewSession(int seed) => new LockOnSession(_tables.lockOn, currency, inventory, data, new System.Random(seed), false);

            private GameObject Make(string name)
            {
                var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                _objects.Add(go);
                return go;
            }

            public void Dispose()
            {
                foreach (var go in _objects) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // 보유 중인 모든 드론의 조각 합계와 보유 수.
        private static int TotalShards(DroneInventory inventory)
        {
            int total = 0;
            foreach (DroneType type in Enum.GetValues(typeof(DroneType)))
                if (inventory.TryGetInfo(type, out var info)) total += info.shards;
            return total;
        }

        // 시작 직후 조건을 만족하는 시드를 찾는다. predicate가 true인 첫 시드와 그 세션을 돌려준다(못 찾으면 null).
        private static LockOnSession FindStartedSession(Env env, Func<LockOnSession, bool> predicate, out int seed)
        {
            for (seed = 1; seed <= 3000; seed++)
            {
                env.data.core = 1000000;
                var session = env.NewSession(seed);
                if (session.Start() != LockOnFailure.None) return null;
                if (predicate(session)) return session;
                // 조건에 안 맞는 판은 그냥 버린다(확정 없이 환경을 새로 만들 수 없으니 코어만 되돌리고 다음 시드로).
            }
            return null;
        }

        private static int LockableCount(LockOnSession s)
        {
            int n = 0;
            for (int i = 0; i < s.SlotCount; i++) if (s.CanLock(i)) n++;
            return n;
        }

        // ---------------- 1) 수치표 ----------------
        private static int CheckTable(LockOnTable table)
        {
            int fails = 0;
            fails += Check(Mathf.Abs(table.GetRateTotal() - 100f) < 0.001f, $"확률 합계가 100 ({table.GetRateTotal()}%)");
            fails += Check(table.GetLockableProbability() > 0f, $"잠글 수 있는 등급이 나올 확률이 0보다 큼 ({table.GetLockableProbability() * 100f:0.0}%)");
            fails += Check(table.GetRate(GachaRarity.SSR) == 0f, "설계 합의대로 락온 뽑기에는 SSR이 없음 (SSR 확률 0)");

            var random = new System.Random(11);
            const int total = 100000;
            var counts = new int[4];
            for (int i = 0; i < total; i++) counts[(int)table.RollRarity(random)]++;
            bool distributionOk = true;
            for (int r = 0; r < 4; r++)
            {
                float expected = table.GetRate((GachaRarity)r);
                float observed = counts[r] * 100f / total;
                if (Mathf.Abs(observed - expected) > 0.6f) distributionOk = false;
            }
            fails += Check(distributionOk, $"10만 번 굴린 분포가 확률표와 ±0.6%p 이내 (N {counts[0] * 100f / total:0.0} / R {counts[1] * 100f / total:0.0} / SR {counts[2] * 100f / total:0.0} / SSR {counts[3] * 100f / total:0.0})");
            if (table.GetRate(GachaRarity.SSR) == 0f)
                fails += Check(counts[3] == 0, $"SSR 확률이 0이면 10만 번 굴려도 SSR이 한 번도 안 나옴 ({counts[3]}개)");
            return fails;
        }

        // ---------------- 2) 시작 ----------------
        private static int CheckStartAndInsufficient(Tables t)
        {
            int fails = 0;
            int first = t.lockOn.FirstCost;

            using (var env = new Env(t, first - 1))
            {
                var session = env.NewSession(1);
                var result = session.Start();
                fails += Check(result == LockOnFailure.InsufficientCore && session.Phase == LockOnPhase.Idle, "코어가 1 모자라면 시작이 거절되고 단계는 시작 전 그대로");
                fails += Check(env.currency.Core == first - 1, $"거절돼도 코어는 그대로 ({env.currency.Core})");
                fails += Check(env.data.logStats.lockOnBlocked == 1 && LastLog(env.data).Contains("lockon_blocked"), "코어 부족이 테스트 로그(lockon_blocked)와 통계에 남음");
                fails += Check(session.Reroll() == LockOnFailure.WrongPhase && session.Confirm().failure == LockOnFailure.WrongPhase, "시작 전에는 재뽑기·확정이 거절됨");
            }

            using (var env = new Env(t, 10000))
            {
                var session = env.NewSession(2);
                var result = session.Start();
                fails += Check(result == LockOnFailure.None && session.Phase == LockOnPhase.Active, "코어가 충분하면 시작되고 단계가 진행 중으로 바뀜");
                fails += Check(env.currency.Core == 10000 - first && session.SpentCore == first, $"첫 공개 비용 {first}코어만 차감 (잔액 {env.currency.Core})");
                fails += Check(session.SlotCount == t.lockOn.SlotCount && session.LockedCount == 0, $"{t.lockOn.SlotCount}칸이 공개되고 처음엔 아무것도 잠기지 않음");
                fails += Check(LastLog(env.data).Contains("lockon_start"), "시작이 테스트 로그(lockon_start)에 남음");
                fails += Check(session.Start() == LockOnFailure.WrongPhase && env.currency.Core == 10000 - first, "진행 중에 다시 시작하면 거절되고 코어는 그대로");
            }
            return fails;
        }

        // ---------------- 3) 잠금 규칙 ----------------
        private static int CheckLockRules(Tables t)
        {
            int fails = 0;
            using (var env = new Env(t, 1000000))
            {
                var session = FindStartedSession(env, s => LockableCount(s) >= 1 && LockableCount(s) < s.SlotCount, out int seed);
                fails += Check(session != null, "잠글 수 있는 칸과 없는 칸이 섞인 시작 상태를 찾음");
                if (session == null) return fails;

                int lockable = -1, nonLockable = -1;
                for (int i = 0; i < session.SlotCount; i++)
                {
                    if (session.CanLock(i) && lockable < 0) lockable = i;
                    if (!session.CanLock(i) && nonLockable < 0) nonLockable = i;
                }
                fails += Check(session.Slots[lockable].rarity >= t.lockOn.LockMinRarity, $"잠글 수 있는 칸은 {t.lockOn.LockMinRarity} 이상 (칸 {lockable}: {session.Slots[lockable].rarity})");
                fails += Check(!session.SetLocked(nonLockable, true) && session.LockedCount == 0, $"{t.lockOn.LockMinRarity} 미만 칸은 잠기지 않음 (칸 {nonLockable}: {session.Slots[nonLockable].rarity})");
                fails += Check(session.SetLocked(lockable, true) && session.LockedCount == 1 && session.Slots[lockable].locked, "잠글 수 있는 칸은 잠김");
                fails += Check(!session.SetLocked(lockable, true), "이미 잠긴 칸을 또 잠가도 변화 없음");
                fails += Check(session.SetLocked(lockable, false) && session.LockedCount == 0, "잠금은 언제든 풀 수 있음");
            }
            return fails;
        }

        // ---------------- 4) 재뽑기 ----------------
        private static int CheckReroll(Tables t)
        {
            int fails = 0;

            // 비용 공식: 다시 뽑는 칸 수 × 칸 단가 × (1 + 프리미엄 × 잠근 칸 수), 소수점 버림. 표 전체를 따로 계산해서 맞춰 본다.
            bool formulaOk = true;
            int n = t.lockOn.SlotCount;
            for (int locked = 0; locked < n; locked++)
            {
                int unlocked = n - locked;
                int expected = (int)Math.Floor(unlocked * (double)t.lockOn.RerollUnitCost * (1.0 + t.lockOn.RerollPremium * locked));
                if (t.lockOn.GetRerollCost(unlocked, locked) != expected) formulaOk = false;
            }
            fails += Check(formulaOk, "재뽑기 비용 공식이 모든 (다시 뽑는 칸, 잠근 칸) 조합에서 맞음");

            using (var env = new Env(t, 1000000))
            {
                var session = FindStartedSession(env, s => LockableCount(s) >= 1 && LockableCount(s) < s.SlotCount, out _);
                fails += Check(session != null, "재뽑기를 시험할 시작 상태를 찾음");
                if (session == null) return fails;

                // 잠글 수 있는 칸을 모두 잠근다.
                for (int i = 0; i < session.SlotCount; i++) if (session.CanLock(i)) session.SetLocked(i, true);
                int lockedBefore = session.LockedCount;
                int expectedCost = t.lockOn.GetRerollCost(session.SlotCount - lockedBefore, lockedBefore);
                fails += Check(session.NextRerollCost == expectedCost, $"다음 재뽑기 비용 표시가 공식과 같음 ({session.NextRerollCost} / 기대 {expectedCost})");

                var before = new List<LockOnSlot>(session.Slots);
                int coreBefore = env.currency.Core;
                int spentBefore = session.SpentCore;
                var result = session.Reroll();
                fails += Check(result == LockOnFailure.None && env.currency.Core == coreBefore - expectedCost, $"재뽑기 비용만큼만 차감 ({coreBefore} → {env.currency.Core})");
                fails += Check(session.SpentCore == spentBefore + expectedCost && session.RerollCount == 1, "이번 판 사용 코어와 재뽑기 횟수가 갱신됨");

                bool lockedSame = true;
                for (int i = 0; i < before.Count; i++)
                {
                    if (!before[i].locked) continue;
                    var now = session.Slots[i];
                    if (!now.locked || now.rarity != before[i].rarity || now.drone != before[i].drone) lockedSame = false;
                }
                fails += Check(lockedSame, "잠근 칸은 재뽑기 뒤에도 그대로(등급·드론·잠금 유지)");
                fails += Check(LastLog(env.data).Contains("lockon_reroll") && env.data.logStats.lockOnRerolls == 1, "재뽑기가 테스트 로그(lockon_reroll)와 통계에 남음");
            }

            // 모두 잠그면 다시 뽑을 칸이 없어서 거절되고 코어는 그대로.
            using (var env = new Env(t, 1000000))
            {
                var session = env.NewSession(7);
                session.Start();
                int guard = 0;
                while (session.UnlockedCount > 0 && guard++ < 1000)
                {
                    for (int i = 0; i < session.SlotCount; i++) if (session.CanLock(i)) session.SetLocked(i, true);
                    if (session.UnlockedCount == 0) break;
                    session.Reroll();
                }
                int coreBefore = env.currency.Core;
                fails += Check(session.UnlockedCount == 0, $"계속 재뽑기하면 결국 모든 칸을 잠글 수 있음 (재뽑기 {session.RerollCount}번)");
                fails += Check(session.Reroll() == LockOnFailure.NothingToReroll && env.currency.Core == coreBefore, "모두 잠그면 재뽑기가 거절되고 코어는 그대로");
            }

            // 재뽑기 코어가 모자라면 아무것도 바뀌지 않는다.
            using (var env = new Env(t, 1000000))
            {
                var session = FindStartedSession(env, s => LockableCount(s) < s.SlotCount, out _);
                if (session != null)
                {
                    env.data.core = 0;
                    var snapshot = new List<LockOnSlot>(session.Slots);
                    var result = session.Reroll();
                    bool same = true;
                    for (int i = 0; i < snapshot.Count; i++)
                        if (session.Slots[i].rarity != snapshot[i].rarity || session.Slots[i].drone != snapshot[i].drone) same = false;
                    fails += Check(result == LockOnFailure.InsufficientCore && same && session.RerollCount == 0, "재뽑기 코어가 모자라면 거절되고 칸·횟수가 그대로");
                }
            }
            return fails;
        }

        // ---------------- 5) 확정 (잠근 칸 1개 + 못 잠근 칸) ----------------
        private static int CheckConfirmMixed(Tables t)
        {
            int fails = 0;
            using (var env = new Env(t, 1000000))
            {
                var session = FindStartedSession(env, s => LockableCount(s) == 1, out _);
                fails += Check(session != null, "잠글 수 있는 칸이 정확히 1개인 시작 상태를 찾음");
                if (session == null) return fails;

                int lockedIndex = -1;
                for (int i = 0; i < session.SlotCount; i++) if (session.CanLock(i)) lockedIndex = i;
                session.SetLocked(lockedIndex, true);
                var slots = new List<LockOnSlot>(session.Slots);

                // 확정 전 보유 상태를 기록한다.
                bool ownedBefore = env.inventory.TryGetInfo(slots[lockedIndex].drone, out var infoBefore);
                int totalShardsBefore = TotalShards(env.inventory);
                int ownedCountBefore = env.inventory.OwnedCount;

                var report = session.Confirm();
                fails += Check(report.success && session.Phase == LockOnPhase.Done && report.slots.Length == session.SlotCount, "확정하면 끝난 상태가 되고 칸 수만큼 결과가 나옴");
                fails += Check(report.spentCore == session.SpentCore && report.spentCore == t.lockOn.FirstCost, "결과의 사용 코어가 첫 공개 비용과 같음(재뽑기 없음)");

                // 잠근 칸: 기본 뽑기와 같은 규칙으로 반영.
                var locked = report.slots[lockedIndex];
                fails += Check(locked.received, "잠근 칸은 받음");
                int expectedDupShards = 0;
                if (!ownedBefore)
                {
                    fails += Check(locked.outcome.outcome == PullOutcome.New && env.inventory.IsOwned(locked.drone), "처음 얻는 드론이면 NEW로 보유 목록에 추가");
                }
                else if (locked.rarity > infoBefore.rarity)
                {
                    fails += Check(locked.outcome.outcome == PullOutcome.Promoted && env.inventory.TryGetInfo(locked.drone, out var after) && after.rarity == locked.rarity, "더 높은 등급이면 승급");
                }
                else
                {
                    expectedDupShards = t.gacha.GetDuplicateShards(locked.rarity);
                    fails += Check(locked.outcome.outcome == PullOutcome.Duplicate && locked.outcome.shardsGained == expectedDupShards, $"같거나 낮은 등급이면 중복 조각 +{expectedDupShards}");
                }

                // 안 가져간 칸: 환산 조각 = 중복 조각 × 비율 (소수점 버림). 보유 드론 수는 잠근 칸이 NEW일 때만 늘어난다.
                int expectedConverted = 0;
                bool convertedOk = true;
                for (int i = 0; i < report.slots.Length; i++)
                {
                    if (i == lockedIndex) continue;
                    var r = report.slots[i];
                    int expected = t.gacha.GetDuplicateShards(r.rarity) * t.lockOn.UnlockedShardPercent / 100;
                    if (r.received || r.convertedShards != expected) convertedOk = false;
                    expectedConverted += expected;
                }
                fails += Check(convertedOk, $"안 가져간 칸은 받지 않고, 환산 조각이 중복 조각 × {t.lockOn.UnlockedShardPercent}%(내림)와 같음");
                int expectedTotal = totalShardsBefore + expectedDupShards + expectedConverted;
                fails += Check(TotalShards(env.inventory) == expectedTotal, $"조각 총량이 맞음 ({totalShardsBefore} + 중복 {expectedDupShards} + 환산 {expectedConverted} = {TotalShards(env.inventory)})");
                fails += Check(env.inventory.OwnedCount == ownedCountBefore + (!ownedBefore ? 1 : 0), "보유 드론 수는 잠근 칸이 NEW일 때만 늘어남(안 가져간 칸은 드론을 주지 않음)");

                fails += Check(session.Confirm().failure == LockOnFailure.WrongPhase, "이미 확정한 판은 다시 확정할 수 없음");
                fails += Check(session.Start() == LockOnFailure.WrongPhase, "끝난 판은 Reset 전에 다시 시작할 수 없음");
                session.Reset();
                fails += Check(session.Phase == LockOnPhase.Idle && session.SpentCore == 0 && session.Start() == LockOnFailure.None, "Reset하면 처음 상태로 돌아가 새로 시작할 수 있음");
            }
            return fails;
        }

        // ---------------- 6) 잠근 칸이 없어도 확정 ----------------
        private static int CheckConfirmWithoutLock(Tables t)
        {
            int fails = 0;
            using (var env = new Env(t, 100000))
            {
                var session = env.NewSession(9);
                session.Start();
                int ownedBefore = env.inventory.OwnedCount;
                int shardsBefore = TotalShards(env.inventory);
                var report = session.Confirm();

                int expectedConverted = 0;
                bool noneReceived = true;
                foreach (var r in report.slots)
                {
                    if (r.received) noneReceived = false;
                    expectedConverted += t.gacha.GetDuplicateShards(r.rarity) * t.lockOn.UnlockedShardPercent / 100;
                }
                fails += Check(report.success && noneReceived, "아무것도 잠그지 않고 확정해도 처리됨 (받는 칸 없음)");
                fails += Check(env.inventory.OwnedCount == ownedBefore, "보유 드론 수가 그대로");
                fails += Check(TotalShards(env.inventory) == shardsBefore + expectedConverted, $"모든 칸이 조각으로만 바뀜 (+{expectedConverted})");
            }
            return fails;
        }

        // ---------------- 7) 로그·요약·기본 뽑기와의 분리 ----------------
        private static int CheckLogsAndIsolation(Tables t)
        {
            int fails = 0;
            using (var env = new Env(t, 100000))
            {
                int pityBefore = env.data.gachaPityCount, softBefore = env.data.gachaSoftPityCount;
                int pullsBefore = env.data.logStats.pullsTotal, spentBefore = env.data.logStats.coreSpentOnPulls;

                var session = env.NewSession(21);
                session.Start();
                for (int i = 0; i < session.SlotCount; i++) if (session.CanLock(i)) session.SetLocked(i, true);
                if (session.UnlockedCount > 0) session.Reroll();
                session.Confirm("나가기");

                string all = string.Join("\n", env.data.playLog);
                fails += Check(all.Contains("lockon_start") && all.Contains("lockon_confirm"), "시작·확정이 테스트 로그에 남음");
                fails += Check(all.Contains("방법=나가기"), "나가며 자동 확정한 판은 로그에 '방법=나가기'로 구분됨");
                fails += Check(env.data.logStats.lockOnConfirms == 1 && env.data.logStats.lockOnCoreSpent == session.SpentCore, "통계: 확정 1판, 쓴 코어가 이번 판 사용 코어와 같음");
                fails += Check(PlayLog.BuildSummaryText(env.data).Contains("락온 뽑기 1판 확정"), "요약에 락온 뽑기 줄이 나옴");
                fails += Check(env.data.gachaPityCount == pityBefore && env.data.gachaSoftPityCount == softBefore, "기본 뽑기의 천장·소천장 카운트는 건드리지 않음");
                fails += Check(env.data.logStats.pullsTotal == pullsBefore && env.data.logStats.coreSpentOnPulls == spentBefore, "기본 뽑기 통계(뽑은 횟수·쓴 코어)에는 섞이지 않음");
            }

            using (var env = new Env(t, 100000))
            {
                // 락온 뽑기를 한 번도 안 쓴 데이터의 요약은 옛 모양 그대로여야 한다.
                fails += Check(!PlayLog.BuildSummaryText(env.data).Contains("락온"), "락온 뽑기를 안 쓰면 요약에 락온 줄이 없음");
            }
            return fails;
        }

        // ---------------- 8) 평균 비용 = 수학적 기대값 ----------------
        // "SR 이상(잠글 수 있는 칸)을 잠그며 모든 칸이 잠길 때까지 재뽑기"하는 봇을 4,000판 돌려 평균 사용 코어를 구하고,
        // 같은 규칙으로 계산한 정확한 기대값(상태 = 잠근 칸 수)과 비교한다. 기본값(450 / 100 / 0.5, 성공 15%)이면 약 3,066.7코어다.
        private static int CheckAverageCost(Tables t)
        {
            int fails = 0;
            using (var env = new Env(t, int.MaxValue / 2))
            {
                const int sessions = 4000;
                long totalSpent = 0;
                int ssrSeen = 0;
                for (int s = 0; s < sessions; s++)
                {
                    var session = env.NewSession(1000 + s);
                    session.Start();
                    int guard = 0;
                    while (guard++ < 1000)
                    {
                        for (int i = 0; i < session.SlotCount; i++) if (session.CanLock(i)) session.SetLocked(i, true);
                        if (session.UnlockedCount == 0) break;
                        session.Reroll();
                    }
                    foreach (var slot in session.Slots) if (slot.rarity == GachaRarity.SSR) ssrSeen++;
                    totalSpent += session.Confirm().spentCore;
                }

                double average = (double)totalSpent / sessions;
                double expected = ExpectedFullSessionCost(t.lockOn);
                double error = Math.Abs(average - expected) / expected;
                fails += Check(error < 0.04, $"봇 {sessions}판 평균 사용 코어 {average:N1} ≈ 수학적 기대값 {expected:N1} (오차 {error * 100:0.0}%, 허용 4%)");
                if (t.lockOn.GetRate(GachaRarity.SSR) == 0f)
                    fails += Check(ssrSeen == 0, "봇 4,000판(12,000칸)에서 SSR이 한 번도 안 나옴");
            }
            return fails;
        }

        // 모든 칸을 잠글 때까지 재뽑기하는 한 판의 기대 비용. 상태 = 지금 잠근 칸 수 L.
        //  E[n] = 0, 나머지는 E[L] = (재뽑기 비용 + Σ_{k≥1} P(k칸 새로 성공) × E[L+k]) / (1 - P(0칸 성공))
        // 처음 공개에서 잠글 칸 수는 이항분포를 따른다.
        private static double ExpectedFullSessionCost(LockOnTable table)
        {
            int n = table.SlotCount;
            double p = table.GetLockableProbability();
            var e = new double[n + 1];
            for (int locked = n - 1; locked >= 0; locked--)
            {
                int unlocked = n - locked;
                double cost = table.GetRerollCost(unlocked, locked);
                double sum = cost;
                for (int k = 1; k <= unlocked; k++) sum += Binomial(unlocked, k, p) * e[locked + k];
                e[locked] = sum / (1.0 - Binomial(unlocked, 0, p));
            }
            double expected = table.FirstCost;
            for (int k = 0; k <= n; k++) expected += Binomial(n, k, p) * e[k];
            return expected;
        }

        private static double Binomial(int n, int k, double p)
        {
            double choose = 1.0;
            for (int i = 1; i <= k; i++) choose = choose * (n - k + i) / i;
            return choose * Math.Pow(p, k) * Math.Pow(1.0 - p, n - k);
        }

        // ---------------- 도우미 ----------------
        private static string LastLog(SaveData data) => data.playLog.Count > 0 ? data.playLog[data.playLog.Count - 1] : "";

        private static int Check(bool ok, string description)
        {
            if (ok) Debug.Log($"[LockOn] ✔ {description}");
            else Debug.LogError($"[LockOn] ✘ {description}");
            return ok ? 0 : 1;
        }
    }
}
