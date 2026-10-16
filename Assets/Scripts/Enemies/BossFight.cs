using System.Collections.Generic;
using UnityEngine;
using SurvivalDrone.Core;
using SurvivalDrone.UI;

namespace SurvivalDrone.Enemies
{
    // 9분(540초)에 시작되는 "보스전" 전체를 진행하는 스크립트.
    //
    // 예전에는 540초에 보스가 나와도 일반 몹이 600초까지 계속 나왔고, 승리 조건도 "남은 몹 전부 처치"라서 마지막이 몹 청소였다.
    // 보스전에서는 그 대신 이렇게 흘러간다:
    //   1) 살아 있는 몹이 모두 폭발하며 사라진다(경험치 구슬은 남는다). 새 몹도 더 나오지 않는다.
    //   2) 그 순간 플레이어 위치를 중심으로 붉은 원(제한 테두리)이 생기고, 플레이어와 보스는 원 밖으로 못 나간다.
    //   3) 원 안쪽에 보스가 나타난다.
    //   4) 보스를 쓰러뜨리면 승리한다.
    //
    // 이 스크립트는 씬에 미리 놓아 두지 않고, EnemySpawner가 보스전을 시작할 때 코드로 만든다(씬 파일을 건드리지 않으려는 것).
    // 그래서 아래 값들은 인스펙터가 아니라 코드의 기본값이 쓰인다. 값을 바꾸려면 이 파일의 숫자를 고치면 된다.
    public class BossFight : MonoBehaviour
    {
        // 지금 보스전이 진행 중인지. 화면 위 타이머를 "보스전"으로 바꾸는 등 다른 스크립트가 확인할 때 쓴다.
        public static bool Active { get; private set; }

        // 진행 중인 보스전(없으면 null).
        public static BossFight Instance { get; private set; }

        // 제한 테두리(원)의 반지름(m). 처음 정한 초안 값이고, 플레이로 보정한다.
        [SerializeField] private float arenaRadius = 22f;

        // 맵(바닥)의 안전 범위. 플레이어가 평소에 갈 수 있는 한계(PlayerController.arenaLimit)와 같은 값이라
        // 원이 이 범위 밖으로 나가지 않게 중심을 보정할 때 쓴다.
        [SerializeField] private float mapLimit = 49f;

        // 보스가 나타나는 자리와 플레이어 사이의 거리(m).
        [SerializeField] private float bossSpawnDistance = 12f;

        // 보스전 전용 체력 배율. 보스 한 마리와 일대일로 싸우므로 체력을 크게 키운다.
        // (원래 보스 체력 500은 혼자 싸우면 10초 안에 끝날 수 있다. 목표는 일반적인 전투력으로 60-90초에 끝나는 것)
        [SerializeField] private float bossHealthMultiplier = 5f;

        // 보스전이 시작될 때 화면에 띄우는 안내 문구.
        [SerializeField] private string entryNotice = "보스 출현!  체력을 모두 깎아 쓰러뜨리세요";

        // 제한 테두리를 그리는 재질의 이름. Assets/Resources 폴더에 있는 에셋이라 씬에 연결하지 않고도 이름으로 불러올 수 있다.
        // (웹 빌드에서 재질이 빠지지 않게 하려고 Resources에 둔다. 에디터 메뉴 SurvivalDrone → Effects → Build Boss Materials로 만든다)
        private const string RingMaterialName = "BossArenaRing";

        // 진행 중인 보스. 쓰러져서 사라지면(null) 승리로 본다.
        public EnemyAI Boss { get; private set; }

        // 보스전이 시작된 판 시각(초). 보스전이 몇 초 걸렸는지 기록할 때 쓴다.
        public float StartedAtMatchTime { get; private set; }

        // 보스전이 시작된 실제 시각(Time.time). 분노 조건("90초 넘게 이어지면") 등에 쓴다.
        public float StartedAtTime { get; private set; }

        private GameObject ringObject;
        private GameObject healthBarObject;
        private bool finished;

