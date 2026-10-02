namespace SurvivalDrone.Meta
{
    // 한 판의 "마일스톤"(중간 목표) 수치 모음.
    // 마일스톤 = 정해진 목표에 도달하면 받을 수 있는 코어 보상. 계정 전체에서 각각 딱 한 번만 받는다.
    //  - 0번: 3분 도달, 1번: 6분 도달 (판 도중에 "달성"으로 기록됨)
    //  - 2번: 처음 클리어 (판이 끝날 때 "달성"으로 기록됨)
    // 달성은 판에서 자동으로 기록되지만, 코어는 로비의 마일스톤 창에서 "획득"을 눌러야 받는다. (일일 퀘스트와 같은 방식)
    // 같은 시간(3분·6분)에 미니 보스도 나오므로, 적 스포너가 이 시간표를 같이 읽는다. (한 곳에서만 고치면 둘이 같이 바뀐다)
    public static class MatchMilestones
    {
        // 3분, 6분 마일스톤이 열리는 시간(초).
        public static readonly float[] Seconds = { 180f, 360f };

        // 마일스톤별 코어 보상. 0번 = 3분, 1번 = 6분, 2번 = 처음 클리어.
        public static readonly int[] Cores = { 100, 150, 300 };

        // 로비 마일스톤 창에 보여줄 이름과 달성 조건 문구. (Cores와 같은 순서)
        public static readonly string[] Titles = { "3분", "6분", "클리어" };
        public static readonly string[] Conditions = { "3분 이상 생존에서 획득", "6분 이상 생존에서 획득", "처음 클리어에서 획득" };

        // 처음 클리어 마일스톤의 번호 (Cores 배열에서의 위치).
        public const int ClearIndex = 2;

        // 마일스톤 개수.
        public static int Count => Cores.Length;

        private static bool IsValid(int index) => index >= 0 && index < Count;

        // 이미 코어를 받았는지.
        public static bool IsClaimed(SaveData data, int index)
        {
            return IsValid(index) && (data.milestoneClaimedMask & (1 << index)) != 0;
        }

        // 달성했는지 (코어를 이미 받은 것도 달성한 것으로 본다. 달성 기록이 생기기 전에 받은 옛 저장도 안전하게 읽히도록).
        public static bool IsDone(SaveData data, int index)
        {
            return IsValid(index) && ((data.milestoneDoneMask | data.milestoneClaimedMask) & (1 << index)) != 0;
        }

        // 마일스톤을 달성 처리한다. 새로 달성한 것이면 true, 이미 달성했거나 번호가 잘못됐으면 false.
        public static bool MarkDone(SaveData data, int index)
        {
            if (!IsValid(index) || IsDone(data, index)) return false;
            data.milestoneDoneMask |= 1 << index;
            return true;
        }
    }
}
