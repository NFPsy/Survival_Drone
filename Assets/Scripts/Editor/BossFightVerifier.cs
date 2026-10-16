using System.Collections.Generic;
using SurvivalDrone.Enemies;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 보스전의 "계산 부분"이 맞는지 확인하는 에디터 전용 검증 도구.
    // 메뉴 SurvivalDrone → Enemies → Verify BossFight 로 실행하면 결과가 콘솔에 [Boss] 접두사로 찍힌다.
    //
    // 게임을 실행하지 않고 확인하는 것:
    //  1) 보스전 경기장(BossArena): 중심이 맵 밖으로 나가지 않는지, 안/밖 판정과 가두기(Clamp)가 맞는지
    //  2) 보스전 재질 2개가 Resources에 있는지 (없으면 웹 빌드에서 공격 예고가 안 보인다)
    //  3) 공격 예고(BossTelegraph)가 원/띠 모양으로 만들어지고 차오름·번쩍임이 에러 없이 동작하는지
    //  4) 판 기록용 요약 글(BuildLogSummary)이 보스전이 없을 때 "보스전=없음"인지
    // 실제로 보스가 움직이고 공격하는 모습은 플레이 모드에서 직접 확인해야 한다.
    public static class BossFightVerifier
    {
        [MenuItem("SurvivalDrone/Enemies/Verify BossFight")]
        public static void Verify()
        {
            if (!EditorSafety.CanRunEditModeTool("Verify BossFight")) return;

            var objects = new List<GameObject>();
            int fails = 0;
            try
            {
                // ---- 1) 경기장 ----
                BossArena.Begin(new Vector3(0f, 0f, 0f), 22f, 49f);
                fails += Check(BossArena.Active && BossArena.Center == Vector3.zero, "맵 한가운데에서 시작하면 경기장 중심은 그 자리");
                fails += Check(BossArena.Contains(new Vector3(21f, 0f, 0f)) && !BossArena.Contains(new Vector3(23f, 0f, 0f)), "반지름 22: 21칸은 안, 23칸은 밖");
                fails += Check(!BossArena.Contains(new Vector3(21f, 0f, 0f), 2f), "여유(margin) 2를 주면 21칸은 밖으로 침");

                var outsidePoint = new Vector3(40f, 1.5f, 0f);
                var clamped = BossArena.Clamp(outsidePoint, 1f);
                fails += Check(Mathf.Abs(clamped.x - 21f) < 0.01f && Mathf.Approximately(clamped.y, 1.5f), $"밖으로 나간 위치는 몸 반지름 1을 뺀 21칸 지점으로 돌아오고 높이는 그대로 → ({clamped.x:F2}, {clamped.y:F2})");
                var insidePoint = new Vector3(5f, 0f, -3f);
                fails += Check(BossArena.Clamp(insidePoint, 1f) == insidePoint, "안에 있는 위치는 그대로");
                BossArena.End();
                fails += Check(!BossArena.Active && BossArena.Clamp(outsidePoint, 1f) == outsidePoint, "경기장을 끝내면 가두기가 꺼진다");

                BossArena.Begin(new Vector3(48f, 0f, -48f), 22f, 49f);
                fails += Check(Mathf.Abs(BossArena.Center.x) <= 27.01f && Mathf.Abs(BossArena.Center.z) <= 27.01f, $"맵 구석에서 시작해도 경기장이 맵 안에 들어가게 중심을 당김 → ({BossArena.Center.x:F1}, {BossArena.Center.z:F1}) (기대 ±27 이내)");
                BossArena.End();

                // ---- 2) 재질 ----
                fails += Check(Resources.Load<Material>("BossArenaRing") != null, "Resources/BossArenaRing 재질이 있음");
                fails += Check(Resources.Load<Material>("BossTelegraphFill") != null, "Resources/BossTelegraphFill 재질이 있음");

                // ---- 3) 공격 예고 ----
                var circle = BossTelegraph.Circle(new Vector3(3f, 0f, 4f), 4.5f);
                objects.Add(circle.gameObject);
                fails += Check(circle.transform.childCount == 3, $"원형 예고는 바닥·속·테두리 3겹 → {circle.transform.childCount}겹");
                circle.SetProgress(0.5f);
                circle.SetProgress(2f);   // 범위를 넘겨도 에러 없이 1로 처리
                circle.Flash();
                fails += Check(true, "원형 예고: 차오름(0.5, 범위 초과)과 번쩍임이 에러 없이 동작");

                var line = BossTelegraph.Line(Vector3.zero, new Vector3(1f, 0f, 1f), 18f, 2.4f);
                objects.Add(line.gameObject);
                fails += Check(line.transform.childCount == 3, $"띠 예고도 3겹 → {line.transform.childCount}겹");
                var forward = line.transform.forward;
                fails += Check(Vector3.Dot(forward, new Vector3(1f, 0f, 1f).normalized) > 0.999f, "띠 예고는 준 방향을 향함");
                line.SetProgress(0.3f);
                line.Flash();

                // ---- 4) 판 기록 요약 ----
                fails += Check(BossFight.BuildLogSummary() == "보스전=없음", $"보스전에 들어간 적이 없으면 기록은 '보스전=없음' → '{BossFight.BuildLogSummary()}'");
            }
            finally
            {
                if (BossArena.Active) BossArena.End();
                foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
            }

            if (fails == 0) Debug.Log($"[Boss] 보스전 검증 완료: 모든 항목 통과 ({System.DateTime.Now:HH:mm:ss})");
            else Debug.LogError($"[Boss] 보스전 검증 완료: {fails}개 항목 실패 ({System.DateTime.Now:HH:mm:ss})");
        }

        private static int Check(bool ok, string message)
        {
            if (ok) Debug.Log($"[Boss] 통과 — {message}");
            else Debug.LogError($"[Boss] 실패 — {message}");
            return ok ? 0 : 1;
        }
    }
}
