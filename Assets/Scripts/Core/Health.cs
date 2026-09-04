using System;
using UnityEngine;

namespace SurvivalDrone.Core
{
    // 체력(HP)을 가진 모든 오브젝트가 공통으로 사용하는 컴포넌트.
    // 플레이어(Player)와 적(Enemy) 둘 다 이 컴포넌트를 붙여서 사용한다.
    public class Health : MonoBehaviour
    {
        // 인스펙터(Inspector)에서 조절 가능한 "최대 체력" 값.
        [SerializeField] private float maxHealth = 10f;

        // 외부에서는 읽기만 가능하도록 프로퍼티로 노출.
        public float MaxHealth => maxHealth;

        // 현재 체력. private set이라 이 클래스 내부에서만 값을 바꿀 수 있다.
        public float CurrentHealth { get; private set; }

        // 이미 죽었는지 여부 (중복으로 죽는 것을 방지하기 위한 플래그)
        public bool IsDead { get; private set; }

        // 체력이 바뀔 때마다 알림을 받고 싶은 다른 스크립트(예: 체력바 UI)가 구독하는 이벤트.
        // 매개변수는 (현재 체력, 최대 체력) 순서.
        public event Action<float, float> OnHealthChanged;

        // 죽었을 때 한 번 호출되는 이벤트. (예: 적이면 사라지고, 플레이어면 게임오버 처리)
        public event Action OnDeath;

        // "피해를 입었다"는 것만 콕 집어서 알려주는 이벤트 (회복과는 구분됨).
        // 매개변수는 이번에 실제로 입은 피해량. 피격 이펙트(DamageFlash)가 이걸 구독해서 사용한다.
        public event Action<float> OnDamaged;

        // 게임 오브젝트가 생성될 때 최초 1회 호출됨.
        private void Awake()
        {
            // 시작할 때는 체력을 가득 채운 상태로 시작한다.
            CurrentHealth = maxHealth;
        }

        // 최대 체력을 바꾸고 싶을 때 사용 (레벨업으로 최대 체력이 늘어나는 경우 등).
        // healAmount를 함께 주면 늘어난 만큼 현재 체력도 같이 회복시켜준다.
        public void SetMaxHealth(float newMax, float healAmount = 0f)
        {
            maxHealth = newMax;
            // 현재 체력이 최대 체력을 넘지 않도록 Mathf.Min으로 제한.
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + healAmount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        // 피해를 입을 때 호출하는 함수. amount만큼 체력을 깎는다.
        public void TakeDamage(float amount)
        {
            // 이미 죽었거나, 피해량이 0 이하면 아무것도 하지 않는다.
            if (IsDead || amount <= 0f) return;

            // 체력이 0 밑으로 내려가지 않도록 Mathf.Max로 최소값을 0으로 고정.
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            OnDamaged?.Invoke(amount);

            // 체력이 0이 되면 사망 처리.
            if (CurrentHealth <= 0f)
            {
                IsDead = true;
                OnDeath?.Invoke();
            }
        }

        // 체력을 회복시키는 함수 (회복 드론 등에서 사용 예정).
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            // 체력이 최대치를 넘지 않도록 Mathf.Min으로 제한.
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }
    }
}
