using System.Collections.Generic;

namespace SurvivalDrone.Meta
{
    // 한 판의 "마일스톤"(중간 목표) 수치 모음.
    // 마일스톤 = 정해진 목표에 도달하면 받을 수 있는 코어 보상. **스테이지마다** 각각 딱 한 번씩 받고, 스테이지가 오를수록 보상이 늘어난다.
    //  - 0번: 3분 도달, 1번: 6분 도달 (판 도중에 "달성"으로 기록됨)
    //  - 2번: 처음 클리어 (판이 끝날 때 "달성"으로 기록됨)
    // 달성은 판에서 자동으로 기록되지만, 코어는 로비의 마일스톤 창에서 "획득"을 눌러야 받는다. (일일 퀘스트와 같은 방식)
    // 같은 시간(3분·6분)에 미니 보스도 나오므로, 적 스포너가 이 시간표를 같이 읽는다. (한 곳에서만 고치면 둘이 같이 바뀐다)
    public static class MatchMilestones
    {
        // 3분, 6분 마일스톤이 열리는 시간(초).
        public static readonly float[] Seconds = { 180f, 360f };

        // 스테이지별 마일스톤 코어 보상. 바깥 칸 = 스테이지 1, 2, 3 순서(스테이지가 오를수록 더 준다),
        // 안쪽 칸 = 0번 3분, 1번 6분, 2번 처음 클리어. 스테이지 칸이 모자라면 마지막 스테이지 값을 쓴다.
        //   스테이지 1: 100 / 150 / 300 (합계 550)
        //   스테이지 2: 200 / 300 / 400 (합계 900)
        //   스테이지 3: 300 / 450 / 600 (합계 1,350)
        private static readonly int[][] CoresByStage =
        {
            new[] { 100, 150, 300 },
            new[] { 200, 300, 400 },
            new[] { 300, 450, 600 },
        };

        // 스테이지(0부터)의 마일스톤(번호 index) 코어 보상. 예) 스테이지 2의 6분 = 300.
        public static int GetCore(int stageIndex, int index)
        {
            int stage = System.Math.Max(0, System.Math.Min(stageIndex, CoresByStage.Length - 1));
            return CoresByStage[stage][index];
        }

        // 로비 마일스톤 창에 보여줄 이름과 달성 조건 문구. (마일스톤 번호 순서)
        public static readonly string[] Titles = { "3분", "6분", "클리어" };
        public static readonly string[] Conditions = { "3분 이상 생존에서 획득", "6분 이상 생존에서 획득", "처음 클리어에서 획득" };

        // 처음 클리어 마일스톤의 번호 (스테이지 안에서의 위치).
        public const int ClearIndex = 2;

        // 마일스톤 개수 (스테이지 하나당).
        public static int Count => Titles.Length;

        private static bool IsValid(int stageIndex, int index) => stageIndex >= 0 && index >= 0 && index < Count;

        private static int GetMask(List<int> masks, int stageIndex) => stageIndex < masks.Count ? masks[stageIndex] : 0;

        private static void AddBit(List<int> masks, int stageIndex, int index)
        {
            while (masks.Count <= stageIndex) masks.Add(0);
            masks[stageIndex] |= 1 << index;
        }

        // 이 스테이지에서 이미 코어를 받은 마일스톤 비트. (스테이지 1은 옛 저장의 계정 전체 기록도 함께 읽는다)
        private static int ClaimedMask(SaveData data, int stageIndex)
        {
            return GetMask(data.stageMilestoneClaimedMasks, stageIndex) | (stageIndex == 0 ? data.milestoneClaimedMask : 0);
        }

        // 이 스테이지에서 달성한 마일스톤 비트. 코어를 이미 받은 것도 달성한 것으로 본다.
        private static int DoneMask(SaveData data, int stageIndex)
        {
            return GetMask(data.stageMilestoneDoneMasks, stageIndex) | (stageIndex == 0 ? data.milestoneDoneMask : 0) | ClaimedMask(data, stageIndex);
        }

        // 이미 코어를 받았는지. stageIndex는 0부터(스테이지 1 = 0).
        public static bool IsClaimed(SaveData data, int stageIndex, int index)
        {
            return IsValid(stageIndex, index) && (ClaimedMask(data, stageIndex) & (1 << index)) != 0;
        }

        // 달성했는지.
        public static bool IsDone(SaveData data, int stageIndex, int index)
        {
            return IsValid(stageIndex, index) && (DoneMask(data, stageIndex) & (1 << index)) != 0;
        }

        // 마일스톤을 달성 처리한다. 새로 달성한 것이면 true, 이미 달성했거나 번호가 잘못됐으면 false.
        public static bool MarkDone(SaveData data, int stageIndex, int index)
        {
            if (!IsValid(stageIndex, index) || IsDone(data, stageIndex, index)) return false;
            AddBit(data.stageMilestoneDoneMasks, stageIndex, index);
            return true;
        }

        // 코어를 받았다고 기록한다. (지급은 CurrencyManager.TryClaimMilestone이 한다)
        public static void MarkClaimed(SaveData data, int stageIndex, int index)
        {
            if (!IsValid(stageIndex, index)) return;
            AddBit(data.stageMilestoneClaimedMasks, stageIndex, index);
        }
    }
}
