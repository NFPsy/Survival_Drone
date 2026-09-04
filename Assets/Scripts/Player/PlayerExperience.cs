using System;
using UnityEngine;
using SurvivalDrone.Core;

namespace SurvivalDrone.Player
{
    // 플레이어의 경험치(XP)와 레벨을 관리하는 스크립트.
    // XP 오브를 먹을 때마다 AddXP가 호출되고, 다 채워지면 레벨업이 일어난다.
    public class PlayerExperience : MonoBehaviour
    {
        // 처음(레벨 1 -> 2) 레벨업에 필요한 XP 양.
        [SerializeField] private float startingXPToNextLevel = 10f;

        // 레벨이 오를 때마다 다음 레벨업에 필요한 XP가 몇 배로 늘어나는지.
        // 1.18이면 매 레벨마다 18%씩 더 많은 XP가 필요해진다(뒤로 갈수록 레벨업이 느려짐).
        [SerializeField] private float xpGrowthPerLevel = 1.18f;

        // 현재 레벨. 1레벨부터 시작.
        public int Level { get; private set; } = 1;

        // 현재까지 모은 XP (다음 레벨업 기준으로 초기화됨).
        public float CurrentXP { get; private set; }

        // 다음 레벨업까지 필요한 총 XP.
        public float XPToNextLevel { get; private set; }

        // 레벨업이 일어날 때 호출되는 이벤트. 새 레벨 값을 전달한다. (레벨업 UI가 이걸 구독함)
        public event Action<int> OnLevelUp;

        // XP가 바뀔 때마다 호출되는 이벤트. (현재 XP, 다음 레벨까지 필요한 XP) 순서. (HUD의 XP바가 구독함)
        public event Action<float, float> OnXPChanged;

        private void Awake()
        {
            XPToNextLevel = startingXPToNextLevel;
        }

        // XP 오브를 먹었을 때 호출되는 함수.
        public void AddXP(float amount)
        {
            if (amount <= 0f) return;

            CurrentXP += amount;

            // while문을 쓰는 이유: 한 번에 큰 XP를 얻어서 레벨이 여러 번 한꺼번에 오를 수도 있기 때문.
            while (CurrentXP >= XPToNextLevel)
            {
                // 다음 레벨업 기준을 넘긴 만큼만 남기고 초과분은 다음 레벨로 이월.
                CurrentXP -= XPToNextLevel;
                Level++;

                // 다음 레벨업에 필요한 XP를 더 늘려서(성장) 뒤로 갈수록 레벨업이 어려워지게 한다.
                XPToNextLevel = Mathf.Round(XPToNextLevel * xpGrowthPerLevel);

                // 레벨업이 일어났다는 것을 다른 스크립트(레벨업 UI 등)에 알림.
                OnLevelUp?.Invoke(Level);
                GameEvents.RaisePlayerLevelUp(Level);
            }

            // XP 값이 바뀌었다는 것을 HUD 등에 알림 (레벨업 여부와 상관없이 항상 호출).
            OnXPChanged?.Invoke(CurrentXP, XPToNextLevel);
        }
    }
}
