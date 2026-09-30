namespace SurvivalDrone.Meta
{
    // 뽑기 결과 한 장이 보유 목록에 어떻게 반영됐는지.
    //  New       = 처음 얻는 드론 (결과 화면에 NEW 표시)
    //  Promoted  = 이미 가진 드론인데 더 높은 등급이 나와서 등급이 올라감 (승급)
    //  Duplicate = 이미 가진 드론과 같거나 낮은 등급이라 조각으로 환산됨
    public enum PullOutcome { New, Promoted, Duplicate }

    // 뽑기 결과가 보유 목록에 반영된 뒤의 결과. 뽑기 결과 화면(UI)이 카드에 NEW / 승급 / +조각 을 표시할 때 쓴다.
    public readonly struct InventoryPullOutcome
    {
        public readonly PullOutcome outcome;

        // 승급이었다면 올라가기 전의 등급 (승급이 아니면 뽑힌 등급과 같은 값).
        public readonly GachaRarity previousRarity;

        // 이번에 받은 조각 수 (중복일 때만 0보다 크다).
        public readonly int shardsGained;

        public InventoryPullOutcome(PullOutcome outcome, GachaRarity previousRarity, int shardsGained)
        {
            this.outcome = outcome;
            this.previousRarity = previousRarity;
            this.shardsGained = shardsGained;
        }
    }
}
