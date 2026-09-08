using UnityEngine;

namespace SurvivalDrone.Core
{
    // 게임 난이도 3단계.
    public enum DifficultyLevel { Easy, Normal, Hard }

    // 지금까지 밸런스를 맞춰온 수치(적 체력/피해량/스폰 속도 등)는 전부 "어려움" 기준이다.
    // 그래서 적 데이터(EnemyDefinition)나 스포너의 수치 자체를 難이도별로 3배 만들지 않고,
    // 이 클래스가 제공하는 "배율(Multiplier)"을 곱해서 쉬움/보통에서는 낮춰주는 방식으로 구현했다.
    // 이렇게 하면 나중에 밸런스를 또 조정하더라도 한 곳(어려움 기준 수치)만 고치면 된다.
    public static class GameDifficulty
    {
        // PlayerPrefs에 난이도를 저장/불러올 때 쓰는 키 이름.
        private const string DifficultyKey = "Difficulty";

        // 현재 선택된 난이도. 처음 실행(저장된 값이 없음)이면 "보통"을 기본값으로 사용한다.
        public static DifficultyLevel Current
        {
            get => (DifficultyLevel)PlayerPrefs.GetInt(DifficultyKey, (int)DifficultyLevel.Normal);
            set => PlayerPrefs.SetInt(DifficultyKey, (int)value);
        }

        // 적의 체력/접촉 피해량에 곱해줄 배율. 어려움(Hard)은 지금까지의 밸런스 그대로(1배),
        // 보통은 20% 약하게, 쉬움은 40% 약하게 만든다.
        //
        // 스폰 속도/최대 마릿수는 일부러 난이도별로 낮추지 않는다. 스폰을 줄이면 쉬움/보통에서
        // 오히려 XP 구슬을 모을 기회가 줄어서 레벨업이 더 느려지는, 의도와 반대되는 결과가 났기 때문.
        // 그래서 "쉬움/보통 = 적은 약하지만 그만큼 많이 나온다"는 방향으로 통일했다.
        public static float EnemyStatMultiplier => Current switch
        {
            DifficultyLevel.Easy => 0.6f,
            DifficultyLevel.Normal => 0.8f,
            _ => 1f,
        };
    }
}
