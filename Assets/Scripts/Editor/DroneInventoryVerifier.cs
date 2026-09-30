using System;
using SurvivalDrone.Drones;
using SurvivalDrone.Meta;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // DroneInventory(보유·승급·중복 조각·강화·장착·전투력)가 결정된 규칙대로 동작하는지 확인하는 에디터 전용 검증 도구.
    // 메뉴 SurvivalDrone → Meta → Verify Inventory 로 실행하면 결과가 콘솔에 [Inventory] 접두사로 찍힌다.
    // 진짜 저장 파일은 건드리지 않고 임시 데이터로만 검사한다.
    public static class DroneInventoryVerifier
    {
        [MenuItem("SurvivalDrone/Meta/Verify Inventory")]
        public static void Verify()
        {
            var growth = AssetDatabase.LoadAssetAtPath<DroneGrowthTable>("Assets/Data/Meta/DroneGrowthTable.asset");
            var gacha = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Meta/GachaTable.asset");
            var currencyTable = AssetDatabase.LoadAssetAtPath<CurrencyTable>("Assets/Data/Meta/CurrencyTable.asset");
            if (growth == null || gacha == null || currencyTable == null)
            {
                Debug.LogError("[Inventory] 검증 실패: DroneGrowthTable / GachaTable / CurrencyTable 에셋을 찾을 수 없습니다.");
                return;
            }

            var inventoryObject = new GameObject("DroneInventoryVerifier_Inventory") { hideFlags = HideFlags.HideAndDontSave };
            var currencyObject = new GameObject("DroneInventoryVerifier_Currency") { hideFlags = HideFlags.HideAndDontSave };
            int fails = 0;
            try
            {
                var data = new SaveData();
                var currency = currencyObject.AddComponent<CurrencyManager>();
                currency.Initialize(data, currencyTable, false);
                var inventory = inventoryObject.AddComponent<DroneInventory>();
                inventory.Initialize(data, growth, gacha, false);

                int changedCount = 0;
                inventory.OnInventoryChanged += () => changedCount++;

                // ---- 시작 지급 ----
                DroneInfo info;
                fails += Check(inventory.OwnedCount == 2 && inventory.TryGetInfo(DroneType.Melee, out info) && info.rarity == GachaRarity.N && info.level == 1
                               && inventory.TryGetInfo(DroneType.Sniper, out info) && info.rarity == GachaRarity.N,
                    $"시작 드론: 근접 N + 저격 N (보유 {inventory.OwnedCount}종)");
                fails += Check(inventory.GetEquipped(0) == DroneType.Melee && inventory.GetEquipped(1) == DroneType.Sniper && inventory.TotalCombatPower == 100,
                    $"시작 드론이 장착되어 있고 전투력 100 (실제 {inventory.TotalCombatPower}) = 스테이지 1 권장 전투력");

                // 이미 초기화된 데이터로 다시 Initialize해도 중복 지급되지 않는다
                inventory.Initialize(data, growth, gacha, false);
                fails += Check(inventory.OwnedCount == 2, $"이미 초기화된 데이터는 시작 드론을 다시 주지 않음 (보유 {inventory.OwnedCount}종)");

                // ---- 뽑기 결과 반영 ----
                // 신규
                var outcome = inventory.Apply(new GachaPullResult(GachaRarity.R, DroneType.Heal, false, 1));
                fails += Check(outcome.outcome == PullOutcome.New && inventory.IsOwned(DroneType.Heal) && inventory.TryGetInfo(DroneType.Heal, out info) && info.rarity == GachaRarity.R && info.shards == 0,
                    "처음 얻는 드론은 NEW로 추가됨 (회복 R)");

                // 같은 등급 중복 → 그 등급의 조각
                outcome = inventory.Apply(new GachaPullResult(GachaRarity.N, DroneType.Melee, false, 2));
                fails += Check(outcome.outcome == PullOutcome.Duplicate && outcome.shardsGained == gacha.GetDuplicateShards(GachaRarity.N) && outcome.shardsGained == 10,
                    $"같은 등급(N) 중복 → 조각 +{outcome.shardsGained}");

                // 낮은 등급 중복: 회복은 R인데 N이 나오면 N 조각(10)
                outcome = inventory.Apply(new GachaPullResult(GachaRarity.N, DroneType.Heal, false, 3));
                inventory.TryGetInfo(DroneType.Heal, out info);
                fails += Check(outcome.outcome == PullOutcome.Duplicate && outcome.shardsGained == 10 && info.rarity == GachaRarity.R && info.shards == 10,
                    $"낮은 등급(N) 중복은 등급 유지, 뽑힌 등급의 조각 +{outcome.shardsGained}");

                // 등급별 조각 환산표 (N 10 / R 20 / SR 30 / SSR 50)
                fails += Check(gacha.GetDuplicateShards(GachaRarity.N) == 10 && gacha.GetDuplicateShards(GachaRarity.R) == 20 &&
                               gacha.GetDuplicateShards(GachaRarity.SR) == 30 && gacha.GetDuplicateShards(GachaRarity.SSR) == 50,
                    "조각 환산표 N10 / R20 / SR30 / SSR50이 결정 기록과 일치");

                // 승급: 저격 N 보유 중 SR이 나오면 SR로 승급, 조각은 받지 않음
                outcome = inventory.Apply(new GachaPullResult(GachaRarity.SR, DroneType.Sniper, false, 4));
                inventory.TryGetInfo(DroneType.Sniper, out info);
                fails += Check(outcome.outcome == PullOutcome.Promoted && outcome.previousRarity == GachaRarity.N && outcome.shardsGained == 0 && info.rarity == GachaRarity.SR && info.level == 1,
                    "더 높은 등급(SR)이 나오면 승급 (저격 N → SR, 조각 없음)");

                // ---- 강화 ----
                // 근접: 조각 10 보유, Lv1→2에 조각 30 + 크레딧 300 필요
                fails += Check(inventory.GetUpgradeShardCost(DroneType.Melee) == 30 && inventory.GetUpgradeCreditCost(DroneType.Melee) == 300,
                    "Lv1→2 강화 비용: 조각 30 + 크레딧 300");
                fails += Check(inventory.TryUpgrade(DroneType.Melee, currency) == UpgradeResult.NotEnoughShards, "조각이 모자라면 강화 실패(NotEnoughShards)");
                fails += Check(inventory.TryUpgrade(DroneType.Collector, currency) == UpgradeResult.NotOwned, "미보유 드론은 강화 불가(NotOwned)");

                for (int i = 0; i < 2; i++) inventory.Apply(new GachaPullResult(GachaRarity.N, DroneType.Melee, false, 5)); // 조각 10 + 20 = 30
                inventory.TryGetInfo(DroneType.Melee, out info);
                int creditBefore = currency.Credit;
                fails += Check(info.shards == 30 && currency.Credit == 0 && inventory.TryUpgrade(DroneType.Melee, currency) == UpgradeResult.NotEnoughCredit,
                    "크레딧이 모자라면 강화 실패(NotEnoughCredit)");
                inventory.TryGetInfo(DroneType.Melee, out info);
                fails += Check(info.shards == 30 && info.level == 1 && currency.Credit == creditBefore, "강화 실패 시 조각·크레딧·레벨이 그대로");

                currency.AddCredit(300);
                fails += Check(inventory.TryUpgrade(DroneType.Melee, currency) == UpgradeResult.Success, "조각 30 + 크레딧 300이면 강화 성공");
                inventory.TryGetInfo(DroneType.Melee, out info);
                fails += Check(info.level == 2 && info.shards == 0 && currency.Credit == 0, $"강화 후 Lv{info.level}, 조각 {info.shards}, 크레딧 {currency.Credit}");

                // 승급해도 강화 레벨은 유지
                inventory.Apply(new GachaPullResult(GachaRarity.R, DroneType.Melee, false, 6));
                inventory.TryGetInfo(DroneType.Melee, out info);
                fails += Check(info.rarity == GachaRarity.R && info.level == 2, "승급(N→R)해도 강화 레벨 2 유지");

                // 최대 레벨까지: 필요한 조각·크레딧을 넉넉히 넣고 끝까지 올린다
                data.ownedDrones.Find(o => o.droneType == DroneType.Melee).shards = 999;
                currency.AddCredit(99999);
                UpgradeResult last = UpgradeResult.Success;
                while (last == UpgradeResult.Success) last = inventory.TryUpgrade(DroneType.Melee, currency);
                inventory.TryGetInfo(DroneType.Melee, out info);
                fails += Check(last == UpgradeResult.MaxLevel && info.level == growth.MaxLevel && growth.MaxLevel == 5, $"최대 레벨 {info.level}에서 더 강화 불가(MaxLevel)");
                fails += Check(inventory.GetUpgradeShardCost(DroneType.Melee) == 0, "최대 레벨의 다음 강화 비용은 0");

                // ---- 전투력 ----
                // 근접 R Lv5: 50 × 1.10 × (1 + 0.06×4) = 68.2, 저격 SR Lv1: 50 × 1.25 = 62.5 → 합 130.7 → 131
                fails += Check(inventory.TotalCombatPower == 131, $"전투력 = 근접 R Lv5(68.2) + 저격 SR Lv1(62.5) = 131 (실제 {inventory.TotalCombatPower})");

                // 성능 배율은 등급 배율 × 강화 배율로 나뉘어 읽힌다 (격납고가 "등급 ×1.10 · 강화 ×1.24"처럼 나눠 보여줄 때 쓴다)
                fails += Check(Mathf.Approximately(inventory.GetGradeMultiplier(DroneType.Melee), 1.1f) &&
                               Mathf.Approximately(inventory.GetLevelMultiplier(DroneType.Melee), 1.24f) &&
                               Mathf.Approximately(inventory.GetStatMultiplier(DroneType.Melee), 1.1f * 1.24f) &&
                               inventory.GetStatMultiplier(DroneType.Collector) == 0f,
                    $"근접 R Lv5 배율: 등급 ×{inventory.GetGradeMultiplier(DroneType.Melee):0.00} × 강화 ×{inventory.GetLevelMultiplier(DroneType.Melee):0.00} = ×{inventory.GetStatMultiplier(DroneType.Melee):0.000}, 미보유는 0");

                // SSR Lv5 두 개면 약 180
                var ssrData = new SaveData();
                var ssrCurrency = currencyObject.AddComponent<CurrencyManager>();
                ssrCurrency.Initialize(ssrData, currencyTable, false);
                var ssrInventory = inventoryObject.AddComponent<DroneInventory>();
                ssrInventory.Initialize(ssrData, growth, gacha, false);
                foreach (var owned in ssrData.ownedDrones) { owned.rarity = GachaRarity.SSR; owned.level = 5; }
                fails += Check(ssrInventory.TotalCombatPower == 180, $"SSR Lv5 두 개의 전투력 = 180 (실제 {ssrInventory.TotalCombatPower})");

                // ---- 장착 ----
                int before = changedCount;
                fails += Check(!inventory.TryEquip(0, DroneType.Collector), "미보유 드론은 장착 불가");
                fails += Check(!inventory.TryEquip(5, DroneType.Melee) && !inventory.TryEquip(-1, DroneType.Melee), "없는 슬롯 번호는 장착 불가");
                fails += Check(inventory.TryEquip(1, DroneType.Heal) && inventory.GetEquipped(1) == DroneType.Heal, "슬롯 2에 회복 장착");

                // 이미 슬롯 1에 있는 근접을 슬롯 2에 끼우면 서로 자리를 바꾼다
                fails += Check(inventory.TryEquip(1, DroneType.Melee) && inventory.GetEquipped(1) == DroneType.Melee && inventory.GetEquipped(0) == DroneType.Heal,
                    "이미 장착 중인 드론을 다른 슬롯에 끼우면 서로 자리 교체 (같은 드론이 두 슬롯에 들어가지 않음)");
                fails += Check(inventory.Unequip(0) && inventory.GetEquipped(0) == null && !inventory.Unequip(0), "슬롯 비우기 (이미 빈 슬롯은 false)");
                fails += Check(inventory.TotalCombatPower == Mathf.RoundToInt(inventory.GetPower(DroneType.Melee)), "빈 슬롯은 전투력에 더해지지 않음");
                fails += Check(changedCount > before, $"장착 변경 시 OnInventoryChanged 이벤트 발생 ({changedCount - before}회)");

                // ---- 실제 뽑기 흐름: GachaSystem 결과를 계속 반영해도 불변 조건이 지켜진다 ----
                var flowData = new SaveData();
                var flowInventory = new GameObject("DroneInventoryVerifier_Flow") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<DroneInventory>();
                flowInventory.Initialize(flowData, growth, gacha, false);
                var gachaSystem = new GachaSystem(gacha, new System.Random(2026));
                bool invariantsOk = true;
                var bestRarity = new GachaRarity[Enum.GetValues(typeof(DroneType)).Length];
                foreach (var owned in flowData.ownedDrones) bestRarity[(int)owned.droneType] = owned.rarity;
                for (int i = 0; i < 2000 && invariantsOk; i++)
                {
                    var pull = gachaSystem.PullSingle();
                    flowInventory.Apply(pull);
                    flowInventory.TryGetInfo(pull.drone, out info);
                    invariantsOk = info.rarity >= bestRarity[(int)pull.drone];   // 등급은 내려가지 않는다
                    bestRarity[(int)pull.drone] = info.rarity;
                }
                var seen = new System.Collections.Generic.HashSet<DroneType>();
                foreach (var owned in flowData.ownedDrones) invariantsOk &= seen.Add(owned.droneType); // 종류당 1개만
                fails += Check(invariantsOk && flowData.ownedDrones.Count == 5, $"뽑기 2,000회 반영 후에도 종류당 1개, 등급은 내려가지 않음 (보유 {flowData.ownedDrones.Count}종)");
                UnityEngine.Object.DestroyImmediate(flowInventory.gameObject);

                // ---- 저장 형식 ----
                string json = JsonUtility.ToJson(data);
                var loaded = JsonUtility.FromJson<SaveData>(json);
                bool same = loaded.ownedDrones.Count == data.ownedDrones.Count && loaded.equippedDrones.Count == data.equippedDrones.Count && loaded.isInventoryInitialized;
                for (int i = 0; same && i < data.ownedDrones.Count; i++)
                {
                    var a = data.ownedDrones[i]; var b = loaded.ownedDrones[i];
                    same = a.droneType == b.droneType && a.rarity == b.rarity && a.level == b.level && a.shards == b.shards;
                }
                for (int i = 0; same && i < data.equippedDrones.Count; i++) same = data.equippedDrones[i] == loaded.equippedDrones[i];
                fails += Check(same, "JSON 저장/불러오기 왕복 일치 (보유 드론, 등급, 레벨, 조각, 장착)");

                var oldSave = JsonUtility.FromJson<SaveData>("{\"saveVersion\":1,\"isCurrencyInitialized\":true,\"core\":100,\"credit\":50}");
                var oldObject = new GameObject("DroneInventoryVerifier_Old") { hideFlags = HideFlags.HideAndDontSave };
                var oldInventory = oldObject.AddComponent<DroneInventory>();
                oldInventory.Initialize(oldSave, growth, gacha, false);
                fails += Check(oldInventory.OwnedCount == 2 && oldInventory.TotalCombatPower == 100, "드론 항목이 없는 옛 저장 파일도 시작 드론을 받고 정상 시작");
                UnityEngine.Object.DestroyImmediate(oldObject);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inventoryObject);
                UnityEngine.Object.DestroyImmediate(currencyObject);
            }

            if (fails == 0) Debug.Log($"[Inventory] 검증 완료: 모든 항목 통과 ({DateTime.Now:HH:mm:ss})");
            else Debug.LogError($"[Inventory] 검증 완료: {fails}개 항목 실패 ({DateTime.Now:HH:mm:ss})");
        }

        private static int Check(bool ok, string message)
        {
            if (ok) Debug.Log($"[Inventory] 통과 — {message}");
            else Debug.LogError($"[Inventory] 실패 — {message}");
            return ok ? 0 : 1;
        }
    }
}
