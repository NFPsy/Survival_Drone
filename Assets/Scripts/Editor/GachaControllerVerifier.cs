using System;
using System.Collections.Generic;
using SurvivalDrone.Drones;
using SurvivalDrone.Meta;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // GachaController(코어 차감 → 뽑기 → 보유 목록 반영 → 천장 저장)의 전체 흐름이 규칙대로 동작하는지 확인하는 에디터 전용 검증 도구.
    // 메뉴 SurvivalDrone → Meta → Verify Gacha Flow 로 실행하면 결과가 콘솔에 [Gacha] 접두사로 찍힌다.
    // 진짜 저장 파일은 건드리지 않고 임시 데이터로만 검사한다.
    public static class GachaControllerVerifier
    {
        // 검증 한 판에 쓰는 부품 묶음 (매 항목마다 깨끗한 상태로 시작하려고 새로 만든다).
        private class Rig
        {
            public SaveData data;
            public CurrencyManager currency;
            public DroneInventory inventory;
            public GachaController controller;
            public readonly List<GameObject> objects = new List<GameObject>();

            public void Destroy()
            {
                foreach (var go in objects) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static GachaTable _gachaTable;
        private static CurrencyTable _currencyTable;
        private static DroneGrowthTable _growthTable;

        [MenuItem("SurvivalDrone/Meta/Verify Gacha Flow")]
        public static void Verify()
        {
            _gachaTable = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Meta/GachaTable.asset");
            _currencyTable = AssetDatabase.LoadAssetAtPath<CurrencyTable>("Assets/Data/Meta/CurrencyTable.asset");
            _growthTable = AssetDatabase.LoadAssetAtPath<DroneGrowthTable>("Assets/Data/Meta/DroneGrowthTable.asset");
            if (_gachaTable == null || _currencyTable == null || _growthTable == null)
            {
                Debug.LogError("[Gacha] 흐름 검증 실패: GachaTable / CurrencyTable / DroneGrowthTable 에셋을 찾을 수 없습니다.");
                return;
            }

            int fails = 0;
            fails += CheckSinglePull();
            fails += CheckTenPull();
            fails += CheckInsufficientCore();
            fails += CheckPityPersistence();
            fails += CheckLongRun();
            fails += CheckSameSeedSameFlow();
            fails += CheckNotReady();

            if (fails == 0) Debug.Log($"[Gacha] 흐름 검증 완료: 모든 항목 통과 ({DateTime.Now:HH:mm:ss})");
            else Debug.LogError($"[Gacha] 흐름 검증 완료: {fails}개 항목 실패 ({DateTime.Now:HH:mm:ss})");
        }

        private static Rig MakeRig(int seed, int startCore)
        {
            var rig = new Rig { data = new SaveData() };
            GameObject Make(string name) { var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; rig.objects.Add(go); return go; }

            rig.currency = Make("GachaFlowVerifier_Currency").AddComponent<CurrencyManager>();
            rig.currency.Initialize(rig.data, _currencyTable, false);
            rig.data.core = startCore; // 시작 코어를 검사에 맞게 덮어쓴다

            rig.inventory = Make("GachaFlowVerifier_Inventory").AddComponent<DroneInventory>();
            rig.inventory.Initialize(rig.data, _growthTable, _gachaTable, false);

            rig.controller = Make("GachaFlowVerifier_Controller").AddComponent<GachaController>();
            rig.controller.Initialize(rig.data, _gachaTable, rig.currency, rig.inventory, new System.Random(seed), false);
            return rig;
        }

        // 1회 뽑기: 코어 300 차감, 결과 1개, 보유 목록·천장에 반영
        private static int CheckSinglePull()
        {
            var rig = MakeRig(1, 1000);
            try
            {
                int ownedBefore = rig.inventory.OwnedCount;
                var report = rig.controller.PullSingle();

                int fails = 0;
                fails += Check(report.success && report.pulls.Length == 1 && report.outcomes.Length == 1 && report.spentCore == _gachaTable.SingleCost,
                    $"1회 뽑기 성공: 결과 {report.pulls.Length}개, 사용 코어 {report.spentCore}");
                fails += Check(rig.currency.Core == 1000 - _gachaTable.SingleCost, $"코어 {1000} → {rig.currency.Core} (1회 비용 {_gachaTable.SingleCost} 차감)");

                var pull = report.pulls[0];
                bool owned = rig.inventory.TryGetInfo(pull.drone, out var info);
                fails += Check(owned && info.rarity >= pull.rarity, $"뽑은 드론({pull.drone} {pull.rarity})이 보유 목록에 반영됨");
                fails += Check(rig.data.gachaPityCount == rig.controller.PityCount && report.pityAfter == rig.controller.PityCount,
                    $"천장 카운트가 저장 데이터에 기록됨 ({rig.data.gachaPityCount})");
                fails += Check((report.outcomes[0].outcome == PullOutcome.New) == (rig.inventory.OwnedCount == ownedBefore + 1), "NEW 여부와 보유 종류 수 증가가 일치");
                return fails;
            }
            finally { rig.Destroy(); }
        }

        // 10연: 코어 2,700(9회 가격) 차감, 결과 10개
        private static int CheckTenPull()
        {
            var rig = MakeRig(2, 5000);
            try
            {
                var report = rig.controller.PullTen();
                int fails = 0;
                fails += Check(report.success && report.pulls.Length == 10 && report.outcomes.Length == 10 && report.spentCore == _gachaTable.TenPullCost,
                    $"10연 성공: 결과 {report.pulls.Length}개, 사용 코어 {report.spentCore}");
                fails += Check(rig.currency.Core == 5000 - _gachaTable.TenPullCost && _gachaTable.TenPullCost == 2700, $"코어 5000 → {rig.currency.Core} (10연 2,700 차감)");

                // 10연은 결과 10개의 천장 카운트가 순서대로 처리됐는지: 마지막 결과의 pityAfter가 최종 카운트와 같다
                fails += Check(report.pulls[9].pityAfter == rig.controller.PityCount && rig.data.gachaPityCount == rig.controller.PityCount,
                    $"10연 마지막 결과의 천장 카운트({report.pulls[9].pityAfter}) = 최종 카운트({rig.controller.PityCount})");
                return fails;
            }
            finally { rig.Destroy(); }
        }

        // 코어 부족: 차감도 뽑기도 없이 부족 이벤트만
        private static int CheckInsufficientCore()
        {
            int fails = 0;
            var rig = MakeRig(3, _gachaTable.SingleCost - 1); // 1회 비용보다 1 모자람
            try
            {
                int eventCount = 0, need = 0, have = 0;
                CurrencyType type = CurrencyType.Credit;
                rig.currency.OnInsufficient += (t, n, h) => { eventCount++; type = t; need = n; have = h; };
                int ownedBefore = rig.inventory.OwnedCount;

                var report = rig.controller.PullSingle();
                fails += Check(!report.success && report.failure == GachaPullFailure.InsufficientCore && report.pulls.Length == 0,
                    $"코어 {_gachaTable.SingleCost - 1}로 1회 뽑기 → 실패(InsufficientCore)");
                fails += Check(rig.currency.Core == _gachaTable.SingleCost - 1, "실패 시 코어가 그대로");
                fails += Check(eventCount == 1 && type == CurrencyType.Core && need == _gachaTable.SingleCost && have == _gachaTable.SingleCost - 1,
                    $"부족 이벤트 1회 (종류 {type}, 필요 {need}, 보유 {have})");
                fails += Check(rig.controller.PityCount == 0 && rig.data.gachaPityCount == 0 && rig.inventory.OwnedCount == ownedBefore,
                    "실패 시 천장 카운트와 보유 목록도 그대로");
            }
            finally { rig.Destroy(); }

            // 10연은 2,700이 있어야 한다: 2,699로는 실패, 2,700이면 성공
            var rig2 = MakeRig(4, _gachaTable.TenPullCost - 1);
            try
            {
                fails += Check(!rig2.controller.PullTen().success && rig2.currency.Core == _gachaTable.TenPullCost - 1, $"코어 {_gachaTable.TenPullCost - 1}로 10연 → 실패, 코어 그대로");
                rig2.currency.AddCore(1);
                var ok = rig2.controller.PullTen();
                fails += Check(ok.success && rig2.currency.Core == 0, $"코어 {_gachaTable.TenPullCost}이면 10연 성공, 잔액 {rig2.currency.Core}");
            }
            finally { rig2.Destroy(); }
            return fails;
        }

        // 천장 카운트 저장·이어하기: 69에서 시작하면 다음 뽑기는 SSR 확정 → 0으로 초기화
        private static int CheckPityPersistence()
        {
            int fails = 0;
            var rig = MakeRig(5, 1000);
            try
            {
                // 이미 저장돼 있던 천장 카운트에서 이어서 시작하는지 (게임을 껐다 켠 상황을 흉내)
                rig.data.gachaPityCount = _gachaTable.PityCount - 1;
                rig.controller.Initialize(rig.data, _gachaTable, rig.currency, rig.inventory, new System.Random(5), false);
                fails += Check(rig.controller.PityCount == _gachaTable.PityCount - 1, $"저장된 천장 카운트 {rig.data.gachaPityCount}에서 이어서 시작");

                var report = rig.controller.PullSingle();
                fails += Check(report.pulls[0].rarity == GachaRarity.SSR && report.pulls[0].isPityGuaranteed, "천장 직전(69)에서 뽑으면 SSR 확정 (천장 발동)");
                fails += Check(rig.controller.PityCount == 0 && rig.data.gachaPityCount == 0, "SSR이 나오면 천장 카운트가 0으로 저장됨");

                // 새 컨트롤러가 같은 저장 데이터에서 시작해도 카운트가 이어진다
                rig.controller.PullSingle();
                int saved = rig.data.gachaPityCount;
                rig.controller.Initialize(rig.data, _gachaTable, rig.currency, rig.inventory, new System.Random(6), false);
                fails += Check(rig.controller.PityCount == saved, $"저장 데이터로 다시 준비해도 천장 카운트 {saved} 유지");
            }
            finally { rig.Destroy(); }
            return fails;
        }

        // 길게 돌려도 불변 조건이 지켜지는지: 코어 정산, 천장 한도, 종류당 1개
        private static int CheckLongRun()
        {
            var rig = MakeRig(7, 2700 * 200);
            try
            {
                int totalSpent = 0;
                int maxPity = 0;
                int pulls = 0;
                for (int i = 0; i < 200; i++)
                {
                    var report = rig.controller.PullTen();
                    totalSpent += report.spentCore;
                    pulls += report.pulls.Length;
                    maxPity = Math.Max(maxPity, rig.controller.PityCount);
                }

                int fails = 0;
                fails += Check(rig.currency.Core == 2700 * 200 - totalSpent && totalSpent == 2700 * 200, $"10연 200번 후 코어 정산: 사용 {totalSpent}, 잔액 {rig.currency.Core}");
                fails += Check(maxPity < _gachaTable.PityCount, $"천장 카운트가 한도({_gachaTable.PityCount}) 미만으로 유지됨 (최대 {maxPity})");
                var seen = new HashSet<DroneType>();
                bool unique = true;
                foreach (var owned in rig.data.ownedDrones) unique &= seen.Add(owned.droneType);
                fails += Check(unique && rig.data.ownedDrones.Count == 5, $"{pulls}회 뽑은 뒤 보유 드론은 종류당 1개, 5종 모두 보유");
                return fails;
            }
            finally { rig.Destroy(); }
        }

        // 같은 시드로 시작하면 흐름 전체(등급·드론·NEW 여부)가 똑같이 나온다
        private static int CheckSameSeedSameFlow()
        {
            var a = MakeRig(11, 27000);
            var b = MakeRig(11, 27000);
            try
            {
                bool same = true;
                for (int i = 0; i < 10 && same; i++)
                {
                    var ra = a.controller.PullTen();
                    var rb = b.controller.PullTen();
                    for (int j = 0; j < 10 && same; j++)
                        same = ra.pulls[j].rarity == rb.pulls[j].rarity && ra.pulls[j].drone == rb.pulls[j].drone &&
                               ra.outcomes[j].outcome == rb.outcomes[j].outcome && ra.outcomes[j].shardsGained == rb.outcomes[j].shardsGained;
                }
                return Check(same, "같은 시드(11)로 10연 10번을 두 번 돌리면 결과·NEW·조각이 모두 동일");
            }
            finally { a.Destroy(); b.Destroy(); }
        }

        // 부품이 준비되지 않았을 때는 예외 없이 실패로 알려준다
        private static int CheckNotReady()
        {
            var go = new GameObject("GachaFlowVerifier_NotReady") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var controller = go.AddComponent<GachaController>();
                controller.Initialize(new SaveData(), _gachaTable, null, null, new System.Random(1), false); // 경고 1개가 뜨는 것이 정상
                var report = controller.PullSingle();
                return Check(!report.success && report.failure == GachaPullFailure.NotReady, "준비되지 않았을 때는 실패(NotReady)로 안전하게 처리 (경고 1개는 정상)");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static int Check(bool ok, string message)
        {
            if (ok) Debug.Log($"[Gacha] 통과 — {message}");
            else Debug.LogError($"[Gacha] 실패 — {message}");
            return ok ? 0 : 1;
        }
    }
}
