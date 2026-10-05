using System.Collections.Generic;
using UnityEngine;

namespace SurvivalDrone.Enemies
{
    // "적도 통과하지 못하는" 건물·기둥에 붙이는 스크립트.
    //
    // 왜 필요한가:
    //  플레이어는 CharacterController로 움직여서 건물(BoxCollider)에 막히지만,
    //  적(EnemyAI)은 transform.position을 직접 바꿔서 움직이기 때문에 물리 충돌을 전혀 보지 않고 건물을 통과했다.
    //  그래서 건물이 "플레이어만 방해하는" 이상한 상태였다.
    //
    // 어떻게 해결하나 (물리 엔진을 쓰지 않는 가벼운 방식 — 웹(WebGL)에서도 부담이 없다):
    //  1) 돌아가기(SteerAround): 건물이 적과 플레이어 사이를 가로막고 있으면, 플레이어 대신 "건물 모서리"를 향해 걷게 한다.
    //     두 모서리 중 (적→모서리→플레이어) 길이가 더 짧은 쪽을 고르고, 모서리에 닿으면 다시 플레이어를 향한다.
    //     (그냥 면에 부딪혀 미끄러지게만 하면, 플레이어가 건물 바로 뒤에 서 있을 때 적이 면에 붙어 영원히 멈춰 버린다)
    //  2) 밀어내기(PushOut): 그래도 건물 안으로 들어갔다면(예: 건물 안에서 스폰) 가장 가까운 면 바깥으로 밀어낸다. 안전장치다.
    //
    // 건물 크기와 위치는 같은 오브젝트의 BoxCollider에서 읽어오므로, 건물을 옮기거나 크기를 바꿔도 코드를 고칠 필요가 없다.
    // 새 건물을 추가할 때는 BoxCollider가 있는 오브젝트에 이 스크립트만 붙이면 된다.
    //
    // 주의: 시작할 때 한 번만 크기·위치를 읽는다. 게임 도중에 움직이는 장애물에는 쓰지 않는다.
    [RequireComponent(typeof(BoxCollider))]
    public class EnemyObstacle : MonoBehaviour
    {
        // 지금 씬에 있는 모든 장애물 목록. static이라서 적들이 이 목록 하나만 보면 된다.
        public static readonly List<EnemyObstacle> All = new List<EnemyObstacle>();

        // 돌아갈 모서리 지점을 몸통에서 이만큼 더 바깥에 잡는다. (모서리를 딱 붙어서 스치다 다시 끼이는 것을 막는다)
        private const float CornerMargin = 0.15f;

        // 이 거리 안에 와 있는 모서리는 "도착한 것"으로 보고 다음 모서리를 고른다.
        // 적은 모서리까지 남은 거리만큼만 걷기 때문에(EnemyAI) 모서리에 정확히 닿으므로 아주 작은 값이면 충분하다.
        // 너무 크게 잡으면(예: 0.2) 그 거리 경계에서 "도착했다 → 다른 모서리로 출발 → 다시 가까운 모서리가 후보가 됨"을 반복해 왔다 갔다 한다.
        private const float CornerArriveDistance = 0.05f;

        // 돌아갈 모서리 4개를 담는 임시 배열. 매 프레임 새로 만들지 않으려고(메모리 낭비 방지) 한 번만 만들어 모두가 같이 쓴다.
        // (게임은 한 번에 한 적씩 계산하므로 같이 써도 안전하다)
        private static readonly Vector2[] Corners = new Vector2[4];

        // 월드 좌표 기준: 상자의 가운데, 회전, 가로·세로(x, z)의 절반 크기.
        private Vector3 center;
        private Quaternion rotation = Quaternion.identity;
        private Quaternion inverseRotation = Quaternion.identity;
        private float halfX;
        private float halfZ;

