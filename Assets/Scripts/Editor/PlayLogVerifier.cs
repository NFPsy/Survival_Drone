using System;
using SurvivalDrone.Drones;
using SurvivalDrone.Enemies;
using SurvivalDrone.LevelUp;
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

                // ---- 판 도중에 나간 기록 (일시정지 메뉴의 재시작·메인메뉴) ----
                var leave = new SaveData();
                PlayLog.RecordAbandon(leave, 1, 95f, 120.5f, 6, 3, 110, "메인메뉴");
                PlayLog.RecordAbandon(leave, 2, 35f, 40f, 2, 2, 110, "재시작");
                fails += Check(leave.playLog[0] == "10-09 13:05:07 | match_abandon | 스테이지=1 나간방법=메인메뉴 생존=95.0초 실제소요=120.5초 도달레벨=6 드론수=3 내전투력=110",
                    $"이탈 기록 문장 형식: '{leave.playLog[0]}'");
                fails += Check(leave.logStats.abandons == 2 && Mathf.Approximately(leave.logStats.abandonSurviveSeconds, 130f) && leave.logStats.matches == 0 && leave.logStats.timedMatches == 0,
                    "이탈 누적: 2번, 나간 시점 합계 130초, 끝난 판 수(matches)에는 안 들어감");
                string leaveSummary = PlayLog.BuildSummaryText(leave);
                fails += Check(leaveSummary.Contains("판 도중에 나감 2번 (평균 1:05 시점") && leaveSummary.Contains("판 수 0"),
                    "요약: 판 도중에 나감 2번, 평균 1:05 시점 (판 수 0은 그대로)");
                fails += Check(!PlayLog.BuildSummaryText(new SaveData()).Contains("판 도중에 나감"), "이탈이 없으면 요약에 '판 도중에 나감' 줄이 없음");
                var leaveLoaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(leave));
                fails += Check(leaveLoaded.logStats.abandons == 2 && Mathf.Approximately(leaveLoaded.logStats.abandonSurviveSeconds, 130f),
                    "JSON 저장/불러오기 왕복 후에도 이탈 누적이 그대로");
                fails += Check(oldSave.logStats.abandons == 0, "이탈 항목이 없는 옛 저장 파일은 이탈 0번으로 읽힘");

                // ---- 판 시작 기록, 마일스톤·일일 퀘스트 수령 기록 ----
                var startLog = new SaveData();
                PlayLog.RecordMatchStart(startLog, 2, 129, "Melee SR Lv3 / Sniper N Lv1");
                PlayLog.RecordMilestoneClaim(startLog, 2, "3분", 150, 2850);
                PlayLog.RecordQuestClaim(startLog, "출석하기", 150, 3000);
                fails += Check(startLog.playLog[0] == "10-09 13:05:07 | match_start | 스테이지=2 내전투력=129 장착=Melee SR Lv3 / Sniper N Lv1",
                    $"판 시작 기록 문장 형식: '{startLog.playLog[0]}'");
                fails += Check(startLog.playLog[1] == "10-09 13:05:07 | milestone_claim | 스테이지=2 항목=3분 코어=+150 남은코어=2850",
                    $"마일스톤 수령 기록 문장 형식: '{startLog.playLog[1]}'");
                fails += Check(startLog.playLog[2] == "10-09 13:05:07 | quest_claim | 퀘스트=출석하기 코어=+150 남은코어=3000",
                    $"일일 퀘스트 수령 기록 문장 형식: '{startLog.playLog[2]}'");
                fails += Check(startLog.logStats.matches == 0 && startLog.logStats.milestoneClaims == 1 && startLog.logStats.milestoneCoreTotal == 150
                               && startLog.logStats.questClaims == 1 && startLog.logStats.questCoreTotal == 150,
                    "판 시작은 판 수(matches)에 안 들어가고, 수령 누적은 횟수·코어 합계가 쌓임");
                fails += Check(PlayLog.BuildSummaryText(startLog).Contains("코어 수령: 마일스톤 1번 (+150) · 일일 퀘스트 1번 (+150)"), "요약: 코어 수령 한 줄");
                fails += Check(!PlayLog.BuildSummaryText(new SaveData()).Contains("코어 수령"), "수령이 없으면 요약에 '코어 수령' 줄이 없음");
                var startLoaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(startLog));
                fails += Check(startLoaded.logStats.milestoneClaims == 1 && startLoaded.logStats.questCoreTotal == 150 && oldSave.logStats.milestoneClaims == 0 && oldSave.logStats.questClaims == 0,
                    "JSON 왕복 후에도 수령 누적이 그대로, 수령 항목이 없는 옛 저장 파일은 0으로 읽힘");

                // ---- 레벨업 선택 요약 (판 기록 끝에 덧붙는 문장) ----
                LevelUpPickLog.Reset();
                fails += Check(LevelUpPickLog.BuildSummary() == "", "레벨업 선택이 하나도 없으면 요약이 빈 글자 (기록에 아무것도 덧붙지 않음)");
                LevelUpPickLog.Record(new LevelUpOption { Kind = LevelUpOptionKind.StatBoost, StatBoost = StatBoostKind.MoveSpeed }, 45.4f);
                LevelUpPickLog.Record(new LevelUpOption { Kind = LevelUpOptionKind.StatBoost, StatBoost = StatBoostKind.MoveSpeed }, 80f);
                LevelUpPickLog.Record(new LevelUpOption { Kind = LevelUpOptionKind.StatBoost, StatBoost = StatBoostKind.MaxHealth }, 100f);
                LevelUpPickLog.Record(new LevelUpOption { Kind = LevelUpOptionKind.NewDrone }, 120f);
                LevelUpPickLog.Record(new LevelUpOption { Kind = LevelUpOptionKind.UpgradeDrone }, 150f);
                LevelUpPickLog.Record(new LevelUpOption { Kind = LevelUpOptionKind.UpgradeDrone }, 160f);
                string picks = LevelUpPickLog.BuildSummary();
                fails += Check(picks == "레벨업선택=이동속도2/체력1/신규드론1/드론강화2 이동속도첫선택=45초", $"레벨업 선택 요약 문장: '{picks}'");
                var withPicks = new SaveData();
                PlayLog.RecordMatch(withPicks, 1, true, 600f, 689f, 14, 5, 100, 0, 300, picks);
                PlayLog.RecordAbandon(withPicks, 1, 50f, 60f, 3, 2, 100, "재시작", picks);
                fails += Check(withPicks.playLog[0].EndsWith("보상크레딧=300 " + picks) && withPicks.playLog[1].EndsWith("내전투력=100 " + picks),
                    "판 결과·이탈 기록 끝에 레벨업 선택 요약이 덧붙음");
                LevelUpPickLog.Reset();
                LevelUpPickLog.Record(new LevelUpOption { Kind = LevelUpOptionKind.UpgradeDrone }, 30f);
                fails += Check(LevelUpPickLog.BuildSummary().EndsWith("이동속도첫선택=없음"), "이동속도를 한 번도 안 골랐으면 '이동속도첫선택=없음'");
                LevelUpPickLog.Reset();

                // ---- 사망 원인·받은 피해 요약 (판 기록 끝에 덧붙는 문장) ----
                DamageSourceLog.Reset();
                fails += Check(DamageSourceLog.BuildSummary(true) == "" && DamageSourceLog.BuildSummary(false) == "",
                    "한 번도 안 맞았으면 요약이 빈 글자 (기록에 아무것도 덧붙지 않음)");
                DamageSourceLog.Record("약한", 6f);
                DamageSourceLog.Record("빠른", 6f);
                DamageSourceLog.Record("튼튼한", 10f);
                DamageSourceLog.Record("빠른", 12f);   // 오버드라이브 중이라 2배로 맞은 경우. 가장 마지막에 맞은 종류 = 빠른
                DamageSourceLog.Record("", 5f);        // 이름이 없거나
                DamageSourceLog.Record("강한", 0f);    // 피해가 0이면 무시
                string lostDamage = DamageSourceLog.BuildSummary(true);
                string wonDamage = DamageSourceLog.BuildSummary(false);
                fails += Check(lostDamage == "사망원인=빠른 받은피해=빠른18/튼튼한10/약한6", $"패배 요약: 사망원인(마지막으로 맞은 종류) + 종류별 피해 합계(큰 순서): '{lostDamage}'");
                fails += Check(wonDamage == "받은피해=빠른18/튼튼한10/약한6", $"승리 요약: 사망원인 없이 받은 피해만: '{wonDamage}'");
                fails += Check(DamageSourceLog.LabelOf(EnemyKind.Fast, false) == "빠른" && DamageSourceLog.LabelOf(EnemyKind.Boss, false) == "보스"
                               && DamageSourceLog.LabelOf(EnemyKind.Boss, true) == "미니보스",
                    "적 종류 이름 (보스 데이터를 쓰는 미니 보스는 '미니보스'로 따로 구분)");
                var withDamage = new SaveData();
                PlayLog.RecordMatch(withDamage, 3, false, 165.6f, 178.7f, 8, 4, 129, 0, 176, null, lostDamage);
                PlayLog.RecordMatch(withDamage, 3, false, 70f, 80f, 5, 3, 129, 0, 10, "레벨업선택=이동속도1/체력0/신규드론1/드론강화0 이동속도첫선택=30초", lostDamage);
                PlayLog.RecordMatch(withDamage, 1, true, 600f, 689f, 14, 5, 100, 0, 300);
                fails += Check(withDamage.playLog[0].EndsWith("보상크레딧=176 " + lostDamage), "판 기록 끝에 사망원인·받은 피해가 덧붙음 (레벨업 선택 요약이 없을 때)");
                fails += Check(withDamage.playLog[1].EndsWith("이동속도첫선택=30초 " + lostDamage), "레벨업 선택 요약이 있으면 그 뒤에 이어서 덧붙음");
                fails += Check(withDamage.playLog[2].EndsWith("보상크레딧=300"), "요약을 안 넘기는 옛 방식 호출은 기록 모양이 그대로");
                DamageSourceLog.Reset();
                fails += Check(DamageSourceLog.BuildSummary(true) == "", "Reset 하면 지난 판의 맞은 기록이 지워짐");

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

                // ---- 장착 요약, 실제 매니저의 마일스톤·퀘스트 수령 기록 ----
                // 이 시점의 장착: 슬롯 1 = 저격(근접과 자리를 바꿨다가 슬롯 2를 비움). 등급은 뽑기 결과로 바뀔 수 있어 모양만 본다.
                string loadout = inventory.BuildLoadoutSummary();
                fails += Check(loadout.StartsWith("Sniper ") && loadout.Contains(" Lv1") && loadout.EndsWith(" / 비움"), $"장착 요약 모양: '{loadout}'");

                MatchMilestones.MarkDone(flowData, 0, 0);
                int coreBeforeClaim = flowData.core;
                currency.TryClaimMilestone(0, 0);
                currency.TryClaimMilestone(0, 0);   // 이미 받은 것: 기록이 늘면 안 됨
                currency.TryClaimMilestone(0, 1);   // 달성하지 않은 것: 기록이 늘면 안 됨
                DailyQuests.MarkAttendance(flowData);
                currency.TryClaimDailyQuest(DailyQuests.AttendanceIndex);
                currency.TryClaimDailyQuest(DailyQuests.AttendanceIndex);
                int mileLines = flowData.playLog.FindAll(l => l.Contains("| milestone_claim |")).Count;
                int questLines = flowData.playLog.FindAll(l => l.Contains("| quest_claim |")).Count;
                int expectedCore = coreBeforeClaim + MatchMilestones.GetCore(0, 0) + DailyQuests.Cores[DailyQuests.AttendanceIndex];
                fails += Check(mileLines == 1 && questLines == 1 && flowData.core == expectedCore
                               && flowData.playLog.Exists(l => l.Contains("| milestone_claim |") && l.Contains("항목=3분") && l.EndsWith($"남은코어={coreBeforeClaim + MatchMilestones.GetCore(0, 0)}"))
                               && flowData.playLog.Exists(l => l.Contains("| quest_claim |") && l.Contains("퀘스트=출석하기") && l.EndsWith($"남은코어={expectedCore}")),
                    $"실제 수령 흐름이 한 번씩만 기록 (마일스톤 {mileLines}줄, 퀘스트 {questLines}줄, 남은 코어가 실제 보유와 같음)");
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
