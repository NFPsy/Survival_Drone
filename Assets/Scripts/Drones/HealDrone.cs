using UnityEngine;
using SurvivalDrone.Core;

namespace SurvivalDrone.Drones
{
    // 회복 드론: 다른 드론들과 달리 공격을 전혀 하지 않는다.
    // 대신 일정 시간마다 주인(플레이어)의 체력을 서서히 채워주는 지원형 드론이다.
    //
    // 기획서 4장 기준 - 강화될수록 좋아지는 것은 "회복량 또는 회복 주기"이므로,
    // 레벨이 오르면 한 번에 회복하는 양이 늘고, 회복 간격은 더 짧아지도록(더 자주 회복하도록) 만들었다.
    public class HealDrone : DroneBase
    {
        // 한 번에 회복시켜주는 체력량 (레벨에 따라 배율이 곱해짐).
        [SerializeField] private float baseHealAmount = 1.2f;

        // 몇 초마다 한 번씩 회복시켜주는지.
        // (밸런스 조정: 원래 2초/2였는데 실제로 플레이해보니 접촉 피해를 사실상 무효화할 만큼 강해서 낮췄다.)
        [SerializeField] private float baseHealInterval = 2.5f;

        // 다음 회복까지 남은 시간.
        private float healTimer;

        // 주인(플레이어)의 체력 컴포넌트. 처음엔 비어있다가 owner가 정해진 뒤에 찾아서 저장해둔다.
        private Health ownerHealth;

        // 회복할 때마다 재생할 파티클. 인스펙터에서 지정 안 해도 자식에서 자동으로 찾는다.
        [SerializeField] private ParticleSystem healEffect;

        // 회복할 때마다 재생할 효과음.
        [SerializeField] private AudioClip healSound;

        // 체력이 깎여 있을 때 회복하면 보여 주는 이펙트의 원본(HealEffect.prefab): 드론 → 플레이어 초록 빛줄기 + "+" 모양 + 발밑 링.
        // 드론이 0.5배로 줄어 있어서, 자식으로 두면 이펙트도 같이 작아진다. 그래서 따로(최상위에) 하나 만들어 두고 쓴다.
        [SerializeField] private HealBeamEffect healVisualPrefab;
        private HealBeamEffect healVisual;

        private void Awake()
        {
            if (healEffect == null) healEffect = GetComponentInChildren<ParticleSystem>();
            if (healVisualPrefab != null) healVisual = Instantiate(healVisualPrefab);
        }

        private void OnDestroy()
        {
            // 드론이 사라지면(씬을 나가면) 따로 만든 이펙트도 같이 지운다.
            if (healVisual != null) Destroy(healVisual.gameObject);
        }

        protected override void Update()
        {
            // 회복 드론도 플레이어를 따라다니기만 하면 되므로 부모의 기본 동작을 그대로 사용.
            base.Update();

            // 아직 주인의 Health 컴포넌트를 찾지 못했다면(owner가 막 정해진 직후) 한 번 찾아서 캐싱.
            if (ownerHealth == null && owner != null)
            {
                ownerHealth = owner.GetComponent<Health>();
            }

            // 주인의 체력 컴포넌트를 못 찾았으면(아직 주인이 없거나 오류 상황) 아무것도 하지 않는다.
            if (ownerHealth == null) return;

            float scale = GetScale();

            // 회복 쿨타임을 줄여나가다가 다 되면 한 번 회복시켜준다.
            healTimer -= Time.deltaTime;
            if (healTimer <= 0f)
            {
                // 레벨이 오를수록(scale이 커질수록) 회복 간격이 짧아져서 더 자주 회복하게 된다.
                healTimer = baseHealInterval / scale;

                // 체력이 깎여 있는지 회복하기 "전에" 확인해 둔다. (회복하고 나면 가득 찰 수 있다)
                bool wasHurt = ownerHealth.CurrentHealth < ownerHealth.MaxHealth - 0.01f;

                // 레벨이 오를수록 회복량도 함께 늘어난다.
                float healthBeforeHeal = ownerHealth.CurrentHealth;
                ownerHealth.Heal(baseHealAmount * scale);
                DroneContributionLog.RecordHeal(ownerHealth.CurrentHealth - healthBeforeHeal);   // 판 기록: 실제로 채운 체력(가득이면 0)

                // 회복할 때마다 작은 파티클은 늘 재생한다. 체력이 가득일 때는 이것만 나와서 화면이 시끄럽지 않다.
                healEffect?.Play();

                // 실제로 체력이 깎여 있었을 때만 큰 이펙트(빛줄기 + "+" + 링)를 보여 준다.
                if (wasHurt && healVisual != null && owner != null) healVisual.Play(transform, owner);
                SurvivalDrone.Core.AudioManager.Instance?.PlaySfx(healSound, 0.6f);
            }
        }
    }
}