        private void Awake()
        {
            Refresh();
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        // BoxCollider와 Transform에서 가운데·회전·크기를 다시 읽는다. (검증 도구도 이 함수를 불러서 쓴다)
        public void Refresh()
        {
            var box = GetComponent<BoxCollider>();
            Vector3 scale = transform.lossyScale;
            center = transform.TransformPoint(box.center);
            rotation = transform.rotation;
            inverseRotation = Quaternion.Inverse(rotation);
            halfX = Mathf.Abs(box.size.x * scale.x) * 0.5f;
            halfZ = Mathf.Abs(box.size.z * scale.z) * 0.5f;
        }

        // ---------------- 1) 돌아가기 ----------------

        // 적이 from에서 to(플레이어)로 걸어가려는데 건물이 가로막고 있으면, 대신 향해야 할 "모서리 지점"을 돌려준다.
        // 가로막지 않으면 to를 그대로 돌려준다. (그러면 호출한 쪽은 평소처럼 플레이어를 향해 걷는다)
        // radius: 적의 몸 반지름. 모서리 지점은 이 반지름만큼 건물에서 떨어진 곳에 잡는다.
        public Vector3 SteerAround(Vector3 from, Vector3 to, float radius)
        {
            // 판단은 모두 "몸 반지름만큼 부풀린 상자" 기준으로 한다. (적의 몸이 건물에 걸리지 않고 지나갈 수 있는지를 봐야 하기 때문)
            // 몸통만 기준으로 하면 몸통은 비껴가도 몸이 건물에 스쳐서 면에 붙어 멈추는 경우를 놓친다.
            float limitX = halfX + radius;
            float limitZ = halfZ + radius;

            // 적이나 플레이어가 부풀린 상자 안쪽(건물에 바짝 붙은 자리)에 있으면, 상자 경계 위 가장 가까운 점으로 옮겨서 계산한다.
            // (플레이어가 건물 벽에 붙어 있어도 적이 "경계까지" 다가가는 것으로 충분하다. 그 안은 PushOut이 막아 준다)
            Vector2 a = ProjectOut(ToLocal(from), limitX, limitZ);
            Vector2 b = ProjectOut(ToLocal(to), limitX, limitZ);

            float edgeX = limitX - 0.02f;   // 테두리를 따라 걷거나 스치는 것은 "막힘"이 아니도록 조금 줄인 크기
            float edgeZ = limitZ - 0.02f;

            // 가로막지 않으면 그냥 가면 된다.
            if (!SegmentHitsRect(a, b, edgeX, edgeZ)) return to;

            // 부풀린 상자의 네 모서리를 후보로 잡는다. (모서리 지점은 거기서 CornerMargin만큼 더 바깥)
            float cornerX = limitX + CornerMargin;
            float cornerZ = limitZ + CornerMargin;

            Vector2[] corners = Corners;
            corners[0] = new Vector2(cornerX, cornerZ);
            corners[1] = new Vector2(cornerX, -cornerZ);
            corners[2] = new Vector2(-cornerX, cornerZ);
            corners[3] = new Vector2(-cornerX, -cornerZ);

            // 모서리 c를 고르는 비용 = (적 → c) + (c에서 플레이어까지 남은 가장 짧은 길).
            // 남은 길: c에서 플레이어가 바로 보이면 직선, 안 보이면 다른 모서리 d 하나를 더 거친다. (상자는 모서리 2개면 항상 돌아갈 수 있다)
            // "직선 거리"만으로 비용을 재면, 건물에 가로막힌 모서리를 실제보다 가깝다고 착각해서 두 모서리 사이를 왔다 갔다 하게 된다.
            bool found = false;
            Vector2 best = default;
            float bestCost = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 c = corners[i];
                if ((c - a).sqrMagnitude < CornerArriveDistance * CornerArriveDistance) continue;   // 이미 도착한 모서리
                if (SegmentHitsRect(a, c, edgeX, edgeZ)) continue;                                  // 그 모서리까지 가는 길이 막힘

                float remaining = float.MaxValue;
                if (!SegmentHitsRect(c, b, edgeX, edgeZ))
                {
                    remaining = (b - c).magnitude;
                }
                else
                {
                    for (int j = 0; j < 4; j++)
                    {
                        if (j == i) continue;
                        Vector2 d = corners[j];
                        if (SegmentHitsRect(c, d, edgeX, edgeZ)) continue;     // c에서 d로 곧장 갈 수 없음
                        if (SegmentHitsRect(d, b, edgeX, edgeZ)) continue;     // d에서도 플레이어가 안 보임
                        float viaD = (d - c).magnitude + (b - d).magnitude;
                        if (viaD < remaining) remaining = viaD;
                    }
                }
                if (remaining == float.MaxValue) continue;

                float cost = (c - a).magnitude + remaining;
                if (cost < bestCost - 0.001f)
                {
                    bestCost = cost;
                    best = c;
                    found = true;
                }
            }
            if (!found) return to;

            Vector3 world = rotation * new Vector3(best.x, 0f, best.y) + center;
            world.y = from.y;
            return world;
        }

