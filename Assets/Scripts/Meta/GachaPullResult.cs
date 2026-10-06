using SurvivalDrone.Drones;

namespace SurvivalDrone.Meta
{
    // 뽑기 1회의 결과. 뽑기 결과 화면(UI)이 이 값을 받아서 카드를 그린다.
    // struct(값 형식)로 만든 이유: 10연이면 10개씩 만들어지는 작은 데이터라서 가볍게 다루려고.
    //
    // 참고: "신규 / 중복 / 조각 환산량"은 여기에 없다.
    //  그건 "이미 이 드론을 갖고 있는가?"를 알아야 정할 수 있어서, 보유 목록을 관리하는
    //  DroneInventory를 만들 때 그쪽에서 이 결과를 받아 판단한다.
    public readonly struct GachaPullResult
    {
        // 나온 등급 (N/R/SR/SSR)
        public readonly GachaRarity rarity;

        // 나온 드론 종류 (근접/저격/수집/폭발/회복)
        public readonly DroneType drone;

        // 이번 뽑기가 "천장 확정"으로 나온 SSR인가? (확률로 나온 SSR이면 false)
        public readonly bool isPityGuaranteed;

        // 이번 뽑기를 처리한 "직후"의 천장 카운트 (SSR이 나왔으면 0). 천장 게이지 UI에 쓴다.
        public readonly int pityAfter;

        // 이번 뽑기가 "소천장(SR 이상 보장)"으로 나온 결과인가? (확률로 SR 이상이 나온 것이면 false)
        public readonly bool isSoftPityGuaranteed;

        // 이번 뽑기를 처리한 직후의 소천장 카운트 (SR 이상이 나왔으면 0). 소천장 게이지 UI에 쓴다.
        public readonly int softPityAfter;

        // 소천장 값은 맨 뒤에 기본값을 두어서, 옛 호출(4개 값만 넘기던 곳)이 그대로 동작한다.
        public GachaPullResult(GachaRarity rarity, DroneType drone, bool isPityGuaranteed, int pityAfter, bool isSoftPityGuaranteed = false, int softPityAfter = 0)
        {
            this.rarity = rarity;
            this.drone = drone;
            this.isPityGuaranteed = isPityGuaranteed;
            this.pityAfter = pityAfter;
            this.isSoftPityGuaranteed = isSoftPityGuaranteed;
            this.softPityAfter = softPityAfter;
        }
    }
}
