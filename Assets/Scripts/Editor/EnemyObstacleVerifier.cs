using System.Collections.Generic;
using SurvivalDrone.Enemies;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 적이 건물(EnemyObstacle)을 통과하지 못하고, 건물 뒤에서 멈춰 갇히지도 않는지 확인하는 에디터 전용 검증 도구.
    // 메뉴 SurvivalDrone → Enemies → Verify EnemyObstacle 로 실행하면 결과가 콘솔에 [Enemy] 접두사로 찍힌다.
    //
    // 게임을 실행하지 않고 "계산만" 한다: 임시 건물을 만들어 두고, 적이 플레이어를 향해 걷는 모습을 프레임마다 흉내 낸다.
    // (EnemyAI의 이동 코드와 같은 순서: 건물이 막으면 모서리를 목표로 → 한 걸음 걷기 → 건물 안이면 밀어내기)
    // 건물 배치는 InGame 씬의 Building_01~05와 같은 값이다.
    public static class EnemyObstacleVerifier
    {
        // InGame 씬의 건물: (가운데 x, z, 가로 x, 가로 z). 높이는 계산에 쓰지 않는다.
        private static readonly float[][] SceneBuildings =
        {
            new[] { 20f, 15f, 4f, 4f },    // Building_01
            new[] { -25f, 10f, 5f, 5f },   // Building_02
            new[] { 15f, -22f, 3f, 3f },   // Building_03
            new[] { -18f, -18f, 4f, 4f },  // Building_04
            new[] { 30f, -5f, 4f, 4f },    // Building_05
        };

        [MenuItem("SurvivalDrone/Enemies/Verify EnemyObstacle")]
        public static void Verify()
        {
            if (!EditorSafety.CanRunEditModeTool("Verify EnemyObstacle")) return;

            var objects = new List<GameObject>();
            int fails = 0;
            try
            {
                // ---- 1) 밀어내기 기본 동작 (건물 하나: 가운데 (0,0), 크기 4x4, 적 반지름 0.5) ----
                var box = MakeBuilding(objects, 0f, 0f, 4f, 4f, 0f);

                var inside = new Vector3(1.6f, 0.7f, 0.2f);   // 오른쪽 면 근처(경계는 x=2.5)
                bool moved = box.PushOut(ref inside, 0.5f);
                fails += Check(moved && Mathf.Approximately(inside.x, 2.5f) && Mathf.Approximately(inside.z, 0.2f) && Mathf.Approximately(inside.y, 0.7f),
                    $"건물 안(오른쪽 면 근처)은 가까운 오른쪽 면 바깥으로 밀려남, 높이는 그대로 → ({inside.x:F2}, {inside.y:F2}, {inside.z:F2}) (기대 2.50, 0.70, 0.20)");

                var behind = new Vector3(0.3f, 0f, -2.0f);    // 아래 면 근처(경계는 z=-2.5)
                moved = box.PushOut(ref behind, 0.5f);
                fails += Check(moved && Mathf.Approximately(behind.z, -2.5f), $"건물 안(아래 면 근처)은 아래 면 바깥으로 밀려남 → z={behind.z:F2} (기대 -2.50)");

                var outside = new Vector3(5f, 0f, 5f);
                moved = box.PushOut(ref outside, 0.5f);
                fails += Check(!moved && outside == new Vector3(5f, 0f, 5f), "건물 밖에 있는 적은 그대로 (밀지 않음)");

                var onEdge = new Vector3(2.5f, 0f, 0f);       // 정확히 경계 위 = 안이 아님
                fails += Check(!box.PushOut(ref onEdge, 0.5f), "정확히 경계 위에 있는 적은 안으로 치지 않음");

                // 몸이 큰 적(반지름 2)은 더 멀리 밀려남
                var big = new Vector3(2.5f, 0f, 0f);
                box.PushOut(ref big, 2f);
                fails += Check(Mathf.Approximately(big.x, 4f), $"반지름이 큰 적은 건물에서 더 멀리 밀려남 → x={big.x:F2} (기대 4.00)");

                // 회전한 건물: 45도로 돌린 건물에서도 건물 기준으로 밀어냄
                var rotated = MakeBuilding(objects, 10f, 10f, 4f, 4f, 45f);
                var nearRotated = new Vector3(10f, 0f, 10f);  // 건물 한가운데 = 안
                fails += Check(rotated.PushOut(ref nearRotated, 0.5f), "회전한 건물의 한가운데에 있는 적도 밀려남");
                float distFromCenter = Vector2.Distance(new Vector2(nearRotated.x, nearRotated.z), new Vector2(10f, 10f));
                fails += Check(Mathf.Approximately(distFromCenter, 2.5f), $"회전한 건물도 건물 기준 가까운 면(가운데에서 2.5)으로 나옴 → {distFromCenter:F2}");

                // ---- 2) 실제 씬 배치로 모의실험: 적이 건물 뒤의 플레이어를 향해 걷기 ----
                var sceneBoxes = new List<EnemyObstacle>();
                foreach (var b in SceneBuildings) sceneBoxes.Add(MakeBuilding(objects, b[0], b[1], b[2], b[3], 0f));

                // 2-1) 플레이어가 건물 "바로 뒤" 한가운데에 서 있는 가장 어려운 경우 (정면에서 막혀 멈추기 쉬움)
                int stuck = 0, total = 0, enteredBuilding = 0;
                float[] speeds = { 1.8f, 3.5f, 7.2f };      // 가장 느린 적 ~ 가장 빠른 적
                float[] frameTimes = { 1f / 60f, 1f / 30f };  // 웹에서 60프레임 / 30프레임일 때
                for (int bi = 0; bi < sceneBoxes.Count; bi++)
                {
                    Vector3 c = new Vector3(SceneBuildings[bi][0], 0f, SceneBuildings[bi][1]);
                    float half = SceneBuildings[bi][2] * 0.5f;
                    foreach (float speed in speeds)
                    foreach (float dt in frameTimes)
                    foreach (Vector3 approach in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                    {
                        // 적은 건물 한쪽 10칸 밖, 플레이어는 정확히 건물 반대쪽 6칸 밖(건물을 사이에 두고 일직선)
                        Vector3 enemy = c + approach * (half + 10f);
                        Vector3 player = c - approach * (half + 6f);
                        total++;
                        var result = Simulate(sceneBoxes, enemy, player, speed, dt, 0.5f, 3000);
                        if (!result.reached) stuck++;
                        if (result.enteredBuilding) enteredBuilding++;
                    }
                }
                fails += Check(stuck == 0, $"건물을 사이에 두고 일직선인 플레이어에게 적이 끝내 도착함 (갇힘 {stuck} / {total}번)");
                fails += Check(enteredBuilding == 0, $"그동안 적이 건물 안으로 들어간 프레임이 없음 (들어감 {enteredBuilding} / {total}번)");

                // 2-2) 무작위 출발·무작위 플레이어 위치·무작위 적 크기 (건물 5개가 모두 있는 씬에서)
                //  적 크기(몸 반지름)는 실제 적들의 값이다: 빠른 0.35 / 약한 0.4 / 튼튼한 0.55 / 강한 0.7 / 엘리트 최대 0.95 / 보스 1.25
                //  세 번에 한 번은 플레이어를 건물 벽에 바짝(0.55~0.9칸) 붙여 둔다. (벽에 붙어 있는 플레이어에게도 적이 다가와야 한다)
                float[] radii = { 0.35f, 0.4f, 0.55f, 0.7f, 0.95f, 1.25f };
                var random = new System.Random(7);
                int randStuck = 0, randEntered = 0, randTotal = 600;
                for (int i = 0; i < randTotal; i++)
                {
                    float radius = radii[random.Next(radii.Length)];
                    Vector3 enemy = RandomPointOutside(random, sceneBoxes, 45f, radius);
                    Vector3 player = (i % 3 == 0)
                        ? PointNearWall(random, SceneBuildings)
                        : RandomPointOutside(random, sceneBoxes, 45f, 1.0f);
                    float speed = speeds[random.Next(speeds.Length)];
                    float dt = frameTimes[random.Next(frameTimes.Length)];
                    var result = Simulate(sceneBoxes, enemy, player, speed, dt, radius, 30000);
                    if (!result.reached) randStuck++;
                    if (result.enteredBuilding) randEntered++;
                }
                fails += Check(randStuck == 0 && randEntered == 0, $"무작위 {randTotal}번(출발·플레이어 위치·속도·프레임·적 크기 무작위, 3번에 1번은 벽에 붙은 플레이어): 갇힘 {randStuck}, 건물 안 진입 {randEntered}");

                // 2-3) 스폰 위치가 건물 안인 경우: 첫 걸음에 바깥으로 나오고, 그 뒤 플레이어에게 도착
                int spawnInsideFails = 0;
                for (int bi = 0; bi < sceneBoxes.Count; bi++)
                {
                    Vector3 c = new Vector3(SceneBuildings[bi][0], 0f, SceneBuildings[bi][1]);
                    var result = Simulate(sceneBoxes, c, c + new Vector3(0f, 0f, 15f), 3.5f, 1f / 60f, 0.5f, 3000);
                    if (!result.reached || result.enteredBuildingAfterFirstFrame) spawnInsideFails++;
                }
                fails += Check(spawnInsideFails == 0, $"건물 한가운데에서 태어난 적도 바깥으로 나와 플레이어에게 도착함 (실패 {spawnInsideFails} / {sceneBoxes.Count}번)");

                // ---- 3) 건물이 없으면 예전과 똑같이 직선으로 걷는다 ----
                var plain = Simulate(new List<EnemyObstacle>(), new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), 4f, 1f / 60f, 0.5f, 600);
                fails += Check(plain.reached && plain.frames >= 270 && plain.frames <= 285, $"건물이 없으면 직선으로 걷는다: 도착 거리 1.5를 뺀 18.5칸을 속도 4로 걷는 데 {plain.frames}프레임 (기대 약 278)");
            }
            finally
            {
                foreach (var go in objects) Object.DestroyImmediate(go);
            }

            if (fails == 0) Debug.Log($"[Enemy] 건물 충돌 검증 완료: 모든 항목 통과 ({System.DateTime.Now:HH:mm:ss})");
            else Debug.LogError($"[Enemy] 건물 충돌 검증 완료: {fails}개 항목 실패 ({System.DateTime.Now:HH:mm:ss})");
        }

        // ---------------- 도우미 ----------------

        private struct SimResult
        {
            public bool reached;
            public bool enteredBuilding;
            public bool enteredBuildingAfterFirstFrame;
            public int frames;
        }

        // 적 한 마리가 플레이어를 향해 걷는 모습을 프레임마다 흉내 낸다. EnemyAI.Update와 같은 순서로 움직인다.
        // 플레이어 근처(1.5칸 + 적의 몸 반지름 안)까지 가면 "도착". maxFrames 안에 못 가면 갇힌 것으로 본다.
        private static SimResult Simulate(List<EnemyObstacle> obstacles, Vector3 enemy, Vector3 player, float speed, float dt, float radius, int maxFrames)
        {
            var result = new SimResult();
            for (int frame = 1; frame <= maxFrames; frame++)
            {
                Vector3 toTarget = player - enemy;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                if (distance <= 1.5f + radius)
                {
                    result.reached = true;
                    result.frames = frame;
                    return result;
                }

                // EnemyAI.Update와 같은 순서: 목표 정하기(건물이 막으면 모서리) → 걷기 → 건물 안이면 밀어내기
                Vector3 goal = player;
                bool detouring = false;
                for (int i = 0; i < obstacles.Count; i++)
                {
                    Vector3 steered = obstacles[i].SteerAround(enemy, goal, radius);
                    if (steered != goal) { goal = steered; detouring = true; }
                }
                Vector3 toGoal = goal - enemy;
                toGoal.y = 0f;
                float goalDistance = toGoal.magnitude;
                Vector3 dir = goalDistance > 0.0001f ? toGoal / goalDistance : toTarget / distance;
                float step = speed * dt;
                if (detouring) step = Mathf.Min(step, goalDistance);
                Vector3 next = enemy + dir * step;
                for (int i = 0; i < obstacles.Count; i++) obstacles[i].PushOut(ref next, radius);
                enemy = next;

                // 밀어낸 뒤에도 건물 안(반지름은 빼고 건물 몸통 기준)에 있으면 통과한 것이다.
                for (int i = 0; i < obstacles.Count; i++)
                {
                    Vector3 probe = enemy;
                    if (obstacles[i].PushOut(ref probe, 0.01f))
                    {
                        result.enteredBuilding = true;
                        if (frame > 1) result.enteredBuildingAfterFirstFrame = true;
                    }
                }
            }
            result.frames = maxFrames;
            return result;
        }

        // 건물 벽에서 0.55~0.9칸 떨어진 무작위 위치를 고른다. (벽에 바짝 붙은 플레이어를 흉내 낸다)
        private static Vector3 PointNearWall(System.Random random, float[][] buildings)
        {
            var b = buildings[random.Next(buildings.Length)];
            float gapX = b[2] * 0.5f + 0.55f + (float)random.NextDouble() * 0.35f;
            float gapZ = b[3] * 0.5f + 0.55f + (float)random.NextDouble() * 0.35f;
            if (random.Next(2) == 0)
                return new Vector3(b[0] + (random.Next(2) == 0 ? gapX : -gapX), 0f, b[1] + ((float)random.NextDouble() * 2f - 1f) * (b[3] * 0.5f));
            return new Vector3(b[0] + ((float)random.NextDouble() * 2f - 1f) * (b[2] * 0.5f), 0f, b[1] + (random.Next(2) == 0 ? gapZ : -gapZ));
        }

        // 어떤 건물 안(반지름 포함)도 아닌 무작위 위치를 고른다.
        private static Vector3 RandomPointOutside(System.Random random, List<EnemyObstacle> obstacles, float range, float radius)
        {
            for (int tries = 0; tries < 100; tries++)
            {
                var p = new Vector3((float)(random.NextDouble() * 2 - 1) * range, 0f, (float)(random.NextDouble() * 2 - 1) * range);
                bool inside = false;
                for (int i = 0; i < obstacles.Count; i++)
                {
                    Vector3 probe = p;
                    if (obstacles[i].PushOut(ref probe, radius)) { inside = true; break; }
                }
                if (!inside) return p;
            }
            return new Vector3(range, 0f, range);
        }

        // 임시 건물 하나를 만든다. (BoxCollider 크기 1 × 오브젝트 크기 = 실제 건물과 같은 구성)
        private static EnemyObstacle MakeBuilding(List<GameObject> objects, float x, float z, float sizeX, float sizeZ, float yawDegrees)
        {
            var go = new GameObject("EnemyObstacleVerifier_Building") { hideFlags = HideFlags.HideAndDontSave };
            objects.Add(go);
            go.transform.position = new Vector3(x, 3f, z);
            go.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            go.transform.localScale = new Vector3(sizeX, 6f, sizeZ);
            go.AddComponent<BoxCollider>();
            var obstacle = go.AddComponent<EnemyObstacle>();
            obstacle.Refresh();   // 편집 모드에서는 Awake가 자동으로 안 불리므로 직접 불러 준다
            return obstacle;
        }

        private static int Check(bool ok, string message)
        {
            if (ok) Debug.Log($"[Enemy] 통과 — {message}");
            else Debug.LogError($"[Enemy] 실패 — {message}");
            return ok ? 0 : 1;
        }
    }
}
