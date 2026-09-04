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

                // 레벨이 오를수록 회복량도 함께 늘어난다.
                ownerHealth.Heal(baseHealAmount * scale);
            }
        }
    }
}
