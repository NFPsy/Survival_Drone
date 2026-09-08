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

        // 다음 공격까지 남은 시간.
        private float attackTimer;

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
                attackTimer = baseAttackInterval / scale;
                TryFire(scale);
            }
        }

        // 실제로 공격을 시도하는 함수.
        private void TryFire(float scale)
        {
            EnemyAI target = FindTarget();
            if (target == null) return; // 사거리 안에 적이 없으면 그냥 넘어간다.

            // 레벨이 오를수록(scale이 커질수록) 데미지도 함께 증가.
            target.ApplyDamage(baseDamage * scale);
            SurvivalDrone.Core.AudioManager.Instance?.PlaySfx(fireSound, 0.5f);

            // 발사 이펙트가 설정되어 있으면 드론과 적 사이를 잇는 가느다란 빛줄기(광선)로 잠깐 보여준 뒤 0.1초 뒤 자동으로 제거.
            if (tracerPrefab != null)
            {
                Vector3 start = transform.position;
                Vector3 end = target.transform.position;
                Vector3 midpoint = (start + end) * 0.5f;
                float distance = Vector3.Distance(start, end);

                var tracer = Instantiate(tracerPrefab, midpoint, Quaternion.LookRotation(end - start));
                // 프리팹의 가로/세로 두께는 그대로 두고, 길이(z축)만 드론-적 사이 거리에 맞춰 늘린다.
                Vector3 tracerScale = tracer.transform.localScale;
                tracer.transform.localScale = new Vector3(tracerScale.x, tracerScale.y, distance);
                Destroy(tracer, 0.1f);
            }
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
