using UnityEngine;
using SurvivalDrone.Enemies;

namespace SurvivalDrone.Drones
{
    // 폭발 드론: 평소엔 가만히 떠 있다가, 일정 시간마다 한 번씩 자기 주변 전체(범위 공격)에 피해를 준다.
    // 근접 드론과 달리 특정 적 하나를 노리는 게 아니라 "범위 안에 있는 모든 적"을 한꺼번에 때린다.
    //
    // 기획서 4장 기준 - 강화될수록 좋아지는 것은 "폭발 범위 또는 피해량"이므로,
    // 레벨이 오르면 범위와 피해량이 함께 커지도록 만들었다.
    public class ExplosionDrone : DroneBase
    {
        // 한 번 폭발할 때 주는 피해량 (레벨에 따라 배율이 곱해짐).
        [SerializeField] private float baseDamage = 10f;

        // 몇 초마다 한 번씩 폭발하는지. 기획서 기준 3초에 1번.
        [SerializeField] private float baseInterval = 3f;

        // 폭발이 닿는 범위(반지름). 레벨이 오르면 이 값이 커진다.
        [SerializeField] private float baseRadius = 3f;

        // 다음 폭발까지 남은 시간.
        private float explodeTimer;

        protected override void Update()
        {
            // 폭발 드론은 궤도를 돌지 않고 플레이어 주변 고정 위치에 떠 있으면 되므로,
            // 부모 클래스(DroneBase)의 기본 "따라다니기" 동작을 그대로 사용한다.
            base.Update();

            // 폭발 쿨타임을 줄여나가다가, 다 되면 한 번 터뜨린다.
            explodeTimer -= Time.deltaTime;
            if (explodeTimer <= 0f)
            {
                explodeTimer = baseInterval;
                Explode();
            }
        }

        // 실제로 주변에 피해를 주는 함수.
        private void Explode()
        {
            // 현재 레벨 기준 배율.
            float scale = GetScale();

            // 범위는 제곱근을 써서 완만하게, 피해량은 배율 그대로 커지게 한다.
            float radius = baseRadius * Mathf.Sqrt(scale);
            float damage = baseDamage * scale;

            // Physics.OverlapSphere로 이 범위 안에 있는 모든 콜라이더를 찾아서 적이면 피해를 준다.
            var colliders = Physics.OverlapSphere(transform.position, radius);
            foreach (var col in colliders)
            {
                var enemy = col.GetComponent<EnemyAI>();
                if (enemy != null) enemy.ApplyDamage(damage);
            }
        }
    }
}
