using System;
using SurvivalDrone.Drones;

namespace SurvivalDrone.Meta
{
    // 내가 가진 드론 하나의 저장용 정보. 저장 파일(SaveData)의 보유 드론 목록에 이 항목이 들어간다.
    // 목록에 항목이 있으면 "그 종류를 보유 중"이라는 뜻이고, 없으면 미보유다. (종류당 1개만 가질 수 있다)
    [Serializable]
    public class OwnedDroneData
    {
        // 드론 종류 (근접/저격/수집/폭발/회복)
        public DroneType droneType;

        // 현재 등급. 더 높은 등급이 뽑히면 올라가고, 내려가는 일은 없다.
        public GachaRarity rarity;

        // 강화 레벨 (1부터). 등급이 올라가도 유지된다.
        public int level = 1;

        // 모아둔 설계도 조각. 중복으로 뽑힐 때 쌓이고 강화할 때 쓴다.
        public int shards;
    }
}