        // ---------------- 2) 밀어내기 (안전장치) ----------------

        // position이 건물(+ 적의 몸 반지름 radius만큼 부풀린 상자) 안에 있으면 가장 가까운 면 바깥으로 밀어내고 true를 돌려준다.
        // 높이(y)는 그대로 둔다. 적은 바닥을 걷고 건물은 높아서 위아래는 따질 필요가 없다.
        public bool PushOut(ref Vector3 position, float radius)
        {
            Vector2 local = ToLocal(position);
            float limitX = halfX + radius;
            float limitZ = halfZ + radius;
            float absX = Mathf.Abs(local.x);
            float absZ = Mathf.Abs(local.y);

            // 상자 밖이면 할 일 없음.
            if (absX >= limitX || absZ >= limitZ) return false;

            // 어느 면으로 나가는 게 더 가까운지: 파고든 깊이가 더 얕은 쪽 면으로 민다.
            if (limitX - absX < limitZ - absZ) local.x = (local.x >= 0f ? 1f : -1f) * limitX;
            else local.y = (local.y >= 0f ? 1f : -1f) * limitZ;

            Vector3 world = rotation * new Vector3(local.x, 0f, local.y) + center;
            position.x = world.x;
            position.z = world.z;
            return true;
        }

        // ---------------- 도우미 ----------------

        // 점 p가 (limitX, limitZ) 크기의 상자 안쪽이면 가장 가까운 면 위로 옮긴다. 밖이면 그대로 돌려준다.
        private static Vector2 ProjectOut(Vector2 p, float limitX, float limitZ)
        {
            float absX = Mathf.Abs(p.x);
            float absZ = Mathf.Abs(p.y);
            if (absX >= limitX || absZ >= limitZ) return p;
            if (limitX - absX < limitZ - absZ) p.x = (p.x >= 0f ? 1f : -1f) * limitX;
            else p.y = (p.y >= 0f ? 1f : -1f) * limitZ;
            return p;
        }

        // 월드 위치를 "건물 기준 평면 좌표"로 바꾼다. (x = 건물의 가로 방향, y = 건물의 세로(z) 방향. 건물이 회전해 있어도 항상 축에 맞는 상자로 계산할 수 있다)
        private Vector2 ToLocal(Vector3 world)
        {
            Vector3 local = inverseRotation * (world - center);
            return new Vector2(local.x, local.z);
        }

        // 선분 a→b가 가운데 (0,0), 반 크기 (limitX, limitZ)인 상자의 안쪽을 지나가는지 검사한다. (Liang–Barsky 방식)
        // 상자의 경계에 스치기만 하는 것은 "지나가지 않음"으로 본다.
        private static bool SegmentHitsRect(Vector2 a, Vector2 b, float limitX, float limitZ)
        {
            float tMin = 0f, tMax = 1f;
            Vector2 d = b - a;
            if (!ClipAxis(a.x, d.x, limitX, ref tMin, ref tMax)) return false;
            if (!ClipAxis(a.y, d.y, limitZ, ref tMin, ref tMax)) return false;
            return tMax - tMin > 1e-4f;
        }

        // 한 축(-limit ~ +limit 사이)에 대해 선분이 안쪽에 있는 구간 [tMin, tMax]를 좁힌다. 안쪽 구간이 없으면 false.
        private static bool ClipAxis(float start, float delta, float limit, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(delta) < 1e-6f)
                return start > -limit && start < limit;   // 이 축으로는 안 움직임: 처음부터 안쪽이어야 한다

            float t1 = (-limit - start) / delta;
            float t2 = (limit - start) / delta;
            if (t1 > t2) { float swap = t1; t1 = t2; t2 = swap; }
            if (t1 > tMin) tMin = t1;
            if (t2 < tMax) tMax = t2;
            return tMin < tMax;
        }
    }
}
