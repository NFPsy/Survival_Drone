using SurvivalDrone.Meta;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // StageProgress(스테이지 해금·선택·최고 기록)가 규칙대로 동작하는지 확인하는 에디터 전용 검증 도구.
    // 메뉴 SurvivalDrone → Meta → Verify Stage 로 실행하면 결과가 콘솔에 [Stage] 접두사로 찍힌다.
    // 진짜 저장 파일은 건드리지 않고 임시 데이터로만 검사한다.
    public static class StageProgressVerifier
    {
        private static readonly string[] StagePaths =
        {
            "Assets/Data/Meta/StageData_01.asset",
            "Assets/Data/Meta/StageData_02.asset",
            "Assets/Data/Meta/StageData_03.asset",
            "Assets/Data/Meta/StageData_04.asset",
            "Assets/Data/Meta/StageData_05.asset",
        };

        [MenuItem("SurvivalDrone/Meta/Verify Stage")]
        public static void Verify()
        {
            if (!EditorSafety.CanRunEditModeTool("Verify Stage")) return;

            var stages = new StageData[StagePaths.Length];
            for (int i = 0; i < StagePaths.Length; i++)
            {
                stages[i] = AssetDatabase.LoadAssetAtPath<StageData>(StagePaths[i]);
                if (stages[i] == null)
                {
                    Debug.LogError($"[Stage] 검증 실패: {StagePaths[i]} 를 찾을 수 없습니다.");
                    return;
                }
            }

            var go = new GameObject("StageProgressVerifier_Temp") { hideFlags = HideFlags.HideAndDontSave };
            int fails = 0;
            try
            {
                var progress = go.AddComponent<StageProgress>();
                var data = new SaveData();
                progress.Initialize(data, stages, false);

                // 1) 처음에는 스테이지 1만 열려 있고 그것이 선택되어 있다
                fails += Check(progress.UnlockedCount == 1 && progress.IsUnlocked(0) && !progress.IsUnlocked(1) && progress.SelectedIndex == 0,
                    $"처음에는 스테이지 1만 해금 (해금 {progress.UnlockedCount}개, 선택 {progress.SelectedIndex + 1}번)");

                // 2) 잠긴 스테이지는 고를 수 없다
                fails += Check(!progress.TrySelect(1) && progress.SelectedIndex == 0, "잠긴 스테이지 2는 선택 불가");
                fails += Check(!progress.TrySelect(-1) && !progress.TrySelect(99), "범위 밖 번호는 선택 불가");

                // 3) 실패하면 해금 없이 기록만 남는다
                progress.RecordMatchResult(false, 123.4f);
                fails += Check(progress.UnlockedCount == 1 && Mathf.Approximately(progress.GetBestSurvivalSeconds(0), 123.4f),
                    $"실패 시 해금 없음, 최고 생존 {progress.GetBestSurvivalSeconds(0):F1}초");

                // 4) 더 짧게 살면 최고 기록은 그대로
                progress.RecordMatchResult(false, 50f);
                fails += Check(Mathf.Approximately(progress.GetBestSurvivalSeconds(0), 123.4f), "더 짧은 기록은 최고 기록을 덮어쓰지 않음");

                // 5) 클리어하면 다음 스테이지가 열린다
                progress.RecordMatchResult(true, 600f);
                fails += Check(progress.UnlockedCount == 2 && progress.IsUnlocked(1) && Mathf.Approximately(progress.GetBestSurvivalSeconds(0), 600f),
                    $"스테이지 1 클리어 → 스테이지 2 해금 (해금 {progress.UnlockedCount}개)");

                // 6) 해금된 스테이지는 고를 수 있고, 배율이 그 스테이지 값으로 바뀐다
                int selectedEventCount = 0;
                progress.OnSelectionChanged += _ => selectedEventCount++;
                fails += Check(progress.TrySelect(1) && progress.SelectedIndex == 1 && selectedEventCount == 1 &&
                               Mathf.Approximately(progress.CurrentEnemyMultiplier, stages[1].EnemyMultiplier),
                    $"스테이지 2 선택 → 적 배율 x{progress.CurrentEnemyMultiplier}, 선택 이벤트 {selectedEventCount}회");

                // 7) 이미 열린 스테이지를 다시 클리어해도 해금 개수가 줄거나 늘지 않는다
                progress.TrySelect(0);
                progress.RecordMatchResult(true, 600f);
                fails += Check(progress.UnlockedCount == 2, $"이미 열린 다음 스테이지는 그대로 (해금 {progress.UnlockedCount}개)");

                // 8) 마지막 스테이지를 클리어해도 스테이지 수를 넘어서 해금되지 않는다
                // 스테이지 2, 3, 4, 5를 차례로 클리어한다 (마지막 스테이지 5를 클리어해도 더 열리지 않아야 한다)
                for (int s = 1; s < stages.Length; s++)
                {
                    progress.TrySelect(s);
                    progress.RecordMatchResult(true, 600f);
                }
                fails += Check(progress.UnlockedCount == stages.Length, $"마지막 스테이지 클리어 후에도 해금 최대 {stages.Length}개 (해금 {progress.UnlockedCount}개)");

                // 9) 저장 형식(JSON) 왕복
                string json = JsonUtility.ToJson(data);
                var loaded = JsonUtility.FromJson<SaveData>(json);
                bool same = loaded.unlockedStageCount == data.unlockedStageCount && loaded.bestSurvivalSeconds.Count == data.bestSurvivalSeconds.Count;
                for (int i = 0; same && i < data.bestSurvivalSeconds.Count; i++)
                    same = Mathf.Approximately(loaded.bestSurvivalSeconds[i], data.bestSurvivalSeconds[i]);
                fails += Check(same, $"JSON 저장/불러오기 왕복 일치: {json}");

                // 10) 옛 저장 파일(스테이지 항목이 없는 JSON)을 읽어도 스테이지 1만 열린 상태로 시작한다
                var oldSave = JsonUtility.FromJson<SaveData>("{\"saveVersion\":1,\"isCurrencyInitialized\":true,\"core\":100,\"credit\":50}");
                fails += Check(oldSave.unlockedStageCount == 1 && oldSave.bestSurvivalSeconds != null && oldSave.bestSurvivalSeconds.Count == 0,
                    "스테이지 항목이 없는 옛 저장 파일도 안전하게 읽힘");

                // 11) 스테이지 수치가 기획(노션 결정 기록)과 같은지: 권장 전투력 100/115/130/150/170, 배율 1.0/1.25/1.5/1.85/2.2
                //     (스테이지 1·3·5가 예전 3스테이지 때의 1·2·3번 값이고, 2·4번이 그 사이)
                int[] expectedPower = { 100, 115, 130, 150, 170 };
                float[] expectedMultiplier = { 1f, 1.25f, 1.5f, 1.85f, 2.2f };
                bool valuesOk = true;
                for (int i = 0; i < stages.Length; i++)
                    valuesOk &= stages[i].RecommendedPower == expectedPower[i] && Mathf.Approximately(stages[i].EnemyMultiplier, expectedMultiplier[i]) && stages[i].StageNumber == i + 1;
                fails += Check(valuesOk, "스테이지 데이터가 결정 기록과 일치 (권장 전투력 100/115/130/150/170, 적 배율 x1.0/x1.25/x1.5/x1.85/x2.2)");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            fails += VerifyHardStages(stages);

            if (fails == 0) Debug.Log($"[Stage] 검증 완료: 모든 항목 통과 ({System.DateTime.Now:HH:mm:ss})");
            else Debug.LogError($"[Stage] 검증 완료: {fails}개 항목 실패 ({System.DateTime.Now:HH:mm:ss})");
        }

        // 하드 스테이지(스테이지마다 하드 버전, 기본 꺼짐) 검증. 하드 스테이지 에셋을 직접 넘겨서 쓰므로 실제 스위치 상태와 상관없이 같은 결과가 나온다.
        private static int VerifyHardStages(StageData[] normal)
        {
            int fails = 0;
            var hard = new StageData[5];
            for (int i = 0; i < 5; i++)
            {
                string path = $"Assets/Data/Meta/StageData_{6 + i:00}.asset";
                hard[i] = AssetDatabase.LoadAssetAtPath<StageData>(path);
                if (hard[i] == null)
                {
                    Debug.LogError($"[Stage] 검증 실패: {path} 를 찾을 수 없습니다.");
                    return 1;
                }
            }

            var settings = Resources.Load<HardStageSettings>("HardStageSettings");
            Debug.Log($"[Stage] 안내 — 하드 스테이지 스위치(실제 에셋): {(settings != null && settings.Enabled ? "켜짐" : "꺼짐")} (OBT 전에는 꺼짐이 정상)");

            var go = new GameObject("StageProgressVerifier_HardTemp") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                // ---- 하드 목록을 안 넘기면(= 스위치 꺼짐) 예전과 똑같이 노멀 5개뿐 ----
                var off = go.AddComponent<StageProgress>();
                off.Initialize(new SaveData(), normal, false);
                fails += Check(off.StageCount == 5 && off.NormalStageCount == 5 && !off.HasHardStages && off.FirstHardIndex == -1 && !off.IsHardStage(4),
                    $"하드 꺼짐: 스테이지 {off.StageCount}개, 하드 없음 (예전과 같음)");

                // ---- 하드 목록을 넘기면(= 켜짐) 노멀 5개 뒤에 6~10번(하드 1~5)이 붙는다 ----
                var onObject = new GameObject("StageProgressVerifier_HardOn") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    var on = onObject.AddComponent<StageProgress>();
                    var data = new SaveData();
                    on.Initialize(data, normal, false, hard);
                    fails += Check(on.StageCount == 10 && on.NormalStageCount == 5 && on.HasHardStages && on.FirstHardIndex == 5 && !on.IsHardStage(4) && on.IsHardStage(5) && on.IsHardStage(9) && !on.IsHardStage(10),
                        $"하드 켜짐: 스테이지 {on.StageCount}개 (노멀 {on.NormalStageCount} + 하드 5), 하드 시작 위치 {on.FirstHardIndex}");
                    fails += Check(on.GetStage(5) == hard[0] && on.GetStage(9) == hard[4] && on.GetStage(5).StageNumber == 1 && on.GetStage(9).StageNumber == 5,
                        "하드 스테이지는 노멀 뒤에 붙고, 번호는 짝이 되는 노멀의 번호(1~5)를 씀");

                    // 처음엔 하드가 전부 잠겨 있고, 노멀 n을 클리어하면 하드 n만 열린다 (순서대로 열릴 필요 없음)
                    fails += Check(!on.IsUnlocked(5) && !on.IsUnlocked(9) && on.UnlockedCount == 1, "처음에는 하드가 전부 잠겨 있음");
                    on.TrySelect(0); on.RecordMatchResult(true, 600f);
                    fails += Check(on.IsUnlocked(5) && !on.IsUnlocked(6) && on.UnlockedCount == 2, "노멀 1 클리어 → 하드 1만 열림 (하드 2는 아직 잠김)");
                    fails += Check(on.IsNormalCleared(0) && !on.IsNormalCleared(1), "노멀 클리어 기록: 스테이지 1만 클리어");
                    on.TrySelect(1); on.RecordMatchResult(true, 600f);
                    on.TrySelect(2); on.RecordMatchResult(true, 600f);
                    fails += Check(on.IsUnlocked(5) && on.IsUnlocked(6) && on.IsUnlocked(7) && !on.IsUnlocked(8) && !on.IsUnlocked(9) && on.UnlockedCount == 4,
                        $"노멀 1~3 클리어 → 하드 1~3 열림, 하드 4·5는 잠김 (노멀 해금 {on.UnlockedCount}개)");

                    // 노멀을 클리어하지 않고 하드를 고를 수 없다
                    fails += Check(!on.TrySelect(9) && on.SelectedIndex == 2, "잠긴 하드(5)는 고를 수 없음");

                    // 마지막 노멀(5)을 클리어해도 노멀 해금 개수는 5를 넘지 않고, 하드 5가 열린다
                    on.TrySelect(3); on.RecordMatchResult(true, 600f);
                    on.TrySelect(4); on.RecordMatchResult(true, 600f);
                    fails += Check(on.UnlockedCount == 5 && on.IsUnlocked(9) && data.unlockedStageCount == 5,
                        $"노멀 5 클리어 → 하드 5 열림, 노멀 해금 개수는 최대 5 (저장값 {data.unlockedStageCount})");

                    // 하드를 클리어해도 노멀 해금 개수나 노멀 클리어 기록은 바뀌지 않는다
                    int maskBefore = data.normalClearedMask;
                    on.TrySelect(5); on.RecordMatchResult(true, 600f);
                    fails += Check(on.UnlockedCount == 5 && data.normalClearedMask == maskBefore, "하드 클리어는 노멀 해금·클리어 기록을 바꾸지 않음");

                    // ---- 옛 저장(클리어 기록 비트 없음)에서 하드 해금 짐작 ----
                    var oldObject = new GameObject("StageProgressVerifier_OldSave") { hideFlags = HideFlags.HideAndDontSave };
                    try
                    {
                        var oldData = new SaveData { unlockedStageCount = 3 };   // 예전에 스테이지 2까지 깬 저장
                        var o = oldObject.AddComponent<StageProgress>();
                        o.Initialize(oldData, normal, false, hard);
                        fails += Check(o.IsUnlocked(5) && o.IsUnlocked(6) && !o.IsUnlocked(7) && oldData.normalClearedMask == 0,
                            "옛 저장(해금 3개): 노멀 1·2를 깬 것으로 보고 하드 1·2가 열림");
                        var oldAll = new SaveData { unlockedStageCount = 5 };    // 스테이지 5까지 열린 옛 저장 (5를 깼는지는 알 수 없음)
                        o.Initialize(oldAll, normal, false, hard);
                        fails += Check(o.IsUnlocked(8) && !o.IsUnlocked(9), "옛 저장(해금 5개): 하드 1~4는 열리고 하드 5는 노멀 5를 한 번 더 깨야 열림");
                    }
                    finally
                    {
                        Object.DestroyImmediate(oldObject);
                    }

                    // 체력·피해 배율이 따로 적용된다 (노멀은 둘이 같은 값)
                    on.TrySelect(5);
                    fails += Check(Mathf.Approximately(on.CurrentHealthMultiplier, 1.5f) && Mathf.Approximately(on.CurrentDamageMultiplier, 1.6f),
                        $"하드 1: 체력 x{on.CurrentHealthMultiplier}, 피해 x{on.CurrentDamageMultiplier} (따로 적용)");
                    on.TrySelect(4);
                    fails += Check(Mathf.Approximately(on.CurrentHealthMultiplier, 2.2f) && Mathf.Approximately(on.CurrentDamageMultiplier, 2.2f),
                        $"노멀 5번: 체력·피해 모두 x{on.CurrentHealthMultiplier} (따로 정하지 않으면 기존 한 값)");
                }
                finally
                {
                    Object.DestroyImmediate(onObject);
                }

                // ---- 하드 스테이지 값이 설계 문서와 같은지 (시작값, OBT 로그로 보정) ----
                int[] power = { 120, 135, 155, 185, 205 };
                float[] hp = { 1.5f, 1.9f, 2.4f, 3.0f, 4.0f };
                float[] dmg = { 1.6f, 1.9f, 2.2f, 2.4f, 2.8f };
                bool ok = true;
                for (int i = 0; i < 5; i++)
                    ok &= hard[i].IsHard && hard[i].StageNumber == i + 1 && hard[i].RecommendedPower == power[i]
                          && Mathf.Approximately(hard[i].HealthMultiplier, hp[i]) && Mathf.Approximately(hard[i].DamageMultiplier, dmg[i])
                          && hard[i].DisplayName == normal[i].DisplayName;
                fails += Check(ok, "하드 1~5 값이 설계 문서와 일치 (권장 120/135/155/185/205, 체력 x1.5~4.0, 피해 x1.6~2.8, 이름은 노멀과 같음)");
                fails += Check(!normal[0].IsHard && !normal[4].IsHard, "노멀 스테이지는 하드가 아님");

                // ---- 보상: 하드 n의 크레딧은 짝이 되는 노멀 n과 같음, 마일스톤은 코어 대신 만능 조각 ----
                var currencyTable = AssetDatabase.LoadAssetAtPath<CurrencyTable>("Assets/Data/Meta/CurrencyTable.asset");
                bool credits = currencyTable != null;
                int[] expectedCredits = { 300, 450, 600, 750, 900 };
                for (int i = 0; credits && i < 5; i++)
                    credits &= currencyTable.GetClearCredit(i + 1) == expectedCredits[i] && currencyTable.GetClearCredit(6 + i) == expectedCredits[i];
                fails += Check(credits, "클리어 크레딧: 하드 1~5(스테이지 6~10)는 짝이 되는 노멀 1~5와 같음 (300/450/600/750/900)");
                var mdata = new SaveData();
                fails += Check(MatchMilestones.HasMilestones(0) && MatchMilestones.HasMilestones(4) && MatchMilestones.HasMilestones(5) && MatchMilestones.HasMilestones(9)
                               && !MatchMilestones.HasMilestones(10) && !MatchMilestones.HasMilestones(-1),
                    "마일스톤은 노멀 1~5와 하드 1~5(스테이지 6~10)에 있고 그 밖의 번호에는 없음");
                fails += Check(!MatchMilestones.IsHardStage(4) && MatchMilestones.IsHardStage(5) && MatchMilestones.IsHardStage(9) && !MatchMilestones.IsHardStage(10),
                    "하드 스테이지 판정: 6~10번째만 하드");
                // 하드 조각 보상: 3분 / 6분 / 클리어. 합계 30 / 40 / 60 / 80 / 100 = 310
                int[][] expectedShards = { new[] { 5, 10, 15 }, new[] { 10, 10, 20 }, new[] { 15, 15, 30 }, new[] { 20, 20, 40 }, new[] { 25, 25, 50 } };
                bool shardTable = true;
                int shardSum = 0;
                for (int h = 0; h < 5; h++)
                    for (int k = 0; k < MatchMilestones.Count; k++)
                    {
                        shardTable &= MatchMilestones.GetShards(5 + h, k) == expectedShards[h][k] && MatchMilestones.GetCore(5 + h, k) == 0;
                        shardSum += MatchMilestones.GetShards(5 + h, k);
                    }
                fails += Check(shardTable && shardSum == 310 && MatchMilestones.GetTotalShards(5) == 30 && MatchMilestones.GetTotalShards(9) == 100,
                    $"하드 마일스톤 조각 표가 설계와 같음 (합계 {shardSum}, 하드 1 = {MatchMilestones.GetTotalShards(5)}, 하드 5 = {MatchMilestones.GetTotalShards(9)}), 코어 보상은 0");
                fails += Check(MatchMilestones.GetShards(0, 0) == 0 && MatchMilestones.GetCore(4, 2) == 600 && MatchMilestones.GetTotalShards(0) == 0,
                    "노멀 스테이지는 조각 보상이 없고 코어 보상은 예전 그대로");
                fails += Check(MatchMilestones.MarkDone(mdata, 5, 0) && MatchMilestones.IsDone(mdata, 5, 0) && MatchMilestones.MarkDone(mdata, 9, MatchMilestones.ClearIndex),
                    "하드 스테이지의 마일스톤도 달성으로 기록됨");
                fails += Check(!MatchMilestones.MarkDone(mdata, 10, 0) && !MatchMilestones.IsDone(mdata, 10, 0), "범위 밖 스테이지(11번째)는 기록되지 않음");
                fails += Check(MatchMilestones.MarkDone(mdata, 4, 0) && MatchMilestones.IsDone(mdata, 4, 0), "노멀 스테이지 5의 마일스톤은 예전처럼 기록됨");
                fails += Check(MatchMilestones.StageLabel(2) == "스테이지 3" && MatchMilestones.StageLabel(5) == "하드 1" && MatchMilestones.StageLabel(9) == "하드 5",
                    "화면 이름: 노멀은 '스테이지 N', 하드는 '하드 N'");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
            return fails;
        }

        private static int Check(bool ok, string message)
        {
            if (ok) Debug.Log($"[Stage] 통과 — {message}");
            else Debug.LogError($"[Stage] 실패 — {message}");
            return ok ? 0 : 1;
        }
    }
}
