using UnityEngine;

namespace SurvivalDrone.Core
{
    // 피해를 입었을 때 작은 스파크 파티클을 한 번 터뜨려주는 컴포넌트.
    // DamageFlash(색 번쩍임)와 비슷하게 Health의 OnDamaged 이벤트를 구독해서 동작한다.
    // 두 효과를 같이 쓰면 "맞았다"는 게 훨씬 확실하게 느껴진다.
    //
    // [RequireComponent(typeof(Health))]는 이 스크립트가 붙은 오브젝트에
    // Health(체력) 컴포넌트가 반드시 있어야 한다는 뜻.
    [RequireComponent(typeof(Health))]
    public class HitSparkEffect : MonoBehaviour
    {
        // 터뜨릴 파티클 시스템. 비워두면 자기 자신이나 자식에서 자동으로 찾는다.
        [SerializeField] private ParticleSystem sparkParticles;

        private Health health;

        private void Awake()
        {
            health = GetComponent<Health>();

            // 인스펙터에서 파티클을 지정하지 않았다면 자식에서 자동으로 찾는다.
            if (sparkParticles == null) sparkParticles = GetComponentInChildren<ParticleSystem>();
        }

        private void OnEnable()
        {
            health.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            health.OnDamaged -= HandleDamaged;
        }

        private void HandleDamaged(float amount)
        {
            if (sparkParticles == null) return;

            // Play()는 이미 재생 중이어도 처음부터 다시 재생해준다 (연속으로 맞아도 자연스럽게 터짐).
            sparkParticles.Play();
        }
    }
}
