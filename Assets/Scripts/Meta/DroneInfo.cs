using SurvivalDrone.Drones;

namespace SurvivalDrone.Meta
{
    // 보유 드론 하나의 "읽기 전용 사본". 격납고 같은 화면(UI)이 정보를 읽어갈 때 쓴다.
    // 저장 데이터(OwnedDroneData)를 그대로 넘기면 화면 쪽에서 실수로 값을 바꿀 수 있어서, 복사본을 만들어 준다.
    public readonly struct DroneInfo
    {
        public readonly DroneType droneType;
        public readonly GachaRarity rarity;
        public readonly int level;
        public readonly int shards;

        public DroneInfo(DroneType droneType, GachaRarity rarity, int level, int shards)
        {
            this.droneType = droneType;
            this.rarity = rarity;
            this.level = level;
            this.shards = shards;
        }
    }
}
