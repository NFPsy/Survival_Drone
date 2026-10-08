using System;
using System.Collections.Generic;
using System.IO;
using SurvivalDrone.Drones;
using SurvivalDrone.Meta;
using SurvivalDrone.UI;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 저장 슬롯(SaveManager의 슬롯 모드)이 규칙대로 동작하는지 확인하는 에디터 전용 검증 도구.
    // 메뉴 SurvivalDrone → Meta → Verify Save Slots 로 실행하면 결과가 콘솔에 [Save] 접두사로 찍힌다.
    //
    // 진짜 저장 파일(persistentDataPath)은 절대 건드리지 않는다: SaveManager의 시험용 스위치(SetTestOverrides)로
    // 임시 폴더와 "슬롯 켜짐/꺼짐"을 강제로 정해서 검사하고, 끝나면 모두 원래대로 돌려 놓는다.
    //
    // 검사 항목:
    //  1) 슬롯이 꺼져 있으면 예전처럼 save.json 하나만 쓰고 슬롯 선택은 거절된다
    //  2) 슬롯이 켜져 있어도 슬롯을 고르기 전에는 아무 파일도 쓰지 않는다 (임시 데이터)
    //  3) 예전 save.json이 슬롯 1로 복사되고, 원래 파일은 그대로 남는다
    //  4) 슬롯을 고르면 그 슬롯 파일을 읽고 쓰며, 슬롯끼리 서로 섞이지 않는다
    //  5) 새 슬롯은 처음 값으로 시작하고 테스터 번호만 다른 슬롯과 같다
    //  6) 범위 밖 번호는 거절된다, 깨진 파일은 .broken으로 백업된다
    //  7) 슬롯을 고르면 DataReplaced 이벤트가 발생하고, 재화·보유 드론·스테이지·뽑기 매니저가 새 슬롯 데이터로 다시 연결된다
    //  8) 모든 슬롯의 기록이 "=== 슬롯 N ===" 구역으로 합쳐서 내보내진다
    public static class SaveSlotVerifier
    {
        [MenuItem("SurvivalDrone/Meta/Verify Save Slots")]
        public static void Verify()
        {
            if (!EditorSafety.CanRunEditModeTool("Verify Save Slots")) return;

            var gachaTable = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Meta/GachaTable.asset");
            var currencyTable = AssetDatabase.LoadAssetAtPath<CurrencyTable>("Assets/Data/Meta/CurrencyTable.asset");
            var growthTable = AssetDatabase.LoadAssetAtPath<DroneGrowthTable>("Assets/Data/Meta/DroneGrowthTable.asset");
            var stages = new[]
            {
                AssetDatabase.LoadAssetAtPath<StageData>("Assets/Data/Meta/StageData_01.asset"),
                AssetDatabase.LoadAssetAtPath<StageData>("Assets/Data/Meta/StageData_02.asset"),
                AssetDatabase.LoadAssetAtPath<StageData>("Assets/Data/Meta/StageData_03.asset"),
            };
            if (gachaTable == null || currencyTable == null || growthTable == null || stages[0] == null)
            {
                Debug.LogError("[Save] 슬롯 검증 실패: 수치표 에셋(GachaTable / CurrencyTable / DroneGrowthTable / StageData)을 찾을 수 없습니다.");
                return;
            }

            string dir = Path.Combine(Path.GetTempPath(), "SurvivalDroneSlotTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                int fails = 0;
                fails += CheckSlotsOff(dir);
                fails += CheckSlotsOn(dir, gachaTable, currencyTable, growthTable, stages);
                fails += CheckBrokenFile(dir);

                if (fails == 0) Debug.Log($"[Save] 슬롯 검증 완료: 모든 항목 통과 ({DateTime.Now:HH:mm:ss})");
                else Debug.LogError($"[Save] 슬롯 검증 완료: {fails}개 항목 실패 ({DateTime.Now:HH:mm:ss})");
            }
            finally
            {
                SaveManager.ClearTestOverrides();
                try { Directory.Delete(dir, true); } catch (Exception) { /* 임시 폴더는 못 지워도 괜찮다 */ }
            }
        }

        // 예전 저장 파일(save.json) 하나를 만든다. 슬롯 1로 옮겨질 내용이다.
        private static SaveData MakeLegacyData()
        {
            var data = new SaveData
            {
                core = 1234, credit = 50, isCurrencyInitialized = true, isInventoryInitialized = true,
                testerId = "ABC123", unlockedStageCount = 2, gachaPityCount = 12,
            };
            data.ownedDrones.Add(new OwnedDroneData { droneType = DroneType.Melee, rarity = GachaRarity.N, level = 1, shards = 0 });
            data.ownedDrones.Add(new OwnedDroneData { droneType = DroneType.Sniper, rarity = GachaRarity.SR, level = 2, shards = 5 });
            data.ownedDrones.Add(new OwnedDroneData { droneType = DroneType.Heal, rarity = GachaRarity.R, level = 1, shards = 0 });
            data.equippedDrones.Add((int)DroneType.Melee);
            data.equippedDrones.Add((int)DroneType.Sniper);
            data.playLog.Add("10-08 12:00:00 | session_start | tester=ABC123");
            data.logStats.matches = 4;
            return data;
        }

        // ---------------- 1) 슬롯이 꺼져 있을 때 ----------------
        private static int CheckSlotsOff(string dir)
        {
            int fails = 0;
            string subDir = Path.Combine(dir, "off");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "save.json"), JsonUtility.ToJson(MakeLegacyData()));

            SaveManager.SetTestOverrides(subDir, false, 3);
            fails += Check(!SaveManager.SlotsEnabled && SaveManager.HasSelectedSlot, "슬롯이 꺼져 있으면 슬롯을 고르지 않아도 저장할 곳이 정해진 상태");
            fails += Check(SaveManager.Data.core == 1234 && SaveManager.FilePath.EndsWith("save.json"), "슬롯이 꺼져 있으면 예전처럼 save.json을 읽음");
            SaveManager.Data.core = 4321;
            SaveManager.Save();
            fails += Check(JsonUtility.FromJson<SaveData>(File.ReadAllText(Path.Combine(subDir, "save.json"))).core == 4321, "슬롯이 꺼져 있으면 save.json에 저장함");
            fails += Check(!SaveManager.SelectSlot(1, out _), "슬롯이 꺼져 있으면 슬롯 선택이 거절됨");
            fails += Check(Directory.GetFiles(subDir, "save_slot*").Length == 0, "슬롯이 꺼져 있으면 슬롯 파일이 만들어지지 않음");
            return fails;
        }

        // ---------------- 2~8) 슬롯이 켜져 있을 때 ----------------
        private static int CheckSlotsOn(string dir, GachaTable gachaTable, CurrencyTable currencyTable, DroneGrowthTable growthTable, StageData[] stages)
        {
            int fails = 0;
            string subDir = Path.Combine(dir, "on");
            Directory.CreateDirectory(subDir);
            string legacyPath = Path.Combine(subDir, "save.json");
            File.WriteAllText(legacyPath, JsonUtility.ToJson(MakeLegacyData()));
            string legacyBefore = File.ReadAllText(legacyPath);

            SaveManager.SetTestOverrides(subDir, true, 3);
            int replaced = 0;
            Action onReplaced = () => replaced++;
            SaveManager.DataReplaced += onReplaced;
            var objects = new List<GameObject>();
            try
            {
                // 2) 슬롯을 고르기 전: 임시 데이터이고 아무 파일도 쓰지 않는다
                fails += Check(SaveManager.SlotsEnabled && !SaveManager.HasSelectedSlot && SaveManager.CurrentSlot == 0, "슬롯을 고르기 전에는 저장할 곳이 정해지지 않은 상태");
                var placeholder = SaveManager.Data;
                placeholder.core = 999;
                SaveManager.Save();
                fails += Check(Directory.GetFiles(subDir, "save_slot*").Length == 0 && File.ReadAllText(legacyPath) == legacyBefore, "슬롯을 고르기 전에는 저장해도 어떤 파일도 쓰지 않음");

                // 3) 예전 save.json → 슬롯 1 복사
                var peek1 = SaveManager.PeekSlot(1);
                fails += Check(peek1 != null && peek1.core == 1234 && File.Exists(SaveManager.SlotPath(1)), "예전 save.json이 슬롯 1로 복사됨 (코어 1234)");
                fails += Check(File.ReadAllText(legacyPath) == legacyBefore, "원래 save.json은 그대로 남음 (백업)");
                fails += Check(SaveManager.PeekSlot(2) == null && SaveManager.PeekSlot(3) == null, "슬롯 2·3은 비어 있음");

                // 4) 슬롯 1 선택
                bool ok = SaveManager.SelectSlot(1, out bool created);
                fails += Check(ok && !created && SaveManager.CurrentSlot == 1 && SaveManager.HasSelectedSlot, "슬롯 1 선택: 이어서 하기 (새로 시작 아님)");
                fails += Check(SaveManager.Data.core == 1234 && SaveManager.Data.unlockedStageCount == 2 && replaced == 1, $"슬롯 1의 내용을 불러옴, DataReplaced 이벤트 {replaced}번");
                SaveManager.Data.core = 2000;
                SaveManager.Save();
                fails += Check(JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveManager.SlotPath(1))).core == 2000, "슬롯 1에 저장하면 슬롯 1 파일에 쓰임");
                fails += Check(File.ReadAllText(legacyPath) == legacyBefore, "슬롯 1에 저장해도 예전 save.json은 안 바뀜");
                fails += Check(!string.IsNullOrEmpty(SaveManager.Data.lastSavedAt), "저장하면 마지막 저장 시각이 기록됨");

                // 5) 새 슬롯 2
                ok = SaveManager.SelectSlot(2, out created);
                fails += Check(ok && created && SaveManager.CurrentSlot == 2 && File.Exists(SaveManager.SlotPath(2)), "슬롯 2 선택: 새로 시작하고 바로 파일이 만들어짐");
                fails += Check(SaveManager.Data.core == 0 && !SaveManager.Data.isCurrencyInitialized && SaveManager.Data.ownedDrones.Count == 0, "새 슬롯은 처음 값에서 시작 (시작 재화·드론은 매니저가 지급)");
                fails += Check(SaveManager.Data.testerId == "ABC123", "새 슬롯의 테스터 번호는 다른 슬롯과 같음 (ABC123)");
                SaveManager.Data.core = 777;
                SaveManager.Save();

                // 슬롯끼리 섞이지 않는다
                SaveManager.SelectSlot(1, out _);
                fails += Check(SaveManager.Data.core == 2000, "슬롯 1로 돌아오면 슬롯 1의 값 (2000, 슬롯 2의 777이 아님)");
                SaveManager.SelectSlot(2, out created);
                fails += Check(!created && SaveManager.Data.core == 777, "슬롯 2로 돌아오면 슬롯 2의 값 (777)");
                SaveManager.SelectSlot(3, out created);
                fails += Check(created && SaveManager.Data.core == 0, "슬롯 3 새로 시작");
                fails += Check(SaveManager.PeekAllSlots().Count == 3, "내용이 있는 슬롯 3개가 모두 보임");

                // 6) 범위 밖
                int slotBefore = SaveManager.CurrentSlot;
                fails += Check(!SaveManager.SelectSlot(0, out _) && !SaveManager.SelectSlot(4, out _) && SaveManager.CurrentSlot == slotBefore, "0번·4번 슬롯은 거절되고 지금 슬롯은 그대로");

                // 7) 매니저 재연결 (씬 없이 Initialize한 매니저에 Rebind를 직접 불러 DataReplaced 처리와 같은 일을 시킨다)
                GameObject Make(string name) { var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; objects.Add(go); return go; }
                SaveManager.SelectSlot(1, out _);
                var data1 = SaveManager.Data;
                var currency = Make("SlotVerifier_Currency").AddComponent<CurrencyManager>();
                currency.Initialize(data1, currencyTable, false);
                var inventory = Make("SlotVerifier_Inventory").AddComponent<DroneInventory>();
                inventory.Initialize(data1, growthTable, gachaTable, false);
                var stage = Make("SlotVerifier_Stage").AddComponent<StageProgress>();
                stage.Initialize(data1, stages, false);
                var gacha = Make("SlotVerifier_Gacha").AddComponent<GachaController>();
                gacha.Initialize(data1, gachaTable, currency, inventory, new System.Random(1), false);
                fails += Check(currency.Core == 2000 && inventory.OwnedCount == 3 && stage.UnlockedCount == 2 && gacha.PityCount == 12, "슬롯 1에 연결: 코어 2000 / 드론 3종 / 해금 스테이지 2 / 천장 12");

                SaveManager.SelectSlot(3, out created);
                var data3 = SaveManager.Data;
                currency.Rebind(data3);
                inventory.Rebind(data3);
                stage.Rebind(data3);
                gacha.Rebind(data3);
                fails += Check(currency.Core == currencyTable.StartCore, $"슬롯 3(새 슬롯)로 다시 연결: 시작 코어 {currencyTable.StartCore} 지급");
                fails += Check(inventory.OwnedCount == 2, "슬롯 3(새 슬롯)로 다시 연결: 시작 드론 2종 지급");
                fails += Check(stage.UnlockedCount == 1 && gacha.PityCount == 0, "슬롯 3(새 슬롯)로 다시 연결: 해금 스테이지 1, 천장 0 (슬롯 1의 값이 새지 않음)");
                fails += Check(data1.core == 2000 && data1.ownedDrones.Count == 3, "슬롯 1의 데이터 객체는 슬롯 3 작업 중에도 바뀌지 않음");

                SaveManager.SelectSlot(1, out _);
                var again = SaveManager.Data;
                currency.Rebind(again);
                inventory.Rebind(again);
                stage.Rebind(again);
                gacha.Rebind(again);
                fails += Check(currency.Core == 2000 && inventory.OwnedCount == 3 && stage.UnlockedCount == 2 && gacha.PityCount == 12, "슬롯 1로 다시 돌아오면 슬롯 1의 값으로 다시 연결");

                // 8) 기록 합쳐서 내보내기, 슬롯 요약
                string export = PlayLog.BuildMultiSlotExportText(SaveManager.PeekAllSlots(), "0.2.0", "Test");
                fails += Check(export.Contains("=== 슬롯 1 ===") && export.Contains("=== 슬롯 2 ===") && export.Contains("=== 슬롯 3 ==="), "내보내기에 슬롯 1·2·3 구역이 모두 있음");
                fails += Check(export.Contains("ABC123") && export.Contains("session_start"), "내보내기에 테스터 번호와 슬롯 1의 기록이 들어 있음");
                string summary = SaveSlotSelectUI.BuildSummary(SaveManager.PeekSlot(1));
                fails += Check(summary.Contains("2,000") && summary.Contains("드론 3종") && summary.Contains("플레이 4판"), "슬롯 요약에 코어·드론 수·플레이 판 수가 나옴");
            }
            finally
            {
                SaveManager.DataReplaced -= onReplaced;
                foreach (var go in objects) UnityEngine.Object.DestroyImmediate(go);
            }
            return fails;
        }

        // ---------------- 6) 깨진 파일 ----------------
        private static int CheckBrokenFile(string dir)
        {
            int fails = 0;
            string subDir = Path.Combine(dir, "broken");
            Directory.CreateDirectory(subDir);
            SaveManager.SetTestOverrides(subDir, true, 3);
            File.WriteAllText(SaveManager.SlotPath(2), "{{{ 깨진 파일");

            bool ok = SaveManager.SelectSlot(2, out bool created);
            fails += Check(ok && !created, "깨진 슬롯 파일도 게임이 멈추지 않고 열림 (새 데이터로 시작)");
            fails += Check(File.Exists(SaveManager.SlotPath(2) + ".broken"), "깨진 파일은 덮어쓰기 전에 .broken으로 백업됨");
            return fails;
        }

        private static int Check(bool ok, string description)
        {
            if (ok) Debug.Log($"[Save] ✔ {description}");
            else Debug.LogError($"[Save] ✘ {description}");
            return ok ? 0 : 1;
        }
    }
}
