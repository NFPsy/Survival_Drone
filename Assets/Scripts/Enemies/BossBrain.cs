using UnityEngine;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;
using SurvivalDrone.UI;

namespace SurvivalDrone.Enemies
{
    // 보스전의 최종 보스에게 붙어서 "공격 패턴"을 정하는 두뇌.
    //
    // 보스는 평소에 EnemyAI가 하던 대로 플레이어를 천천히 쫓아간다. 이 스크립트는 일정 시간마다 그 추격을 멈추고
    // 두 가지 패턴을 번갈아 쓴다. 둘 다 바닥에 붉은 "예고"가 먼저 보이니, 보고 피하면 맞지 않는다.
    //
    //  1) 내려찍기: 보스가 멈추고, 플레이어가 서 있던 자리에 붉은 원이 차오른다. 다 차면 터지고, 원 안에 있으면 피해를 입는다.
    //  2) 돌진   : 보스가 멈추고, 플레이어 쪽으로 붉은 띠가 차오른다. 다 차면 그 방향으로 쭉 돌진한다. 맞으면 피해를 입고,
    //              끝나면 잠깐 숨을 고른다(이때가 공격할 기회).
    //  분노      : 체력이 반 이하로 내려가거나 보스전이 오래(90초) 이어지면 시작된다. 패턴이 더 자주 나오고, 내려찍기가 3연속이 되고,
    //              보스가 더 빠르고 더 아파진다. (계속 도망 다니며 시간만 끌지 못하게 하려는 장치)
    //
    // 숫자(피해량·범위·시간)는 모두 "처음 정한 값"이다. 직접 플레이해 보고 코드 맨 위의 값을 고쳐 가며 맞춘다.
    // 씬에는 미리 붙여 두지 않고 BossFight가 보스를 만든 직후 코드로 붙인다. (그래서 값은 인스펙터가 아니라 이 파일의 기본값이다)
    public class BossBrain : MonoBehaviour
    {
        private enum State { Chase, SlamTelegraph, SlamRecover, DashTelegraph, Dash, DashRecover }

        // ── 공통 ──
        // 패턴이 끝난 뒤 다음 패턴이 나오기까지의 간격(초). 평소 / 분노 때.
        [SerializeField] private float patternCooldown = 3.5f;
        [SerializeField] private float rageCooldown = 2.1f;

        // ── 내려찍기 ──
        // 10/18 난이도 조정: 플레이해 보니 분노 때 3연속(예고 0.9초, 반지름 4.5m)은 기본 이동속도로는 피할 수 없었다.
        // 반응 시간(약 0.4초)을 빼고도 원 밖으로 나갈 수 있게 예고를 늘리고, 범위·연속 횟수·피해를 줄였다.
        [SerializeField] private float slamRadius = 4.0f;              // 터지는 범위(반지름, m) (옛 4.5)
        [SerializeField] private float slamTelegraphSeconds = 1.6f;    // 예고(원이 차오르는) 시간 (옛 1.3)
        [SerializeField] private float rageSlamTelegraphSeconds = 1.3f; // 분노 때 예고 시간 (옛 0.9)
        [SerializeField] private int rageSlamCount = 2;                // 분노 때 연속으로 내려찍는 횟수 (옛 3)
        [SerializeField] private float slamDamage = 16f;               // 기본 피해(스테이지 피해 배율이 곱해진다) (옛 20)
        [SerializeField] private float slamRecoverSeconds = 0.5f;      // 터진 뒤 멈춰 있는 시간

        // ── 돌진 ──
        [SerializeField] private float dashLength = 18f;               // 돌진 거리(m)
        [SerializeField] private float dashWidth = 2.4f;               // 예고 띠 폭(m)
        [SerializeField] private float dashTelegraphSeconds = 1.0f;    // 예고 시간
        [SerializeField] private float dashSpeed = 16f;                // 돌진 속도(m/s)
        [SerializeField] private float dashMaxSeconds = 1.1f;          // 돌진이 이어지는 최대 시간
        [SerializeField] private float dashDamage = 25f;               // 기본 피해(맞으면 한 번만)
        [SerializeField] private float dashRecoverSeconds = 0.8f;      // 돌진이 끝난 뒤 숨 고르는 시간
        [SerializeField] private float dashMinLength = 3f;             // 이보다 짧게밖에 못 달리면(벽·건물 앞) 돌진 대신 내려찍기를 쓴다

