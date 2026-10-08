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

            if (fails == 0) Debug.Log($"[Stage] 검증 완료: 모든 항목 통과 ({System.DateTime.Now:HH:mm:ss})");
            else Debug.LogError($"[Stage] 검증 완료: {fails}개 항목 실패 ({System.DateTime.Now:HH:mm:ss})");
        }

        private static int Check(bool ok, string message)
        {
            if (ok) Debug.Log($"[Stage] 통과 — {message}");
            else Debug.LogError($"[Stage] 실패 — {message}");
            return ok ? 0 : 1;
        }
    }
}
