using UnityEngine;
using SurvivalDrone.Enemies;

namespace SurvivalDrone.Drones
{
    // 근접 드론: 플레이어 주위를 빙글빙글 돌면서 가까이 있는 적을 자동으로 공격한다.
    // 기획서 4장 기준 - 강화될수록 좋아지는 것은 "도는 속도 또는 크기"이므로,
    // 데미지 자체보다는 궤도 속도와 판정 범위가 레벨에 따라 커지도록 만들었다.
    public class MeleeDrone : DroneBase
    {
        // 한 번 맞을 때 주는 피해량.
        [SerializeField] private float damage = 4f;

        // 1초에 몇 번 공격하는지.
        [SerializeField] private float attacksPerSecond = 2f;

        // 공격이 닿는 범위(반지름). 레벨이 오르면 이 값이 커진다.
        [SerializeField] private float baseAttackRadius = 1.8f;

        // 플레이어로부터 얼마나 떨어져서 도는지(궤도 반지름).
        [SerializeField] private float baseOrbitRadius = 2f;

        // 얼마나 빠르게 도는지(초당 회전 각도). 레벨이 오르면 더 빨리 돈다.
        [SerializeField] private float baseOrbitSpeed = 180f;

        // 현재까지 돈 각도(0~360도가 계속 누적됨).
        private float orbitAngle;

        // 다음 공격까지 남은 시간.
        private float attackTimer;

        // DroneBase의 기본 "따라다니기" 동작 대신, 이 드론만의 "궤도 돌기" 동작으로 완전히 교체한다.
        protected override void Update()
        {
            if (owner == null) return;

            // 현재 레벨 기준 배율(1배, 1.28배, 1.56배... 형태로 커짐).
            float scale = GetScale();

            // 매 프레임 각도를 조금씩 증가시켜서 회전시킨다. 레벨이 높을수록 더 빨리 돈다.
            orbitAngle += baseOrbitSpeed * scale * Time.deltaTime;

            // 각도(orbitAngle)를 이용해 플레이어를 중심으로 한 원 위의 위치를 계산.
            Vector3 offset = Quaternion.Euler(0f, orbitAngle, 0f) * (Vector3.forward * baseOrbitRadius);
            transform.position = owner.position + offset + Vector3.up * 1f;

            // 레벨이 오르면 드론 자체의 크기도 조금씩 커지도록(제곱근을 써서 너무 급격히 커지지 않게 조절).
            transform.localScale = Vector3.one * Mathf.Sqrt(scale);

            // 공격 쿨타임을 줄이고, 다 되면 주변 적들에게 피해를 준다.
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = 1f / attacksPerSecond;

                // 공격 판정 범위도 레벨에 따라 커진다.
                DealDamageToNearby(baseAttackRadius * Mathf.Sqrt(scale));
            }
        }

        // 지정한 반경(radius) 안에 있는 모든 적에게 피해를 주는 함수.
        private void DealDamageToNearby(float radius)
        {
            // Physics.OverlapSphere: 이 위치를 중심으로 한 구(sphere) 안에 있는 모든 콜라이더를 찾아준다.
            var colliders = Physics.OverlapSphere(transform.position, radius);
            foreach (var col in colliders)
            {
                // 찾은 콜라이더가 적(EnemyAI)이라면 피해를 준다. 적이 아니면(바닥, 건물 등) 무시.
                var enemy = col.GetComponent<EnemyAI>();
                if (enemy != null) enemy.ApplyDamage(damage);
            }
        }
    }
}