        // 새 판을 시작할 때 이전 판의 static 값이 남아 있지 않게 비운다. (에디터에서 도메인 리로드를 끄고 플레이할 때를 위한 안전장치)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Active = false;
            Instance = null;
        }

        // 테스트(CBT) 기록 한 줄 끝에 덧붙일 보스전 요약을 만든다.
        //  - 이번 판에서 보스전이 열리지 않았으면 "보스전=없음"
        //  - 열렸으면 "보스전=진입 보스체력=35% 보스전시간=62.4초" (보스체력 = 판이 끝났을 때 남은 비율, 보스를 쓰러뜨렸으면 0%)
        // 판이 끝나는 순간(결과 화면이 뜰 때) 부르는 것을 전제로 한다. 그때는 BossFight가 아직 살아 있어서 값을 읽을 수 있다.
        public static string BuildLogSummary()
        {
            if (Instance == null) return "보스전=없음";

            float ratio = 0f;
            var boss = Instance.Boss;
            if (boss != null && boss.Health != null && boss.Health.MaxHealth > 0f)
                ratio = Mathf.Clamp01(boss.Health.CurrentHealth / boss.Health.MaxHealth);

            float seconds = Time.time - Instance.StartedAtTime;
            return $"보스전=진입 보스체력={Mathf.CeilToInt(ratio * 100f)}% 보스전시간={seconds:F1}초";
        }

        // 보스전을 시작한다. EnemySpawner가 540초에 부른다.
        //  spawner: 보스를 만들어 줄 스포너, player: 플레이어, 보스 데이터와 프리팹은 스포너가 알고 있다.
        public void Begin(EnemySpawner spawner, Transform player)
        {
            Active = true;
            Instance = this;
            StartedAtMatchTime = GameManager.Instance != null ? GameManager.Instance.ElapsedTime : 0f;
            StartedAtTime = Time.time;

            // 1) 살아 있는 몹을 모두 폭발시켜 없앤다. (구슬은 남는다)
            // 목록을 복사해서 도는 이유: Vanish()가 목록(ActiveEnemies)에서 자기 자신을 지우기 때문에, 원본을 돌면 건너뛰는 몹이 생긴다.
            var snapshot = new List<EnemyAI>(EnemyAI.ActiveEnemies);
            foreach (var enemy in snapshot)
                if (enemy != null) enemy.Vanish();

            // 2) 제한 테두리를 켠다. 플레이어가 맵 가장자리에 있어서 중심이 보정되면 플레이어가 원 밖일 수 있으니 원 안으로 옮긴다.
            BossArena.Begin(player.position, arenaRadius, mapLimit);
            MovePlayerInside(player);
            ringObject = BuildRing(BossArena.Center, BossArena.Radius);

            // 3) 보스를 원 안쪽, 플레이어에서 조금 떨어진 곳에 만든다.
            Vector3 bossPosition = PickBossPosition(player.position);
            Boss = spawner.SpawnFinalBoss(bossPosition, bossHealthMultiplier);

            // 보스에게 공격 패턴(내려찍기·돌진·분노)을 붙인다.
            if (Boss != null) Boss.gameObject.AddComponent<BossBrain>().Init(Boss, player, StartedAtTime);

            // 4) 화면 위쪽에 "BOSS" 체력바를 띄운다. (HUD를 못 찾으면 체력바 없이 보스전만 진행된다)
            var bar = BossHealthBar.Create(Boss);
            if (bar != null) healthBarObject = bar.gameObject;

            HUDNotice.Instance?.Show(entryNotice);
            Debug.Log($"[BossFight] 보스전 시작 - 판 시간 {StartedAtMatchTime:F0}초, 원 중심 ({BossArena.Center.x:F1}, {BossArena.Center.z:F1}), 반지름 {arenaRadius}m");
        }