        // ── 분노 ──
        [SerializeField] private float rageHealthRatio = 0.5f;         // 체력이 이 비율 이하면 분노
        [SerializeField] private float rageAfterSeconds = 90f;         // 보스전이 이 시간 넘게 이어지면 분노
        [SerializeField] private float rageMoveSpeedMultiplier = 1.3f; // 분노 때 이동속도 배율
        [SerializeField] private float rageDamageMultiplier = 1.2f;    // 분노 때 피해 배율(패턴·접촉)

        private EnemyAI boss;
        private Transform player;
        private Health playerHealth;
        private float fightStartTime;

        private State state = State.Chase;
        private float stateTimer;
        private float cooldown;
        private bool rage;
        private bool nextIsSlam = true;   // 패턴은 내려찍기와 돌진을 번갈아 쓴다(같은 패턴이 연속 두 번 나오지 않게)

        // 내려찍기 진행 상태
        private BossTelegraph slamTelegraph;
        private Vector3 slamCenter;
        private int slamIndex;
        private int slamTotal;
        private float slamDuration;

        // 돌진 진행 상태
        private BossTelegraph dashTelegraph;
        private Vector3 dashDirection;
        private float dashPathLength;     // 이번 돌진에서 실제로 달릴 수 있는 거리(벽·건물 앞에서 줄어듦)
        private float dashWalked;
        private float dashTimer;
        private bool dashHit;

        // 보스전이 시작되면 BossFight가 보스를 만든 직후 불러서 연결한다.
        public void Init(EnemyAI bossAi, Transform playerTransform, float startTime)
        {
            boss = bossAi;
            player = playerTransform;
            playerHealth = playerTransform != null ? playerTransform.GetComponent<Health>() : null;
            fightStartTime = startTime;
            cooldown = patternCooldown;   // 등장하자마자 공격하지 않도록 첫 패턴 전에 한 번 쉰다
        }

        private void Update()
        {
            if (boss == null || player == null) return;
            if (playerHealth != null && playerHealth.IsDead) return;

            CheckRage();

            switch (state)
            {
                case State.Chase: UpdateChase(); break;
                case State.SlamTelegraph: UpdateSlamTelegraph(); break;
                case State.SlamRecover: UpdateRecover(slamRecoverSeconds); break;
                case State.DashTelegraph: UpdateDashTelegraph(); break;
                case State.Dash: UpdateDash(); break;
                case State.DashRecover: UpdateRecover(dashRecoverSeconds); break;
            }
        }

        private void OnDestroy()
        {
            // 보스가 쓰러지는 순간 화면에 남은 예고 표시를 모두 치운다.
            if (slamTelegraph != null) Destroy(slamTelegraph.gameObject);
            if (dashTelegraph != null) Destroy(dashTelegraph.gameObject);
        }

        // ───────────────────────── 분노 ─────────────────────────

        private void CheckRage()
        {
            if (rage) return;

            float ratio = boss.Health != null && boss.Health.MaxHealth > 0f ? boss.Health.CurrentHealth / boss.Health.MaxHealth : 1f;
            bool lowHealth = ratio <= rageHealthRatio;
            bool tooLong = Time.time - fightStartTime >= rageAfterSeconds;
            if (!lowHealth && !tooLong) return;

            rage = true;
            BossFightLog.RecordRageStart(Time.time - fightStartTime);   // 판 기록: 보스전 몇 초째에 분노했는지
            boss.MoveSpeedMultiplier = rageMoveSpeedMultiplier;
            // 돌진 중에는 접촉 피해가 꺼져 있다(돌진 피해가 따로 들어가서). 그때는 돌진이 끝날 때 분노 값으로 켠다.
            if (state != State.Dash) boss.ContactDamageScale = rageDamageMultiplier;
            HUDNotice.Instance?.Show("보스가 분노했다!", 2f);
            Debug.Log($"[BossBrain] 분노 시작 - {(lowHealth ? "체력 절반 이하" : "보스전 장기화")}");
        }

        // ───────────────────────── 추격 → 패턴 고르기 ─────────────────────────

