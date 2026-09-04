using System;
using UnityEngine;

namespace SurvivalDrone.Core
{
    // 게임 전체에서 공용으로 사용하는 "이벤트 알림판" 같은 클래스.
    // static이라서 별도로 GameObject에 붙이지 않아도 어디서든 GameEvents.OnEnemyKilled 처럼 접근할 수 있다.
    // 예: 적이 죽었을 때 여러 시스템(사운드, 이펙트, 통계 등)에 한 번에 알리고 싶을 때 사용.
    public static class GameEvents
    {
        // 적이 죽었을 때 발생하는 이벤트. 죽은 위치(Vector3)를 함께 전달한다.
        public static event Action<Vector3> OnEnemyKilled;

        // 플레이어가 레벨업했을 때 발생하는 이벤트. 새로운 레벨 값을 함께 전달한다.
        public static event Action<int> OnPlayerLevelUp;

        // 플레이어가 죽었을 때 발생하는 이벤트.
        public static event Action OnPlayerDied;

        // 매치(한 판)를 승리로 종료했을 때 발생하는 이벤트.
        public static event Action OnMatchWon;

        // 아래 4개 함수는 이벤트를 "발생시키는" 역할만 한다.
        // ?.Invoke()는 이 이벤트를 구독하는 곳이 하나도 없어도 에러 없이 안전하게 넘어가기 위한 문법.
        public static void RaiseEnemyKilled(Vector3 position) => OnEnemyKilled?.Invoke(position);
        public static void RaisePlayerLevelUp(int newLevel) => OnPlayerLevelUp?.Invoke(newLevel);
        public static void RaisePlayerDied() => OnPlayerDied?.Invoke();
        public static void RaiseMatchWon() => OnMatchWon?.Invoke();
    }
}
