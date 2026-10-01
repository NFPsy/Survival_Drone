namespace SurvivalDrone.Meta
{
    // 한 판의 "마일스톤"(중간 목표) 수치 모음.
    // 마일스톤 = 판 도중 정해진 시간에 도달하면 주는 코어 보상. 계정 전체에서 각각 딱 한 번만 받는다.
    //  - 0번: 3분 도달, 1번: 6분 도달 (판 도중에 지급)
    //  - 2번: 처음 클리어 (판이 끝날 때 지급)
    // 같은 시간(3분·6분)에 미니 보스도 나오므로, 적 스포너가 이 시간표를 같이 읽는다. (한 곳에서만 고치면 둘이 같이 바뀐다)
    public static class MatchMilestones
    {
        // 3분, 6분 마일스톤이 열리는 시간(초).
        public static readonly float[] Seconds = { 180f, 360f };

        // 마일스톤별 코어 보상. 0번 = 3분, 1번 = 6분, 2번 = 처음 클리어.
        public static readonly int[] Cores = { 100, 150, 300 };

        // 처음 클리어 마일스톤의 번호 (Cores 배열에서의 위치).
        public const int ClearIndex = 2;
    }
}
