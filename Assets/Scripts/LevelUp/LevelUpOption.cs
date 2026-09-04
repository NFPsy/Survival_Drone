using SurvivalDrone.Drones;

namespace SurvivalDrone.LevelUp
{
    // 레벨업 선택지가 어떤 "종류"인지 구분하는 값.
    // 기획서 7장 기준 - 신규 드론 / 드론 강화 / 능력치 강화 세 가지 중 하나가 섞여서 나온다.
    public enum LevelUpOptionKind { NewDrone, UpgradeDrone, StatBoost }

    // 능력치 강화를 골랐을 때, 어떤 능력치를 올릴지 구분하는 값.
    public enum StatBoostKind { MoveSpeed, MaxHealth }

    // 레벨업 화면에 보여줄 선택지 하나를 표현하는 데이터 클래스.
    // MonoBehaviour가 아니라 순수 데이터라서 오브젝트에 붙이지 않고 그냥 new로 생성해서 사용한다.
    public class LevelUpOption
    {
        // 이 선택지가 신규 드론인지, 드론 강화인지, 능력치 강화인지.
        public LevelUpOptionKind Kind;

        // Kind가 NewDrone/UpgradeDrone일 때, 어떤 드론을 대상으로 하는지.
        public DroneType DroneType;

        // Kind가 StatBoost일 때, 어떤 능력치를 올리는지.
        public StatBoostKind StatBoost;

        // 화면에 보여줄 제목 (예: "신규 드론: Sniper").
        public string Title;

        // 화면에 보여줄 설명 (예: "새로운 드론을 장착합니다.").
        public string Description;
    }
}
