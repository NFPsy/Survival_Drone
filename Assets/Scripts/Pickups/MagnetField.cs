namespace SurvivalDrone.Pickups
{
    // 수집 드론(Collector Drone)이 "XP를 끌어당기는 범위"를 얼마나 넓혀주는지
    // 게임 전체에서 공유하는 값. static이라서 오브젝트에 붙이지 않고 어디서든 접근 가능.
    //
    // 수집 드론이 있으면 이 값이 커지고, 없으면(비활성화되면) 0으로 돌아간다.
    // XPOrb(경험치 구슬) 스크립트가 이 값을 읽어서 자석 범위를 계산한다.
    public static class MagnetField
    {
        // 현재 추가로 적용되는 자석 반경. 기본값 0 (수집 드론이 없으면 추가 범위 없음).
        public static float ExtraRadius { get; private set; }

        // 수집 드론이 자신의 레벨에 맞는 반경 값을 매 프레임 이 함수로 갱신한다.
        public static void SetExtraRadius(float value)
        {
            ExtraRadius = value;
        }
    }
}
