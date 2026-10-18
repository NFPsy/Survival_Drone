using UnityEngine;
using SurvivalDrone.Enemies;

namespace SurvivalDrone.Drones
{
    // 저격 드론: 사거리 안에서 "가장 가까이 있는" 적 하나를 골라 원거리 공격한다.
    // 기획서 4장 기준 - 강화될수록 좋아지는 것은 "공격력 또는 공격 속도"이므로,
    // 레벨이 오르면 데미지와 공격 속도가 함께 증가하도록 만들었다.
    public class SniperDrone : DroneBase
    {
        // 한 발당 피해량 (레벨에 따라 배율이 곱해짐).
        [SerializeField] private float baseDamage = 15f;

        // 공격과 공격 사이의 기본 간격(초). 레벨이 오르면 이 간격이 짧아진다(더 빨리 쏨).
        [SerializeField] private float baseAttackInterval = 2f;

        // 이 거리 안에 있는 적만 공격 대상으로 삼는다.
        [SerializeField] private float range = 14f;

        // 발사 순간 잠깐 보여줄 이펙트(총알 궤적 등) 프리팹. 비워둬도 동작에는 문제 없음(연출용).
        [SerializeField] private GameObject tracerPrefab;

        // 발사할 때마다 재생할 효과음.
        [SerializeField] private AudioClip fireSound;

        // 광선이 화면에 남아 있는 시간(초). 너무 짧으면(예전 0.1초) 눈에 거의 안 띄어서 조금 늘렸다.
        [SerializeField] private float tracerDuration = 0.18f;

        // 광선의 굵기 배율. 프리팹의 가로·세로 두께에 곱한다. (1 = 프리팹 그대로)
        [SerializeField] private float tracerThicknessMultiplier = 2.2f;

        // 발사하는 순간 드론 위치(총구)에서 번쩍이는 빛. 비워두면 번쩍임이 없다.
        [SerializeField] private ParticleSystem muzzleFlash;

        // 광선이 맞은 자리에서 번쩍이는 빛. 비워두면 번쩍임이 없다.
        [SerializeField] private ParticleSystem impactFlash;

        // 새 레이저 이펙트의 원본(SniperBeam.prefab): 밝은 빔 + 맞은 자리의 불꽃. 지정하면 위의 옛 막대(tracerPrefab) 대신 이것을 쓴다.
        // 드론이 0.5배로 줄어 있어서, 자식으로 두면 이펙트도 같이 작아진다. 그래서 따로(최상위에) 하나 만들어 두고 쓴다.
        [SerializeField] private SniperBeamEffect beamPrefab;
        private SniperBeamEffect beam;

        // 맞은 적의 "몸 한가운데" 높이: 적 크기(세로 배율)의 0.7배. 발이 아니라 가슴께로 쏜다.
        private const float ChestHeightPerScale = 0.7f;

        // 다음 공격까지 남은 시간.
        private float attackTimer;

        private void Awake()
        {
            if (beamPrefab != null) beam = Instantiate(beamPrefab);
        }

        private void OnDestroy()
        {
            // 드론이 사라지면(씬을 나가면) 따로 만든 이펙트도 같이 지운다.
            if (beam != null) Destroy(beam.gameObject);
        }

        protected override void Update()
        {
            // 저격 드론은 궤도를 돌지 않고 플레이어 주변 고정 위치에 떠 있으므로,
            // 부모 클래스(DroneBase)의 기본 "따라다니기" 동작을 그대로 사용한다.
            base.Update();

            float scale = GetScale();
            attackTimer -= Time.deltaTime;

            if (attackTimer <= 0f)
            {
                // 레벨이 오를수록(scale이 커질수록) 공격 간격이 짧아져서 더 자주 쏘게 된다.
                // 오버드라이브 중이면 거기에 더해 배율만큼 또 짧아진다.
                attackTimer = baseAttackInterval / (scale * OverdriveAttackSpeed);
                TryFire(scale);
            }
        }

        // 실제로 공격을 시도하는 함수.
        private void TryFire(float scale)
        {
            EnemyAI target = FindTarget();
            if (target == null) return; // 사거리 안에 적이 없으면 그냥 넘어간다.

            // 맞는 지점(적의 가슴 높이)을 피해를 주기 "전에" 정해 둔다. 이 공격으로 적이 사라져도 이펙트가 그 자리를 알고 있게 한다.
            Vector3 hitPoint = target.transform.position + Vector3.up * (Mathf.Max(Mathf.Abs(target.transform.lossyScale.y), 0.7f) * ChestHeightPerScale);

            // 레벨이 오를수록(scale이 커질수록) 데미지도 함께 증가.
            DroneContributionLog.RecordDamage(DroneType.Sniper, target.ApplyDamage(baseDamage * scale));   // 판 기록: 저격 드론이 준 피해
            SurvivalDrone.Core.AudioManager.Instance?.PlaySfx(fireSound, 0.5f);

            // 새 레이저 이펙트가 있으면 그것을 쓴다. (밝은 빔이 쭉 뻗고 맞은 자리에서 불꽃이 튄다)
            if (beam != null) beam.Fire(transform, hitPoint);

            // 새 이펙트가 없을 때만, 예전처럼 드론과 적 사이를 잇는 막대(광선)로 잠깐 보여준 뒤 자동으로 제거.
            if (beam == null && tracerPrefab != null)
            {
                Vector3 start = transform.position;
                Vector3 end = target.transform.position;
                Vector3 midpoint = (start + end) * 0.5f;
                float distance = Vector3.Distance(start, end);

                var tracer = Instantiate(tracerPrefab, midpoint, Quaternion.LookRotation(end - start));
                // 길이(z축)는 드론-적 사이 거리에 맞추고, 가로/세로 두께는 프리팹 값에 굵기 배율을 곱해 더 잘 보이게 한다.
                Vector3 tracerScale = tracer.transform.localScale;
                tracer.transform.localScale = new Vector3(
                    tracerScale.x * tracerThicknessMultiplier,
                    tracerScale.y * tracerThicknessMultiplier,
                    distance);
                Destroy(tracer, tracerDuration);
            }

            PlayFlash(muzzleFlash, transform.position);
            PlayFlash(impactFlash, target.transform.position);
        }

        // 번쩍임 파티클을 지정한 위치에서 재생한다. 월드 기준으로 시뮬레이션되는 파티클이라,
        // 재생 직전에 위치를 정해 두면 드론이 움직이거나 적이 사라져도 그 자리에서 터진다.
        private static void PlayFlash(ParticleSystem flash, Vector3 position)
        {
            if (flash == null) return;

            flash.transform.position = position;
            flash.Play();
        }

        // 사거리 안에 있는 적들 중 "가장 가까이 있는" 적 하나를 찾는 함수.
        private EnemyAI FindTarget()
        {
            EnemyAI best = null;
            float bestDistance = float.MaxValue;

            // EnemyAI.ActiveEnemies는 현재 살아있는 모든 적의 목록 (EnemyAI.cs에서 관리됨).
            foreach (var enemy in EnemyAI.ActiveEnemies)
            {
                if (enemy == null) continue;

                // 맵 밖의 적은 피해를 받지 않으므로 겨냥하지 않는다. (헛발 방지)
                if (!enemy.IsInsideArena) continue;

                float distance = Vector3.Distance(transform.position, enemy.transform.position);

                // 사거리 밖이면 후보에서 제외.
                if (distance > range) continue;

                // 지금까지 찾은 것보다 더 가까우면 새로운 후보로 교체.
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemy;
                }
            }

            return best;
        }
    }
}
