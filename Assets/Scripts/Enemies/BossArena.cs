using UnityEngine;

namespace SurvivalDrone.Enemies
{
    // 보스전의 "제한 테두리"(동그란 싸움터)가 어디에 있는지 기억해 두는 도우미.
    //
    // 보스전이 시작되면 그 순간 플레이어가 있던 자리를 중심으로 원 하나를 정하고, 플레이어와 보스는 그 원 밖으로 나갈 수 없다.
    // 원을 실제로 화면에 그리는 일은 BossFight가 하고, 이 클래스는 "원의 위치·크기"와 "위치를 원 안으로 되돌리는 계산"만 맡는다.
    //
    // static(어디서든 바로 부르는 방식)인 이유: 플레이어 이동(PlayerController)·적 이동(EnemyAI)·보스 패턴(BossBrain)이
    // 모두 같은 원을 알아야 하는데, 서로를 인스펙터로 연결하지 않고도 값을 주고받게 하려는 것이다.
    public static class BossArena
    {
        // 지금 제한 테두리가 켜져 있는지. 보스전이 아닐 때는 false라서 이 클래스의 모든 계산은 "아무것도 안 함"이 된다.
        public static bool Active { get; private set; }

        // 원의 중심(바닥 위, y는 쓰지 않는다)과 반지름(m).
        public static Vector3 Center { get; private set; }
        public static float Radius { get; private set; }

        // 새 판을 시작할 때(씬이 다시 불릴 때) 이전 판의 값이 남아 있지 않게 비운다.
        // 에디터에서 "도메인 리로드"를 끄고 플레이하면 static 값이 그대로 남기 때문에 필요한 안전장치다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Active = false;
            Center = Vector3.zero;
            Radius = 0f;
        }

        // 제한 테두리를 켠다. 중심은 "플레이어 위치"인데, 원이 맵(±mapLimit) 밖으로 삐져나가지 않도록
        // 중심을 맵 안쪽으로 조금 당겨 놓는다. (플레이어가 맵 가장자리에 있을 때 원이 투명벽을 넘지 않게 하려는 것)
        public static void Begin(Vector3 playerPosition, float radius, float mapLimit)
        {
            float limit = Mathf.Max(0f, mapLimit - radius);
            Center = new Vector3(
                Mathf.Clamp(playerPosition.x, -limit, limit),
                0f,
                Mathf.Clamp(playerPosition.z, -limit, limit));
            Radius = radius;
            Active = true;
        }

        // 제한 테두리를 끈다. (보스전이 끝났을 때)
        public static void End()
        {
            Active = false;
        }

        // 위치가 원 안(여유 margin을 뺀 반지름 이내)에 있는지.
        public static bool Contains(Vector3 position, float margin = 0f)
        {
            if (!Active) return true;
            float dx = position.x - Center.x;
            float dz = position.z - Center.z;
            float limit = Mathf.Max(0f, Radius - margin);
            return dx * dx + dz * dz <= limit * limit;
        }

        // 위치를 원 안으로 되돌린다. 이미 안에 있으면(또는 테두리가 꺼져 있으면) 받은 값 그대로 돌려준다.
        // margin: 몸 반지름만큼의 여유. 몸이 원 밖으로 삐져나가지 않도록 가장자리에서 그만큼 안쪽에서 멈추게 한다.
        // y 값은 건드리지 않는다.
        public static Vector3 Clamp(Vector3 position, float margin = 0f)
        {
            if (!Active) return position;

            float dx = position.x - Center.x;
            float dz = position.z - Center.z;
            float limit = Mathf.Max(0f, Radius - margin);
            float sqrDistance = dx * dx + dz * dz;
            if (sqrDistance <= limit * limit) return position;

            // 원 밖이면 중심에서 그 방향으로 limit만큼 떨어진 원 위의 점으로 옮긴다.
            float distance = Mathf.Sqrt(sqrDistance);
            float scale = limit / distance;
            return new Vector3(Center.x + dx * scale, position.y, Center.z + dz * scale);
        }
    }
}
