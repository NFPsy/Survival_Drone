namespace SurvivalDrone.Meta
{
    // 뽑기 요청이 성공했는지, 실패했다면 왜인지.
    //  InsufficientCore = 코어가 모자람 (아무것도 차감·뽑기하지 않음)
    //  NotReady         = 필요한 매니저(재화·보유 드론)가 준비되지 않음
    public enum GachaPullFailure { None, InsufficientCore, NotReady }

    // 뽑기 한 번(1회 또는 10연)의 전체 결과. 뽑기 결과 화면(UI)이 이것 하나로 카드들을 그린다.
    //  pulls[i]    = i번째로 나온 등급·드론 종류
    //  outcomes[i] = 그 결과가 보유 목록에 어떻게 반영됐는지 (NEW / 승급 / 중복 조각)
    public readonly struct GachaPullReport
    {
        public readonly bool success;
        public readonly GachaPullFailure failure;

        // 이번에 쓴 코어 (실패하면 0).
        public readonly int spentCore;

        public readonly GachaPullResult[] pulls;
        public readonly InventoryPullOutcome[] outcomes;

        // 뽑기를 모두 처리한 직후의 천장 카운트 (천장 게이지 UI용).
        public readonly int pityAfter;

        public GachaPullReport(int spentCore, GachaPullResult[] pulls, InventoryPullOutcome[] outcomes, int pityAfter)
        {
            success = true;
            failure = GachaPullFailure.None;
            this.spentCore = spentCore;
            this.pulls = pulls;
            this.outcomes = outcomes;
            this.pityAfter = pityAfter;
        }

        private GachaPullReport(GachaPullFailure failure, int pityAfter)
        {
            success = false;
            this.failure = failure;
            spentCore = 0;
            pulls = new GachaPullResult[0];
            outcomes = new InventoryPullOutcome[0];
            this.pityAfter = pityAfter;
        }

        public static GachaPullReport Failed(GachaPullFailure failure, int pityCount) => new GachaPullReport(failure, pityCount);
    }
}
