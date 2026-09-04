using UnityEngine;
using SurvivalDrone.Pickups;

namespace SurvivalDrone.Drones
{
    // 수집 드론: 직접 공격하지는 않고, 대신 XP 오브를 끌어당기는 범위를 넓혀주는 지원형 드론.
    // 기획서 4장 기준 - 강화될수록 좋아지는 것은 "끌어당기는 범위" 하나뿐이다.
    public class CollectorDrone : DroneBase
    {
        // 레벨 1일 때 추가로 넓혀주는 자석 반경.
        [SerializeField] private float baseExtraRadius = 3f;

        protected override void Update()
        {
            // 수집 드론도 플레이어를 따라다니기만 하면 되므로 부모의 기본 동작을 그대로 사용.
            base.Update();

            // 매 프레임 현재 레벨에 맞는 반경 값을 계산해서 전체 게임에서 공유하는
            // MagnetField(자석 범위 값)에 반영한다. XPOrb 스크립트가 이 값을 읽어서 사용한다.
            MagnetField.SetExtraRadius(baseExtraRadius * GetScale());
        }

        // 이 드론이 비활성화되거나 파괴될 때(예: 게임 재시작) 자석 범위를 0으로 되돌려서
        // 드론이 없는데도 자석 효과가 남아있는 버그를 방지한다.
        private void OnDisable()
        {
            MagnetField.SetExtraRadius(0f);
        }
    }
}
