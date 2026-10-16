using System.Collections.Generic;

namespace SurvivalDrone.Meta
{
    // 한 판의 "마일스톤"(중간 목표) 수치 모음.
    // 마일스톤 = 정해진 목표에 도달하면 받을 수 있는 보상. 노멀 스테이지는 코어, 하드 스테이지는 만능 조각이다. **스테이지마다** 각각 딱 한 번씩 받고, 스테이지가 오를수록 보상이 늘어난다.
    //  - 0번: 3분 도달, 1번: 6분 도달 (판 도중에 "달성"으로 기록됨)
    //  - 2번: 처음 클리어 (판이 끝날 때 "달성"으로 기록됨)
    // 달성은 판에서 자동으로 기록되지만, 보상은 로비의 마일스톤 창에서 "획득"을 눌러야 받는다. (일일 퀘스트와 같은 방식)
    // 같은 시간(3분·6분)에 미니 보스도 나오므로, 적 스포너가 이 시간표를 같이 읽는다. (한 곳에서만 고치면 둘이 같이 바뀐다)
    public static class MatchMilestones
    {
        // 3분, 6분 마일스톤이 열리는 시간(초).
        public static readonly float[] Seconds = { 180f, 360f };

        // 스테이지별 마일스톤 코어 보상. 바깥 칸 = 스테이지 1 ~ 5 순서(스테이지가 오를수록 더 준다),
        // 안쪽 칸 = 0번 3분, 1번 6분, 2번 처음 클리어. 스테이지 칸이 모자라면 마지막 스테이지 값을 쓴다.
        // 규칙: 스테이지 n의 3분 = 50×(n+1), 6분 = 3분×1.5, 클리어 = 3분×2.
        // (10/8에 3스테이지 → 5스테이지로 늘렸다. 예전 스테이지 1·2·3이 지금 1·3·5이고 2·4는 그 사이 값.)
        //   스테이지 1: 100 / 150 / 200 (합계 450)
        //   스테이지 2: 150 / 225 / 300 (합계 675)
        //   스테이지 3: 200 / 300 / 400 (합계 900)
        //   스테이지 4: 250 / 375 / 500 (합계 1,125)
        //   스테이지 5: 300 / 450 / 600 (합계 1,350)   → 전체 합계 4,500
        private static readonly int[][] CoresByStage =
        {
            new[] { 100, 150, 200 },
            new[] { 150, 225, 300 },
            new[] { 200, 300, 400 },
            new[] { 250, 375, 500 },
            new[] { 300, 450, 600 },
        };

        // 하드 스테이지 1~5(전체 목록의 6~10번째)의 마일스톤 보상은 코어가 아니라 **만능 조각**이다. (코어 수입이 늘어나지 않게 하려는 설계)
        // 바깥 칸 = 하드 1 ~ 5, 안쪽 칸 = 0번 3분, 1번 6분, 2번 처음 클리어. 합계는 30 / 40 / 60 / 80 / 100 (하드 전부 310조각).
        // 조각은 받는 순간 "다음 강화에 가장 가까운 드론 한 마리"에 자동으로 들어간다(DroneInventory.AddUniversalShards).
        // 클리어 칸이 합계의 약 절반이라, 못 깨도 3분·6분까지 가면 일부를 받는다. 값은 OBT 로그를 보고 고친다.
        private static readonly int[][] ShardsByHardStage =
        {
            new[] { 5, 10, 15 },
            new[] { 10, 10, 20 },
            new[] { 15, 15, 30 },
            new[] { 20, 20, 40 },
            new[] { 25, 25, 50 },
        };

        // 노멀 스테이지 개수 (하드 스테이지가 시작되는 위치이기도 하다).
        public static int NormalCount => CoresByStage.Length;

        // 이 스테이지(0부터)가 하드 스테이지(노멀 뒤에 붙는 6~10번째)인지. 하드 스테이지의 마일스톤은 코어가 아니라 조각을 준다.
        public static bool IsHardStage(int stageIndex) => stageIndex >= CoresByStage.Length && stageIndex < CoresByStage.Length + ShardsByHardStage.Length;

        // 이 스테이지(0부터)에 마일스톤이 있는지. 노멀 1~5(코어)와 하드 1~5(조각)에 있다.
        // 표에 줄이 없는 번호는 없다고 본다. 마지막 줄 값을 물려받아 보상이 새어 나가는 일을 막으려고, 마일스톤을 기록·수령하는 곳이 모두 이 값을 먼저 본다.
        public static bool HasMilestones(int stageIndex) => stageIndex >= 0 && stageIndex < CoresByStage.Length + ShardsByHardStage.Length;

        // 스테이지(0부터)의 마일스톤(번호 index) 코어 보상. 예) 스테이지 2의 6분 = 300. 하드 스테이지는 코어 대신 조각을 주므로 0이다.
        public static int GetCore(int stageIndex, int index)
        {
            if (IsHardStage(stageIndex)) return 0;
            int stage = System.Math.Max(0, System.Math.Min(stageIndex, CoresByStage.Length - 1));
            return CoresByStage[stage][index];
        }