        private void Update()
        {
            if (finished || GameManager.Instance == null) return;

            // 판이 이미 끝났으면(플레이어가 죽었거나 승리) 더 할 일이 없다.
            if (GameManager.Instance.State != MatchState.Playing)
            {
                Finish();
                return;
            }

            // 보스가 사라졌다 = 쓰러졌다 → 승리.
            if (Boss == null)
            {
                Debug.Log($"[BossFight] 보스 처치 - 보스전 {Time.time - StartedAtTime:F1}초");
                GameManager.Instance.ReportBossDefeated();
                Finish();
            }
        }

        // 보스전을 마무리한다. 제한 테두리를 끈다.
        private void Finish()
        {
            if (finished) return;
            finished = true;
            BossArena.End();
            if (ringObject != null) Destroy(ringObject);
            if (healthBarObject != null) Destroy(healthBarObject);
        }

        private void OnDestroy()
        {
            // 씬이 바뀌거나 오브젝트가 사라질 때도 static 값이 남지 않게 정리한다.
            BossArena.End();
            if (Instance == this)
            {
                Instance = null;
                Active = false;
            }
        }

        // 플레이어를 원 안으로 옮긴다. CharacterController가 켜진 채로 위치만 바꾸면 컨트롤러가 이전 위치로 되돌려 버리는
        // 경우가 있어서, 잠깐 끄고 옮긴 뒤 다시 켠다. (PlayerController.KeepInsideArena와 같은 방식)
        private static void MovePlayerInside(Transform player)
        {
            Vector3 clamped = BossArena.Clamp(player.position, 0.6f);
            if (clamped == player.position) return;

            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.position = clamped;
            if (controller != null) controller.enabled = true;
        }

        // 보스가 나타날 자리를 고른다. 원의 중심 쪽(원 안에서 여유가 있는 쪽)으로 플레이어에서 bossSpawnDistance만큼 떨어진 곳.
        // 플레이어가 중심 근처에 있어서 방향이 모호하면 무작위 방향을 쓴다.
        private Vector3 PickBossPosition(Vector3 playerPosition)
        {
            Vector3 toCenter = BossArena.Center - playerPosition;
            toCenter.y = 0f;
            Vector3 direction;
            if (toCenter.magnitude > 1f)
            {
                direction = toCenter.normalized;
            }
            else
            {
                Vector2 random = Random.insideUnitCircle.normalized;
                direction = new Vector3(random.x, 0f, random.y);
            }

            Vector3 position = playerPosition + direction * bossSpawnDistance;
            // 큰 보스가 원 밖으로 삐져나오지 않도록 몸 크기(약 2.5m)만큼 안쪽으로 보정한다.
            return BossArena.Clamp(position, 2.5f);
        }

        // 제한 테두리를 바닥에 그린다. 가는 고리 모양 메쉬를 코드로 만들어 붉은 재질을 입힌다.
        private static GameObject BuildRing(Vector3 center, float radius)
        {
            var material = Resources.Load<Material>(RingMaterialName);
            if (material == null)
            {
                Debug.LogWarning("[BossFight] Resources에 'BossArenaRing' 재질이 없어 제한 테두리를 그리지 않습니다. 메뉴 SurvivalDrone → Effects → Build Boss Materials를 실행하세요. (테두리 기능은 그대로 동작합니다)");
                return null;
            }

            const int segments = 96;       // 원을 몇 조각으로 나눠 그릴지
            const float halfWidth = 0.3f;  // 고리 굵기의 절반(m)
            const float height = 0.07f;    // 바닥 위로 살짝 띄운 높이(바닥과 겹쳐 깜빡이지 않게)

            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = dir * (radius - halfWidth);       // 안쪽 점
                vertices[i * 2 + 1] = dir * (radius + halfWidth);   // 바깥쪽 점

                int next = (i + 1) % segments;
                int a = i * 2, b = i * 2 + 1, c = next * 2, d = next * 2 + 1;
                int t = i * 6;
                triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = c;
                triangles[t + 3] = c; triangles[t + 4] = b; triangles[t + 5] = d;
            }

            var mesh = new Mesh { name = "BossArenaRingMesh", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();

            var ring = new GameObject("BossArenaRing");
            ring.transform.position = new Vector3(center.x, height, center.z);
            ring.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = ring.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return ring;
        }
    }
}
