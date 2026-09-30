using System;
using SurvivalDrone.Drones;
using SurvivalDrone.Meta;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 테스트(CBT) 기록(PlayLog)이 규칙대로 쌓이고 요약되는지 확인하는 에디터 전용 검증 도구.
    // 메뉴 SurvivalDrone → Meta → Verify PlayLog 로 실행하면 결과가 콘솔에 [Save] 접두사로 찍힌다.
    // 진짜 저장 파일은 건드리지 않고 임시 데이터로만 검사한다.
    public static class PlayLogVerifier
    {
        [MenuItem("SurvivalDrone/Meta/Verify PlayLog")]
        public static void Verify()
        {
            if (!EditorSafety.CanRunEditModeTool("Verify PlayLog")) return;

            var growth = AssetDatabase.LoadAssetAtPath<DroneGrowthTable>("Assets/Data/Meta/DroneGrowthTable.asset");
            var gacha = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Meta/GachaTable.asset");
            var currencyTable = AssetDatabase.LoadAssetAtPath<CurrencyTable>("Assets/Data/Meta/CurrencyTable.asset");
            var stages = new StageData[3];
            for (int i = 0; i < 3; i++) stages[i] = AssetDatabase.LoadAssetAtPath<StageData>($"Assets/Data/Meta/StageData_0{i + 1}.asset");
            if (growth == null || gacha == null || currencyTable == null || stages[0] == null || stages[2] == null)
            {
                Debug.LogError("[Save] 기록 검증 실패: 수치표 에셋을 찾을 수 없습니다.");
                return;
            }

            var previousClock = PlayLog.Clock;
            var objects = new System.Collections.Generic.List<GameObject>();
            int fails = 0;
            try
            {
                // 시각을 고정해서 기록 문장을 정확히 비교한다
                var fixedTime = new DateTime(2026, 10, 9, 13, 5, 7);
                PlayLog.Clock = () => fixedTime;

                var data = new SaveData();

                // ---- 세션 시작 / 화면 ----
                PlayLog.StartSession(data, "0.1.0", "WebGLPlayer", "960x600");
                fails += Check(data.testerId.Length == 6 && data.logStats.sessions == 1, $"세션 시작: 테스터 번호 '{data.testerId}'(6자리), 접속 1회");
                string firstId = data.testerId;
                PlayLog.StartSession(data, "0.1.0", "WebGLPlayer", "960x600");
                fails += Check(data.testerId == firstId && data.logStats.sessions == 2, "다시 접속해도 테스터 번호가 그대로, 접속 횟수만 증가");
                PlayLog.RecordScreen(data, "Lobby");
                fails += Check(data.playLog[data.playLog.Count - 1] == "10-09 13:05:07 | screen | Lobby", $"화면 기록 문장 형식: '{data.playLog[data.playLog.Count - 1]}'");

                // ---- 요약: 아직 아무것도 안 했을 때 ----
                string empty = PlayLog.BuildSummaryText(data);
                fails += Check(empty.Contains("판 수 0") && empty.Contains("아직 뽑기를 한 번도 안 함"), "판·뽑기가 없을 때 요약이 '판 수 0 / 아직 뽑기를 한 번도 안 함'");

                // ---- 판 결과 (첫 뽑기 전에 2판) ----
                PlayLog.RecordMatch(data, 1, false, 125.4f, 140f, 6, 2, 100, 15, 80);
                PlayLog.RecordMatch(data, 1, true, 600f, 689f, 14, 5, 100, 40, 200);

                // ---- 뽑기 (10연 1번, 1회 1번, 막힘 1번) ----
                PlayLog.RecordPull(data, true, 2700, new[] { 6, 3, 1, 0 }, 3, 2, 60, 0, 10, 70);
                PlayLog.RecordPull(data, false, 300, new[] { 0, 0, 0, 1 }, 0, 0, 50, 0, 0, 70);
                PlayLog.RecordPullBlocked(data, false, 300, 0);
                PlayLog.RecordUpgrade(data, "Melee", 2, 116);
                PlayLog.RecordEquip(data, 2, "Heal", 122);
                PlayLog.RecordEquip(data, 2, null, 100);
                PlayLog.RecordUnlock(data, 2);

                var s = data.logStats;
                fails += Check(s.matches == 2 && s.clears == 1 && Mathf.Approximately(s.totalSurviveSeconds, 725.4f) && s.rewardCoreTotal == 55 && s.rewardCreditTotal == 280,
                    $"판 누적: {s.matches}판, 클리어 {s.clears}, 생존 합계 {s.totalSurviveSeconds}초, 보상 코어 {s.rewardCoreTotal}/크레딧 {s.rewardCreditTotal}");
                fails += Check(s.pullsTotal == 11 && s.tenPullActions == 1 && s.singlePullActions == 1 && s.ssrTotal == 1 && s.coreSpentOnPulls == 3000 && s.blockedPulls == 1,
                    $"뽑기 누적: 총 {s.pullsTotal}회(10연 {s.tenPullActions}, 1회 {s.singlePullActions}), SSR {s.ssrTotal}, 사용 코어 {s.coreSpentOnPulls}, 막힘 {s.blockedPulls}");
                fails += Check(s.matchesBeforeFirstPull == 2, $"첫 뽑기 전에 플레이한 판 수 = {s.matchesBeforeFirstPull} (기대 2)");
                fails += Check(s.upgrades == 1, "강화 횟수 누적 1");

                // ---- 요약 문장 ----
                string summary = PlayLog.BuildSummaryText(data);
                fails += Check(summary.Contains("판 수 2 (클리어 1, 클리어율 50.0%)"), "요약: 판 수 2, 클리어율 50.0%");
                fails += Check(summary.Contains("실패한 판 평균 생존: 2:05 (1판") && !summary.Contains("평균 생존 6:02"),
                    "요약: 실패한 판 평균 생존 2:05 (클리어한 판 600초가 섞인 평균은 더 이상 보여주지 않음)");
                fails += Check(summary.Contains("판당 평균 실제 소요 시간: 6:54 (2판") && summary.Contains("클리어한 판 평균 실제 소요 시간: 11:29 (1판"),
                    "요약: 판당 평균 실제 소요 6:54 (140초·689초 평균), 클리어한 판 11:29");
                fails += Check(!summary.Contains("※"), "모든 판에 시간 기록이 있으면 '※ 시간 통계는 일부만' 안내가 없음");
                fails += Check(summary.Contains("판당 평균 보상: 코어 27.5 / 크레딧 140.0"), "요약: 판당 평균 보상 코어 27.5 / 크레딧 140.0");
                fails += Check(summary.Contains("뽑기 총 11회 (1회 1번, 10연 1번) · SSR 1개") && summary.Contains("첫 뽑기 전에 플레이한 판 수: 2"), "요약: 뽑기 총 11회, SSR 1개, 첫 뽑기 전 2판");

                // ---- 내보내기 글 ----
                string export = PlayLog.BuildExportText(data, "0.1.0", "WebGLPlayer");
                fails += Check(export.Contains("=== 드론 지휘관 테스트 기록 ===") && export.Contains($"테스터: {firstId}") && export.Contains("내보낸 시각: 2026-10-09 13:05:07"),
                    "내보내기 머리말: 제목, 테스터 번호, 내보낸 시각");
                fails += Check(export.Contains("| match | 스테이지=1 결과=클리어 생존=600.0초 실제소요=689.0초 도달레벨=14 드론수=5 내전투력=100 보상코어=40 보상크레딧=200") &&
                               export.Contains("| pull | 종류=10연 사용코어=2700 N=6 R=3 SR=1 SSR=0 신규=3 승급=2 조각=60 남은코어=0 천장=10/70") &&
                               export.Contains("| equip | 슬롯=2 드론=비움 내전투력=100") && export.Contains("| unlock | 스테이지=2"),
                    "내보내기 본문에 판·뽑기·장착·해금 기록이 모두 들어 있음");

                // ---- 저장 형식 ----
                string json = JsonUtility.ToJson(data);
                var loaded = JsonUtility.FromJson<SaveData>(json);
                fails += Check(loaded.playLog.Count == data.playLog.Count && loaded.testerId == data.testerId && loaded.logStats.matches == 2 && loaded.logStats.matchesBeforeFirstPull == 2
                               && loaded.logStats.timedMatches == 2 && Mathf.Approximately(loaded.logStats.timedRealSeconds, 829f),
                    "JSON 저장/불러오기 왕복 후에도 기록·테스터 번호·누적 숫자가 그대로");
                var oldSave = JsonUtility.FromJson<SaveData>("{\"saveVersion\":1,\"core\":100}");
                fails += Check(oldSave.playLog != null && oldSave.logStats != null && oldSave.logStats.matchesBeforeFirstPull == -1 && oldSave.testerId == "",
                    "기록 항목이 없는 옛 저장 파일도 안전하게 읽힘 (첫 뽑기 전 판 수 = -1)");

                // ---- 옛 기록(시간 기록이 없는 판)이 섞여 있을 때 ----
                var legacy = new SaveData();
                legacy.logStats.matches = 3;   // 예전에 기록된 3판(클리어 2): 실제 소요 시간 없음
                legacy.logStats.clears = 2;
                PlayLog.RecordMatch(legacy, 1, false, 100f, 130f, 5, 2, 100, 15, 80);
                string legacySummary = PlayLog.BuildSummaryText(legacy);
                fails += Check(legacySummary.Contains("판 수 4 (클리어 2, 클리어율 50.0%)") && legacySummary.Contains("실패한 판 평균 생존: 1:40 (1판") &&
                               legacySummary.Contains("판당 평균 실제 소요 시간: 2:10 (1판") && legacySummary.Contains("※ 시간 통계는 실제 소요 시간 기록이 생긴 뒤의 1판만 집계 (전체 4판)"),
                    "옛 판이 섞여도 시간 통계는 새로 기록된 판만 집계하고 그 사실을 안내 (1판 기준: 실패 생존 1:40, 실제 소요 2:10)");
                var noFail = new SaveData();
                PlayLog.RecordMatch(noFail, 1, true, 600f, 700f, 10, 5, 100, 40, 200);
                fails += Check(PlayLog.BuildSummaryText(noFail).Contains("실패한 판 평균 생존: 실패한 판 없음"), "실패한 판이 없으면 '실패한 판 없음'으로 표시");

                // ---- 최대 개수 ----
                var big = new SaveData();
                for (int i = 0; i < PlayLog.MaxEntries + 50; i++) PlayLog.RecordScreen(big, $"Scene{i}");
                fails += Check(big.playLog.Count == PlayLog.MaxEntries && big.playLog[0].EndsWith("Scene50") && big.playLog[big.playLog.Count - 1].EndsWith($"Scene{PlayLog.MaxEntries + 49}"),
                    $"기록이 {PlayLog.MaxEntries}개를 넘으면 오래된 것부터 지워짐 (처음 남은 것: Scene50)");

                // ---- 실제 매니저들이 기록을 남기는지 (임시 데이터로) ----
                var flowData = new SaveData();
                var currencyObject = new GameObject("PlayLogVerifier_Currency") { hideFlags = HideFlags.HideAndDontSave };
                var inventoryObject = new GameObject("PlayLogVerifier_Inventory") { hideFlags = HideFlags.HideAndDontSave };
                var gachaObject = new GameObject("PlayLogVerifier_Gacha") { hideFlags = HideFlags.HideAndDontSave };
                var stageObject = new GameObject("PlayLogVerifier_Stage") { hideFlags = HideFlags.HideAndDontSave };
                objects.AddRange(new[] { currencyObject, inventoryObject, gachaObject, stageObject });

                var currency = currencyObject.AddComponent<CurrencyManager>();
                currency.Initialize(flowData, currencyTable, false);
                var inventory = inventoryObject.AddComponent<DroneInventory>();
                inventory.Initialize(flowData, growth, gacha, false);
                var controller = gachaObject.AddComponent<GachaController>();
                controller.Initialize(flowData, gacha, currency, inventory, new System.Random(3), false);
                var stage = stageObject.AddComponent<StageProgress>();
                stage.Initialize(flowData, stages, false);

                controller.PullTen();                       // 코어 2,700 → 성공
                controller.PullSingle();                    // 코어 0 → 막힘
                inventory.TryEquip(1, DroneType.Melee);     // 근접을 슬롯 2로 옮김(저격과 자리 교체) → 장착 기록
                inventory.Unequip(1);                       // 해제 기록
                stage.RecordMatchResult(true, 600f);        // 클리어 → 스테이지 2 해금 기록

                bool hasPull = flowData.playLog.Exists(l => l.Contains("| pull |") && l.Contains("종류=10연"));
                bool hasBlocked = flowData.playLog.Exists(l => l.Contains("| pull_blocked |") && l.Contains("필요코어=300 보유코어=0"));
                bool hasUnequip = flowData.playLog.Exists(l => l.Contains("| equip |") && l.Contains("드론=비움"));
                bool hasUnlock = flowData.playLog.Exists(l => l.Contains("| unlock |") && l.Contains("스테이지=2"));
                fails += Check(hasPull && hasBlocked && hasUnequip && hasUnlock && flowData.logStats.pullsTotal == 10 && flowData.logStats.blockedPulls == 1,
                    $"실제 뽑기 흐름·재고·장착·해금이 기록을 남김 (뽑기 {flowData.logStats.pullsTotal}회, 막힘 {flowData.logStats.blockedPulls}번)");

                // ---- 강화 기록 ----
                var upgradeData = flowData; // 같은 인벤토리로 강화 성공 기록 확인
                var melee = upgradeData.ownedDrones.Find(o => o.droneType == DroneType.Melee);
                melee.shards = 999;
                currency.AddCredit(9999);
                inventory.TryUpgrade(DroneType.Melee, currency);
                fails += Check(upgradeData.playLog.Exists(l => l.Contains("| upgrade |") && l.Contains("드론=Melee 레벨=2")) && upgradeData.logStats.upgrades == 1,
                    "드론 강화 성공이 기록에 남음 (드론=Melee 레벨=2)");
            }
            finally
            {
                PlayLog.Clock = previousClock;
                foreach (var go in objects) UnityEngine.Object.DestroyImmediate(go);
            }

            if (fails == 0) Debug.Log($"[Save] 기록 검증 완료: 모든 항목 통과 ({DateTime.Now:HH:mm:ss})");
            else Debug.LogError($"[Save] 기록 검증 완료: {fails}개 항목 실패 ({DateTime.Now:HH:mm:ss})");
        }

        private static int Check(bool ok, string message)
        {
            if (ok) Debug.Log($"[Save] 통과 — {message}");
            else Debug.LogError($"[Save] 실패 — {message}");
            return ok ? 0 : 1;
        }
    }
}
