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
            if (!EditorSafety.CanRunEditModeTool("Verify Inventory")) return;

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

                // 기본 최대 레벨(Lv5)까지: 필요한 조각·크레딧을 넉넉히 넣고 Lv5가 될 때까지 올린다.
                // (실제 에셋의 각성 스위치가 켜져 있으면 Lv5 다음에도 각성 강화가 가능하다. 그래서 스위치와 상관없이 같은 기준으로
                //  검사하려고 "전체 최대 레벨"이 아니라 "기본 최대 레벨"에서 멈춘다. 각성 쪽은 아래 각성 검사 구역에서 따로 확인한다.)
                data.ownedDrones.Find(o => o.droneType == DroneType.Melee).shards = 999;
                currency.AddCredit(99999);
                UpgradeResult last = UpgradeResult.Success;
                inventory.TryGetInfo(DroneType.Melee, out info);
                while (info.level < growth.BaseMaxLevel && last == UpgradeResult.Success)
                {
                    last = inventory.TryUpgrade(DroneType.Melee, currency);
                    inventory.TryGetInfo(DroneType.Melee, out info);
                }
                fails += Check(last == UpgradeResult.Success && info.level == growth.BaseMaxLevel && growth.BaseMaxLevel == 5, $"기본 최대 레벨 Lv{info.level}까지 강화");
                if (!growth.AwakeningEnabled)
                {
                    // 각성이 꺼져 있으면 Lv5가 끝이다(지금 CBT 빌드와 같은 동작).
                    last = inventory.TryUpgrade(DroneType.Melee, currency);
                    fails += Check(last == UpgradeResult.MaxLevel && growth.MaxLevel == 5, $"최대 레벨 {info.level}에서 더 강화 불가(MaxLevel)");
                    fails += Check(inventory.GetUpgradeShardCost(DroneType.Melee) == 0, "최대 레벨의 다음 강화 비용은 0");
                }
                else
                {
                    // 각성이 켜져 있으면 Lv5의 다음 강화(Lv6)는 각성 단계이고 비용이 0이 아니다.
                    fails += Check(growth.IsAwakeningLevel(info.level + 1) && inventory.GetUpgradeShardCost(DroneType.Melee) > 0,
                        $"각성 켜짐: Lv{info.level}의 다음 강화는 각성 단계(조각 {inventory.GetUpgradeShardCost(DroneType.Melee)})");
                }

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

                // ---- 만능 조각 (하드 스테이지 마일스톤 보상): 다음 강화에 가장 가까운 드론에 들어간다 ----
                var shardData = new SaveData();
                var shardInventory = new GameObject("DroneInventoryVerifier_Universal") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<DroneInventory>();
                try
                {
                    shardInventory.Initialize(shardData, growth, gacha, false);
                    // 근접은 조각 20(다음 강화까지 10 남음), 저격은 0(30 남음) → 가장 조금 모자란 근접에 전부 들어간다
                    shardData.ownedDrones.Find(o => o.droneType == DroneType.Melee).shards = 20;
                    int given = shardInventory.AddUniversalShards(60, out DroneType? usedTarget);
                    shardInventory.TryGetInfo(DroneType.Melee, out DroneInfo meleeInfo);
                    shardInventory.TryGetInfo(DroneType.Sniper, out DroneInfo sniperInfo);
                    fails += Check(given == 60 && usedTarget == DroneType.Melee && meleeInfo.shards == 80 && sniperInfo.shards == 0,
                        $"만능 조각 +60 → 다음 강화에 가장 가까운 드론(근접)에 들어감 (근접 {meleeInfo.shards}, 저격 {sniperInfo.shards})");
                    fails += Check(shardInventory.AddUniversalShards(0, out usedTarget) == 0 && usedTarget == null && shardInventory.AddUniversalShards(-5, out usedTarget) == 0,
                        "만능 조각 0 이하는 아무 일도 없음");
                    foreach (var owned in shardData.ownedDrones) owned.level = growth.MaxLevel;
                    given = shardInventory.AddUniversalShards(100, out usedTarget);
                    fails += Check(given == 0 && usedTarget == null, "받을 드론이 없으면(전부 최대 레벨) 만능 조각은 들어가지 않고 0을 돌려줌");

                    // ---- 하드 마일스톤 수령: 로비 마일스톤 창의 "획득"이 부르는 CurrencyManager.TryClaimMilestone ----
                    foreach (var owned in shardData.ownedDrones) owned.level = 1;
                    var claimData = shardData;
                    claimData.ownedDrones.Find(o => o.droneType == DroneType.Melee).shards = 20;
                    claimData.ownedDrones.Find(o => o.droneType == DroneType.Sniper).shards = 0;
                    var claimCurrency = new GameObject("DroneInventoryVerifier_Claim") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<CurrencyManager>();
                    try
                    {
                        claimCurrency.Initialize(claimData, currencyTable, false);
                        int coreBeforeClaim = claimCurrency.Core;
                        fails += Check(claimCurrency.TryClaimMilestone(5, 0, shardInventory) == 0 && !MatchMilestones.IsClaimed(claimData, 5, 0),
                            "하드 마일스톤: 달성하지 않았으면 받을 수 없음");
                        MatchMilestones.MarkDone(claimData, 5, 0);
                        int claimed = claimCurrency.TryClaimMilestone(5, 0, shardInventory);
                        shardInventory.TryGetInfo(DroneType.Melee, out DroneInfo claimInfo);
                        fails += Check(claimed == 5 && claimInfo.shards == 25 && MatchMilestones.IsClaimed(claimData, 5, 0) && claimCurrency.Core == coreBeforeClaim,
                            $"하드 1 3분 마일스톤 → 만능 조각 +5가 다음 강화에 가장 가까운 근접에 들어감 (근접 {claimInfo.shards}), 코어는 그대로");
                        fails += Check(claimCurrency.TryClaimMilestone(5, 0, shardInventory) == 0, "같은 하드 마일스톤은 한 번만 받음");
                        MatchMilestones.MarkDone(claimData, 5, MatchMilestones.ClearIndex);
                        fails += Check(claimCurrency.TryClaimMilestone(5, MatchMilestones.ClearIndex, shardInventory) == 15, "하드 1 클리어 마일스톤 = 조각 +15");
                        fails += Check(claimData.playLog.FindAll(l => l.Contains("| hard_milestone_claim |") && l.Contains("스테이지=6") && l.Contains("하드=1") && l.Contains("대상=Melee")).Count == 2,
                            "하드 마일스톤 수령은 플레이 기록에 항목마다 한 줄씩 남음");
                        fails += Check(claimData.logStats.milestoneClaims == 0 && claimData.logStats.milestoneCoreTotal == 0, "하드 조각 수령은 코어 마일스톤 통계에 섞이지 않음");

                        // 드론 보관함이 없으면(메인 메뉴를 거치지 않음) 받은 것으로 기록하지 않는다
                        MatchMilestones.MarkDone(claimData, 6, 0);
                        fails += Check(claimCurrency.TryClaimMilestone(6, 0) == 0 && !MatchMilestones.IsClaimed(claimData, 6, 0),
                            "드론 보관함이 없으면 하드 마일스톤을 받지 못하고, 받은 것으로 기록되지 않음");

                        // 받을 드론이 없으면(전부 최대 레벨) 조각은 0이지만 받은 것으로 기록한다
                        foreach (var owned in claimData.ownedDrones) owned.level = growth.MaxLevel;
                        fails += Check(claimCurrency.TryClaimMilestone(6, 0, shardInventory) == 0 && MatchMilestones.IsClaimed(claimData, 6, 0)
                                       && claimData.playLog.Exists(l => l.Contains("| hard_milestone_claim |") && l.Contains("하드=2") && l.Contains("조각=+0") && l.Contains("대상=없음")),
                            "전부 최대 레벨이면 하드 마일스톤은 조각 0으로 받은 것 처리 (기록에 대상=없음)");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(claimCurrency.gameObject);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(shardInventory.gameObject);
                }

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

                // ---- 조각 교환 (크레딧 → 조각) ----
                // 결정(10/6): 조각 10개 = 크레딧 500, 하루 3번. 하루 한도는 날짜가 바뀌면(기기 날짜 기준) 새로 시작한다.
                fails += Check(growth.ShardExchangeShards == 10 && growth.ShardExchangeCredit == 500 && growth.ShardExchangeDailyLimit == 3,
                    $"조각 교환 수치표 = 조각 {growth.ShardExchangeShards} / 크레딧 {growth.ShardExchangeCredit} / 하루 {growth.ShardExchangeDailyLimit}번 (결정: 10 / 500 / 3)");

                var exData = new SaveData();
                var exCurrencyObject = new GameObject("DroneInventoryVerifier_ExCurrency") { hideFlags = HideFlags.HideAndDontSave };
                var exInventoryObject = new GameObject("DroneInventoryVerifier_ExInventory") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    var exCurrency = exCurrencyObject.AddComponent<CurrencyManager>();
                    exCurrency.Initialize(exData, currencyTable, false);
                    var exInventory = exInventoryObject.AddComponent<DroneInventory>();
                    exInventory.Initialize(exData, growth, gacha, false);
                    const string day1 = "2026-10-01", day2 = "2026-10-02";

                    int creditBeforeEx = exCurrency.Credit;
                    var notOwned = exInventory.TryExchangeShards(DroneType.Collector, exCurrency, day1);
                    var noCredit = exInventory.TryExchangeShards(DroneType.Melee, exCurrency, day1);
                    exInventory.TryGetInfo(DroneType.Melee, out info);
                    fails += Check(notOwned == ExchangeResult.NotOwned && noCredit == ExchangeResult.NotEnoughCredit && info.shards == 0
                                   && exCurrency.Credit == creditBeforeEx && exInventory.GetExchangeCountToday(day1) == 0,
                        "미보유 드론은 교환 불가, 크레딧이 모자라면 실패하고 조각·크레딧·오늘 횟수가 그대로");

                    exCurrency.AddCredit(2000);
                    var r1 = exInventory.TryExchangeShards(DroneType.Melee, exCurrency, day1);
                    var r2 = exInventory.TryExchangeShards(DroneType.Melee, exCurrency, day1);
                    var r3 = exInventory.TryExchangeShards(DroneType.Melee, exCurrency, day1);
                    exInventory.TryGetInfo(DroneType.Melee, out info);
                    fails += Check(r1 == ExchangeResult.Success && r2 == ExchangeResult.Success && r3 == ExchangeResult.Success
                                   && info.shards == 30 && exCurrency.Credit == 500 && exInventory.GetExchangeCountToday(day1) == 3,
                        $"하루 3번 교환 성공: 조각 {info.shards}, 크레딧 {exCurrency.Credit}, 오늘 {exInventory.GetExchangeCountToday(day1)}/3");

                    var r4 = exInventory.TryExchangeShards(DroneType.Melee, exCurrency, day1);
                    exInventory.TryGetInfo(DroneType.Melee, out info);
                    fails += Check(r4 == ExchangeResult.DailyLimit && info.shards == 30 && exCurrency.Credit == 500,
                        "크레딧이 충분해도 4번째는 하루 한도(DailyLimit)로 막히고 조각·크레딧이 그대로");

                    var next = exInventory.TryExchangeShards(DroneType.Melee, exCurrency, day2);
                    exInventory.TryGetInfo(DroneType.Melee, out info);
                    fails += Check(next == ExchangeResult.Success && info.shards == 40 && exCurrency.Credit == 0 && exInventory.GetExchangeCountToday(day2) == 1,
                        $"다음 날(날짜 변경)이면 횟수가 새로 시작해서 다시 교환 가능 (조각 {info.shards}, 오늘 {exInventory.GetExchangeCountToday(day2)}/3)");

                    exData.ownedDrones.Find(o => o.droneType == DroneType.Melee).level = growth.MaxLevel;
                    exCurrency.AddCredit(500);
                    var maxed = exInventory.TryExchangeShards(DroneType.Melee, exCurrency, day2);
                    fails += Check(maxed == ExchangeResult.MaxLevel && exCurrency.Credit == 500 && exInventory.GetExchangeCountToday(day2) == 1,
                        "최대 레벨 드론은 교환 불가(MaxLevel), 크레딧·횟수가 그대로");

                    var s = exData.logStats;
                    fails += Check(s.shardExchanges == 4 && s.shardsBought == 40 && s.creditSpentOnShards == 2000
                                   && exData.playLog.Exists(l => l.Contains("| shard_exchange |") && l.Contains("드론=Melee 조각=+10 사용크레딧=500 오늘=1/3")),
                        $"테스트 로그에 교환 기록: {s.shardExchanges}번, 조각 {s.shardsBought}, 크레딧 {s.creditSpentOnShards}");
                    fails += Check(PlayLog.BuildSummaryText(exData).Contains("조각 교환 4번 (조각 40개, 크레딧 2,000 사용)"), "요약에 조각 교환 줄이 나온다");

                    var exLoaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(exData));
                    fails += Check(exLoaded.shardExchangeDate == day2 && exLoaded.shardExchangeCount == 1, "교환 횟수·날짜가 JSON 저장/불러오기 후에도 유지");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(exCurrencyObject);
                    UnityEngine.Object.DestroyImmediate(exInventoryObject);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inventoryObject);
                UnityEngine.Object.DestroyImmediate(currencyObject);
            }

            fails += VerifyAwakening(growth, gacha, currencyTable);

            if (fails == 0) Debug.Log($"[Inventory] 검증 완료: 모든 항목 통과 ({DateTime.Now:HH:mm:ss})");
            else Debug.LogError($"[Inventory] 검증 완료: {fails}개 항목 실패 ({DateTime.Now:HH:mm:ss})");
        }

        // 각성 단계(OBT 2단계, 기본 꺼짐) 검증. 실제 에셋을 복제해서 스위치를 끈 것/켠 것으로 나눠 검사하므로
        // 실제 에셋의 스위치 상태와 상관없이 같은 결과가 나온다.
        private static int VerifyAwakening(DroneGrowthTable growth, GachaTable gacha, CurrencyTable currencyTable)
        {
            int fails = 0;
            Debug.Log($"[Inventory] 안내 — 각성 단계 스위치(실제 에셋): {(growth.AwakeningEnabled ? "켜짐" : "꺼짐")} (OBT 2단계 전에는 꺼짐이 정상)");

            var off = UnityEngine.Object.Instantiate(growth);
            var on = UnityEngine.Object.Instantiate(growth);
            var currencyObject = new GameObject("AwakeningVerifier_Currency") { hideFlags = HideFlags.HideAndDontSave };
            var inventoryObject = new GameObject("AwakeningVerifier_Inventory") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                SetAwakening(off, false);
                SetAwakening(on, true);

                fails += Check(off.MaxLevel == 5 && off.BaseMaxLevel == 5 && off.GetUpgradeShardCost(5) == 0 && off.GetUpgradeCreditCost(5) == 0 && !off.IsAwakeningLevel(6),
                    $"각성 꺼짐: 최대 레벨 {off.MaxLevel}, Lv5에서는 더 올릴 수 없음 (지금 게임과 같음)");
                fails += Check(on.MaxLevel == 8 && on.BaseMaxLevel == 5, $"각성 켜짐: 최대 레벨 {on.MaxLevel} (기본 {on.BaseMaxLevel} + 각성 3)");
                fails += Check(on.GetUpgradeShardCost(1) == off.GetUpgradeShardCost(1) && on.GetUpgradeShardCost(4) == off.GetUpgradeShardCost(4)
                               && on.GetUpgradeCreditCost(4) == off.GetUpgradeCreditCost(4),
                    "Lv1~5 강화 비용은 각성이 켜져도 같다");
                fails += Check(on.GetUpgradeShardCost(5) == 150 && on.GetUpgradeShardCost(6) == 200 && on.GetUpgradeShardCost(7) == 250 && on.GetUpgradeShardCost(8) == 0,
                    $"각성 조각 비용: Lv5→6 {on.GetUpgradeShardCost(5)}, 6→7 {on.GetUpgradeShardCost(6)}, 7→8 {on.GetUpgradeShardCost(7)}, 8→ {on.GetUpgradeShardCost(8)}");
                fails += Check(on.GetUpgradeCreditCost(5) == 1500 && on.GetUpgradeCreditCost(6) == 2000 && on.GetUpgradeCreditCost(7) == 2500 && on.GetUpgradeCreditCost(8) == 0,
                    $"각성 크레딧 비용: {on.GetUpgradeCreditCost(5)} / {on.GetUpgradeCreditCost(6)} / {on.GetUpgradeCreditCost(7)}");
                fails += Check(!on.IsAwakeningLevel(5) && on.IsAwakeningLevel(6) && on.IsAwakeningLevel(8) && !on.IsAwakeningLevel(9),
                    "각성 레벨 판별: 6~8만 각성 단계");

                // ---- 켜짐 상태에서 Lv5 → Lv8 강화 흐름 ----
                var data = new SaveData();
                var currency = currencyObject.AddComponent<CurrencyManager>();
                currency.Initialize(data, currencyTable, false);
                var inventory = inventoryObject.AddComponent<DroneInventory>();
                inventory.Initialize(data, on, gacha, false);
                var melee = data.ownedDrones.Find(o => o.droneType == DroneType.Melee);
                melee.level = 5;
                melee.shards = 600;
                currency.AddCredit(6000);

                var r1 = inventory.TryUpgrade(DroneType.Melee, currency);
                var r2 = inventory.TryUpgrade(DroneType.Melee, currency);
                var r3 = inventory.TryUpgrade(DroneType.Melee, currency);
                var r4 = inventory.TryUpgrade(DroneType.Melee, currency);
                DroneInfo info;
                inventory.TryGetInfo(DroneType.Melee, out info);
                fails += Check(r1 == UpgradeResult.Success && r2 == UpgradeResult.Success && r3 == UpgradeResult.Success && r4 == UpgradeResult.MaxLevel && info.level == 8,
                    $"켜짐: Lv5 → 8까지 강화 3번 성공 후 4번째는 최대 레벨 (Lv{info.level})");
                fails += Check(info.shards == 0 && currency.Credit == 0, $"각성 3번에 조각 600·크레딧 6,000이 정확히 쓰임 (남은 조각 {info.shards}, 크레딧 {currency.Credit})");
                fails += Check(Mathf.Abs(inventory.GetLevelMultiplier(DroneType.Melee) - (1f + on.LevelBonusPerLevel * 7f)) < 0.001f,
                    $"Lv8 강화 배율 ×{inventory.GetLevelMultiplier(DroneType.Melee):0.00} = 1 + 레벨당 보너스 × 7 (각성 레벨에도 이어짐)");

                // ---- 꺼짐 상태에서는 Lv5가 끝 ----
                var offData = new SaveData();
                var offCurrencyObject = new GameObject("AwakeningVerifier_OffCurrency") { hideFlags = HideFlags.HideAndDontSave };
                var offInventoryObject = new GameObject("AwakeningVerifier_OffInventory") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    var offCurrency = offCurrencyObject.AddComponent<CurrencyManager>();
                    offCurrency.Initialize(offData, currencyTable, false);
                    var offInventory = offInventoryObject.AddComponent<DroneInventory>();
                    offInventory.Initialize(offData, off, gacha, false);
                    var offMelee = offData.ownedDrones.Find(o => o.droneType == DroneType.Melee);
                    offMelee.level = 5;
                    offMelee.shards = 600;
                    offCurrency.AddCredit(6000);
                    fails += Check(offInventory.TryUpgrade(DroneType.Melee, offCurrency) == UpgradeResult.MaxLevel && offMelee.level == 5 && offMelee.shards == 600,
                        "꺼짐: Lv5에서 강화하면 최대 레벨로 거절되고 조각·레벨이 그대로");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(offCurrencyObject);
                    UnityEngine.Object.DestroyImmediate(offInventoryObject);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(off);
                UnityEngine.Object.DestroyImmediate(on);
                UnityEngine.Object.DestroyImmediate(currencyObject);
                UnityEngine.Object.DestroyImmediate(inventoryObject);
            }
            return fails;
        }

        // 복제한 성장표의 각성 스위치를 켜거나 끈다 (private 칸이라 SerializedObject로 건드린다).
        private static void SetAwakening(DroneGrowthTable table, bool enabled)
        {
            var so = new SerializedObject(table);
            so.FindProperty("_awakeningEnabled").boolValue = enabled;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int Check(bool ok, string message)
        {
            if (ok) Debug.Log($"[Inventory] 통과 — {message}");
            else Debug.LogError($"[Inventory] 실패 — {message}");
            return ok ? 0 : 1;
        }
    }
}
