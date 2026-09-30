using SurvivalDrone.Drones;

namespace SurvivalDrone.UI
{
    // 화면에 보여줄 드론 이름(한글)을 한 곳에서 정해두는 도우미.
    // 뽑기 결과 카드, 격납고처럼 여러 화면이 같은 이름을 쓰도록 한다.
    public static class DroneNames
    {
        public static string Get(DroneType type)
        {
            switch (type)
            {
                case DroneType.Melee: return "근접 드론";
                case DroneType.Sniper: return "저격 드론";
                case DroneType.Collector: return "수집 드론";
                case DroneType.Explosion: return "폭발 드론";
                case DroneType.Heal: return "회복 드론";
                default: return type.ToString();
            }
        }
    }
}