        private void UpdateChase()
        {
            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;

            // 내려찍기와 돌진을 번갈아 쓴다. 돌진을 쓸 차례인데 달릴 공간이 거의 없으면(벽·건물 앞) 이번엔 내려찍기를 쓴다.
            if (nextIsSlam)
            {
                StartSlam();
                nextIsSlam = false;
            }
            else if (TryStartDash())
            {
                nextIsSlam = true;
            }
            else
            {
                // 돌진할 공간이 없으니 이번엔 내려찍기를 쓰고, 다음 차례에 다시 돌진을 시도한다.
                StartSlam();
            }
        }

        // 패턴을 끝내고 다시 추격으로 돌아간다.
        private void EndPattern()
        {
            state = State.Chase;
            boss.MovementLocked = false;
            cooldown = rage ? rageCooldown : patternCooldown;
        }

        // 숨 고르는 시간이 지나면 패턴을 끝낸다.
        private void UpdateRecover(float seconds)
        {
            stateTimer += Time.deltaTime;
            if (stateTimer >= seconds) EndPattern();
        }

        // ───────────────────────── 내려찍기 ─────────────────────────

        private void StartSlam()
        {
            boss.MovementLocked = true;
            slamIndex = 0;
            slamTotal = rage ? Mathf.Max(1, rageSlamCount) : 1;
            slamDuration = rage ? rageSlamTelegraphSeconds : slamTelegraphSeconds;
            BeginSlamTelegraph();
        }

        // 지금 플레이어가 서 있는 자리에 예고 원을 만든다. (이 자리가 고정되므로, 플레이어는 예고가 차오르는 동안 벗어나면 된다)
        private void BeginSlamTelegraph()
        {
            slamCenter = Flat(player.position);
            slamTelegraph = BossTelegraph.Circle(slamCenter, slamRadius);
            slamTelegraph.SetProgress(0f);
            state = State.SlamTelegraph;
            stateTimer = 0f;
        }

        private void UpdateSlamTelegraph()
        {
            stateTimer += Time.deltaTime;
            FaceToward(slamCenter);
            if (slamTelegraph != null) slamTelegraph.SetProgress(stateTimer / slamDuration);
            if (stateTimer < slamDuration) return;

            // 터진다: 원 안에 있으면 피해. (판 기록에 "몇 번 터졌고 몇 번 맞았는지"를 남긴다)
            bool slamHitPlayer = FlatDistance(player.position, slamCenter) <= slamRadius;
            BossFightLog.RecordSlam(rage, slamHitPlayer);
            if (slamHitPlayer)
                DealDamage(slamDamage, "보스내려찍기");

            // 번쩍인 뒤 잠깐 있다 사라진다.
            if (slamTelegraph != null)
            {
                slamTelegraph.Flash();
                Destroy(slamTelegraph.gameObject, 0.25f);
                slamTelegraph = null;
            }

            slamIndex++;
            if (slamIndex < slamTotal)
            {
                BeginSlamTelegraph();   // 분노 때는 이어서 다음 자리에 또 내려찍는다
            }
            else
            {
                state = State.SlamRecover;
                stateTimer = 0f;
            }
        }

        // ───────────────────────── 돌진 ─────────────────────────

        // 돌진을 시작한다. 달릴 공간이 너무 짧으면 시작하지 않고 false를 돌려준다.
        private bool TryStartDash()
        {
            Vector3 origin = Flat(boss.transform.position);
            Vector3 toPlayer = Flat(player.position) - origin;
            if (toPlayer.sqrMagnitude < 0.0001f) return false;

            Vector3 direction = toPlayer.normalized;
            float length = ComputeDashLength(origin, direction);
            if (length < dashMinLength) return false;

            boss.MovementLocked = true;
            dashDirection = direction;
            dashPathLength = length;
            dashWalked = 0f;
            dashTimer = 0f;
            dashHit = false;
            dashTelegraph = BossTelegraph.Line(origin, direction, length, dashWidth);
            dashTelegraph.SetProgress(0f);
            state = State.DashTelegraph;
            stateTimer = 0f;
            return true;
        }

