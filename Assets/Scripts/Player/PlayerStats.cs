using UnityEngine;
using SurvivalDrone.Core;

namespace SurvivalDrone.Player
{
    // 플레이어의 "기본 능력치"를 관리하는 스크립트.
    // 레벨업 선택지에서 "이동속도 증가", "최대체력 증가"를 고르면 여기 값이 올라간다.
    public class PlayerStats : MonoBehaviour
    {
        // 강화되기 전, 처음 시작할 때의 기본 이동속도.
        [SerializeField] private float baseMoveSpeed = 5f;

        // 강화되기 전, 처음 시작할 때의 기본 최대체력.
        [SerializeField] private float baseMaxHealth = 100f;

        // 레벨업으로 추가된 이동속도 보너스 총합.
        public float MoveSpeedBonus { get; private set; }

        // 레벨업으로 추가된 최대체력 보너스 총합.
        public float MaxHealthBonus { get; private set; }

        // 실제로 게임에서 사용되는 최종 이동속도 = 기본값 + 보너스.
        public float MoveSpeed => baseMoveSpeed + MoveSpeedBonus;

        // 실제로 게임에서 사용되는 최종 최대체력 = 기본값 + 보너스.
        public float MaxHealth => baseMaxHealth + MaxHealthBonus;

        private void Start()
        {
            // 게임 시작 시, 같은 오브젝트에 붙어있는 Health 컴포넌트의
            // 최대체력 값을 PlayerStats 기준으로 맞춰준다(가득 채운 상태로 시작).
            var health = GetComponent<Health>();
            if (health != null) health.SetMaxHealth(MaxHealth, MaxHealth);
        }

        // 이동속도 보너스를 더해주는 함수 (레벨업 선택지에서 호출됨).
        public void AddMoveSpeed(float amount)
        {
            MoveSpeedBonus += amount;
        }

        // 최대체력 보너스를 더해주는 함수 (레벨업 선택지에서 호출됨).
        public void AddMaxHealth(float amount)
        {
            MaxHealthBonus += amount;

            // 최대체력이 늘어난 만큼 Health 컴포넌트에도 반영하고,
            // 늘어난 양만큼 현재 체력도 즉시 채워준다(레벨업 보상 느낌).
            var health = GetComponent<Health>();
            if (health != null) health.SetMaxHealth(MaxHealth, amount);
        }
    }
}