        // 하드 스테이지(0부터, 전체 목록 위치)의 마일스톤(번호 index) 만능 조각 보상. 노멀 스테이지는 0이다.
        public static int GetShards(int stageIndex, int index)
        {
            return IsHardStage(stageIndex) ? ShardsByHardStage[stageIndex - CoresByStage.Length][index] : 0;
        }

        // 하드 스테이지 하나의 조각 보상 합계 (스테이지 카드에 "마일스톤 조각 최대 +N"으로 보여 준다). 노멀 스테이지는 0.
        public static int GetTotalShards(int stageIndex)
        {
            int total = 0;
            for (int i = 0; i < Count; i++) total += GetShards(stageIndex, i);
            return total;
        }

        // 화면에 보여 줄 스테이지 이름. 노멀은 "스테이지 3", 하드는 "하드 3" (하드 n은 노멀 n의 어려운 버전).
        public static string StageLabel(int stageIndex)
        {
            return IsHardStage(stageIndex) ? $"하드 {stageIndex - CoresByStage.Length + 1}" : $"스테이지 {stageIndex + 1}";
        }

        // 로비 마일스톤 창에 보여줄 이름과 달성 조건 문구. (마일스톤 번호 순서)
        public static readonly string[] Titles = { "3분", "6분", "클리어" };
        public static readonly string[] Conditions = { "3분 이상 생존에서 획득", "6분 이상 생존에서 획득", "처음 클리어에서 획득" };

        // 처음 클리어 마일스톤의 번호 (스테이지 안에서의 위치).
        public const int ClearIndex = 2;

        // 마일스톤 개수 (스테이지 하나당).
        public static int Count => Titles.Length;

        // 마일스톤이 있는 스테이지의 올바른 번호인지. 마일스톤이 없는 번호는 항상 false라서 달성·수령 기록이 만들어지지 않는다.
        private static bool IsValid(int stageIndex, int index) => HasMilestones(stageIndex) && index >= 0 && index < Count;

        private static int GetMask(List<int> masks, int stageIndex) => stageIndex < masks.Count ? masks[stageIndex] : 0;

        private static void AddBit(List<int> masks, int stageIndex, int index)
        {
            while (masks.Count <= stageIndex) masks.Add(0);
            masks[stageIndex] |= 1 << index;
        }

        // 이 스테이지에서 이미 보상을 받은 마일스톤 비트. (스테이지 1은 옛 저장의 계정 전체 기록도 함께 읽는다)
        private static int ClaimedMask(SaveData data, int stageIndex)
        {
            return GetMask(data.stageMilestoneClaimedMasks, stageIndex) | (stageIndex == 0 ? data.milestoneClaimedMask : 0);
        }

        // 이 스테이지에서 달성한 마일스톤 비트. 보상을 이미 받은 것도 달성한 것으로 본다.
        private static int DoneMask(SaveData data, int stageIndex)
        {
            return GetMask(data.stageMilestoneDoneMasks, stageIndex) | (stageIndex == 0 ? data.milestoneDoneMask : 0) | ClaimedMask(data, stageIndex);
        }

        // 이미 보상을 받았는지. stageIndex는 0부터(스테이지 1 = 0).
        public static bool IsClaimed(SaveData data, int stageIndex, int index)
        {
            return IsValid(stageIndex, index) && (ClaimedMask(data, stageIndex) & (1 << index)) != 0;
        }

        // 달성했는지.
        public static bool IsDone(SaveData data, int stageIndex, int index)
        {
            return IsValid(stageIndex, index) && (DoneMask(data, stageIndex) & (1 << index)) != 0;
        }

        // 이 스테이지의 마일스톤 세 칸을 모두 받았는지.
        public static bool AllClaimed(SaveData data, int stageIndex)
        {
            if (!HasMilestones(stageIndex)) return false;
            for (int i = 0; i < Count; i++)
                if (!IsClaimed(data, stageIndex, i)) return false;
            return true;
        }

        // 마일스톤을 달성 처리한다. 새로 달성한 것이면 true, 이미 달성했거나 번호가 잘못됐으면 false.
        public static bool MarkDone(SaveData data, int stageIndex, int index)
        {
            if (!IsValid(stageIndex, index) || IsDone(data, stageIndex, index)) return false;
            AddBit(data.stageMilestoneDoneMasks, stageIndex, index);
            return true;
        }

        // 보상을 받았다고 기록한다. (지급은 CurrencyManager.TryClaimMilestone이 한다)
        public static void MarkClaimed(SaveData data, int stageIndex, int index)
        {
            if (!IsValid(stageIndex, index)) return;
            AddBit(data.stageMilestoneClaimedMasks, stageIndex, index);
        }
    }
}