        // 보스가 실제로 달릴 수 있는 거리를 잰다. 0.5m씩 앞으로 가 보면서 제한 테두리 밖이거나 건물에 막히는 지점 직전까지만 센다.
        // (예고 띠를 그 길이까지만 그려서, 띠가 벽 너머까지 이어지는 거짓말을 하지 않게 하려는 것)
        private float ComputeDashLength(Vector3 origin, Vector3 direction)
        {
            float length = 0f;
            var obstacles = EnemyObstacle.All;
            for (float distance = 0.5f; distance <= dashLength; distance += 0.5f)
            {
                Vector3 point = origin + direction * distance;
                if (!BossArena.Contains(point, boss.BodyRadius)) break;

                bool blocked = false;
                for (int i = 0; i < obstacles.Count; i++)
                {
                    Vector3 probe = point;
                    if (obstacles[i].PushOut(ref probe, boss.BodyRadius)) { blocked = true; break; }
                }
                if (blocked) break;

                length = distance;
            }
            return length;
        }

        private void UpdateDashTelegraph()
        {
            stateTimer += Time.deltaTime;
            FaceDirection(dashDirection);
            if (dashTelegraph != null) dashTelegraph.SetProgress(stateTimer / dashTelegraphSeconds);
            if (stateTimer < dashTelegraphSeconds) return;

            // 돌진 시작. 돌진 피해가 따로 들어가므로 몸이 닿았을 때의 접촉 피해는 이 동안 끈다.
            state = State.Dash;
            boss.ContactDamageScale = 0f;
            if (dashTelegraph != null) dashTelegraph.Flash();
        }

        private void UpdateDash()
        {
            dashTimer += Time.deltaTime;

            float step = dashSpeed * Time.deltaTime;
            Vector3 position = boss.transform.position;
            Vector3 newPosition = position + dashDirection * step;
            bool stopped = false;

            // 건물에 막히면 그 자리에서 멈춘다.
            var obstacles = EnemyObstacle.All;
            for (int i = 0; i < obstacles.Count; i++)
            {
                if (obstacles[i].PushOut(ref newPosition, boss.BodyRadius)) stopped = true;
            }

            // 제한 테두리 밖으로 나가려 하면 가장자리에서 멈춘다.
            Vector3 clamped = BossArena.Clamp(newPosition, boss.BodyRadius);
            if (clamped != newPosition) stopped = true;

            boss.transform.position = clamped;
            dashWalked += step;

            // 돌진이 몸에 닿으면 피해(한 번만).
            if (!dashHit && FlatDistance(player.position, boss.transform.position) <= boss.BodyRadius + 0.5f + 0.4f)
            {
                dashHit = true;
                DealDamage(dashDamage, "보스돌진");
            }

            if (stopped || dashWalked >= dashPathLength || dashTimer >= dashMaxSeconds) EndDash();
        }

        private void EndDash()
        {
            BossFightLog.RecordDash(rage, dashHit);   // 판 기록: 돌진을 한 번 썼고 맞았는지
            if (dashTelegraph != null)
            {
                Destroy(dashTelegraph.gameObject);
                dashTelegraph = null;
            }
            boss.ContactDamageScale = rage ? rageDamageMultiplier : 1f;
            state = State.DashRecover;
            stateTimer = 0f;
        }

        // ───────────────────────── 도우미 ─────────────────────────

        // 패턴 피해를 플레이어에게 준다. 일반 몹의 접촉 피해와 같은 규칙: 스테이지 피해 배율을 곱하고, 분노 때는 더 곱한다.
        // 테스트(CBT) 기록에 "보스내려찍기"·"보스돌진"으로 남기며, 반드시 TakeDamage 앞에서 기록한다(마지막 일격이 빠지지 않게).
        private void DealDamage(float baseDamage, string label)
        {
            if (playerHealth == null || playerHealth.IsDead) return;

            float stageScale = StageProgress.Instance != null ? StageProgress.Instance.CurrentDamageMultiplier : 1f;
            float damage = baseDamage * stageScale * (rage ? rageDamageMultiplier : 1f);
            DamageSourceLog.Record(label, damage * playerHealth.DamageTakenMultiplier);
            playerHealth.TakeDamage(damage);
        }

        private static Vector3 Flat(Vector3 position)
        {
            return new Vector3(position.x, 0f, position.z);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // 보스가 어떤 자리를 향해 몸을 돌린다. (예고 중에 바라보는 방향이 보이게 하려는 것)
        private void FaceToward(Vector3 point)
        {
            Vector3 direction = Flat(point) - Flat(boss.transform.position);
            FaceDirection(direction);
        }

        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            boss.transform.rotation = Quaternion.RotateTowards(boss.transform.rotation, target, 360f * Time.deltaTime);
        }
    }
}
