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

        // 터질 때 재생할 파티클. 인스펙터에서 지정 안 해도 자식에서 자동으로 찾는다.
        [SerializeField] private ParticleSystem explosionEffect;

        // 터질 때 재생할 효과음.
        [SerializeField] private AudioClip explosionSound;

        // 터질 때 바닥에서 피해 범위 끝까지 퍼져 나가는 충격파 링. "어디까지 피해가 닿는지"를 눈으로 보여 준다.
        // 비워두면 링 없이 예전처럼 불꽃만 터진다.
        [SerializeField] private ParticleSystem shockwaveRing;

        // 터지는 순간 드론 위치에서 한 번 크게 번쩍이는 빛. 비워두면 번쩍임이 없다.
        [SerializeField] private ParticleSystem coreFlash;

        private void Awake()
        {
            if (explosionEffect == null) explosionEffect = GetComponentInChildren<ParticleSystem>();
        }

        protected override void Update()
        {
            // 폭발 드론은 궤도를 돌지 않고 플레이어 주변 고정 위치에 떠 있으면 되므로,
            // 부모 클래스(DroneBase)의 기본 "따라다니기" 동작을 그대로 사용한다.
            base.Update();

            // 폭발 쿨타임을 줄여나가다가, 다 되면 한 번 터뜨린다.
            explodeTimer -= Time.deltaTime;
            if (explodeTimer <= 0f)
            {
                // 오버드라이브 중이면 폭발 간격도 배율만큼 짧아진다.
                explodeTimer = baseInterval / OverdriveAttackSpeed;
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

            // 폭발 파티클도 실제 피해 범위(radius)에 맞춰 크기를 조절한 뒤 재생.
            if (explosionEffect != null)
            {
                var shape = explosionEffect.shape;
                shape.radius = radius;
                explosionEffect.Play();
            }
            PlayShockwave(radius);
            SurvivalDrone.Core.AudioManager.Instance?.PlaySfx(explosionSound);
        }

        // 충격파 링과 중심 번쩍임을 재생한다. 두 효과는 "월드 기준"으로 시뮬레이션되는 파티클이라,
        // 재생 직전에 위치를 정해 두면 그 뒤에 드론이 움직여도 터진 자리에 남아 퍼져 나간다.
        private void PlayShockwave(float radius)
        {
            if (shockwaveRing != null)
            {
                // 링은 바닥 바로 위(y=0.1)에 눕혀서 드론 바로 아래부터 퍼지게 한다.
                shockwaveRing.transform.position = new Vector3(transform.position.x, 0.1f, transform.position.z);

                // 링 그림은 한가운데부터 가장자리까지가 한 장이라서, 크기를 "지름"(반지름 x 2)으로 맞추면
                // 다 퍼졌을 때 링의 바깥 가장자리가 실제 피해 범위와 같아진다.
                var ringMain = shockwaveRing.main;
                ringMain.startSize = radius * 2f;
                shockwaveRing.Play();
            }

            if (coreFlash != null)
            {
                coreFlash.transform.position = transform.position;
                var flashMain = coreFlash.main;
                flashMain.startSize = Mathf.Max(1.5f, radius * 0.8f);
                coreFlash.Play();
            }
        }
    }
}
