using UnityEngine;

namespace SurvivalDrone.Enemies
{
    // 적의 "종류"를 구분하기 위한 값. 기획서 5장의 5종류 로봇과 대응된다.
    public enum EnemyKind { Weak, Tough, Fast, Strong, Boss }

    // 적 한 종류의 스탯(수치) 데이터를 담아두는 상자.
    // ScriptableObject로 만들어서, 코드를 건드리지 않고도 Unity 에디터에서
    // 체력/속도/피해량 같은 수치를 에셋 파일(.asset)로 따로 관리할 수 있게 했다.
    // (기획서 5장의 "로봇 종류별 표"를 그대로 데이터화한 것이라고 보면 된다)
    [CreateAssetMenu(menuName = "SurvivalDrone/Enemy Definition", fileName = "EnemyDefinition")]
    public class EnemyDefinition : ScriptableObject
    {
        // 이 데이터가 어떤 종류의 적인지.
        public EnemyKind kind;

        // 에디터에서 보기 편하게 붙이는 이름 (예: "약한 로봇").
        public string displayName = "Enemy";

        // 최대 체력.
        public float maxHealth = 10f;

        // 이동 속도.
        public float moveSpeed = 3.5f;

        // 플레이어와 부딪혔을 때 주는 피해량.
        public float contactDamage = 5f;

        // 게임 시작 후 몇 초가 지나야 이 적이 등장하기 시작하는지 (기획서의 "처음 등장 시점").
        public float unlockTime = 0f;

        // 이 적을 처치했을 때 지급하는 경험치(XP) 양.
        public float xpReward = 1f;

        // 에디터에서 구분하기 쉽도록 사용하는 참고용 색상(실제 렌더링에는 프리팹의 머티리얼을 사용).
        public Color debugColor = Color.white;
    }
}
