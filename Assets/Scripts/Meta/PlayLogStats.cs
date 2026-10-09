using System;

namespace SurvivalDrone.Meta
{
    // 테스트(CBT) 기록의 "누적 숫자" 모음. 기록 목록(문장)과 따로 숫자를 쌓아두면,
    // 기록이 오래돼서 잘려 나가도(최대 개수 제한) 요약 숫자는 틀어지지 않는다.
    // 저장 파일(SaveData)에 함께 저장된다.
    [Serializable]
    public class PlayLogStats
    {
        // 게임을 켠 횟수
        public int sessions;

        // 판(매치) 관련: 총 판 수, 클리어한 판 수, 생존 시간 합계(초), 받은 보상 합계
        public int matches;
        public int clears;
        public float totalSurviveSeconds;
        public int rewardCoreTotal;
        public int rewardCreditTotal;

        // 뽑기 관련: 뽑은 횟수 합계, 1회 뽑기 누른 횟수, 10연 누른 횟수, 나온 SSR 개수, 뽑기에 쓴 코어, 코어 부족으로 막힌 횟수
        public int pullsTotal;
        public int singlePullActions;
        public int tenPullActions;
        public int ssrTotal;
        public int coreSpentOnPulls;
        public int blockedPulls;

        // 첫 뽑기를 하기까지 플레이한 판 수. 아직 뽑기를 한 번도 안 했으면 -1. ("첫 뽑기 전 이탈" 지표에 쓴다)
        public int matchesBeforeFirstPull = -1;

        // 소천장(SR 이상 보장)이 발동한 횟수. 옛 저장 파일에는 없지만 0으로 안전하게 읽힌다.
        public int softPityTriggers;

        // 드론 강화 성공 횟수
        public int upgrades;

        // 조각 교환(크레딧 → 조각) 횟수와, 그때 산 조각 합계·쓴 크레딧 합계. 옛 저장 파일에는 없지만 0으로 안전하게 읽힌다.
        public int shardExchanges;
        public int shardsBought;
        public int creditSpentOnShards;

        // 락온 뽑기: 확정한 횟수, 재뽑기 횟수, 확정한 판들에서 쓴 코어 합계, 코어 부족으로 막힌 횟수.
        // 옛 저장 파일에는 없지만 0으로 안전하게 읽힌다.
        public int lockOnConfirms;
        public int lockOnRerolls;
        public int lockOnCoreSpent;
        public int lockOnBlocked;

        // 판 도중에 일시정지 메뉴에서 "재시작"/"메인메뉴"로 나간 횟수와, 나간 시점(게임 시간)의 합계(초).
        // 사망·클리어로 끝난 판(matches)과는 따로 센다. 그래야 클리어율·평균 생존 같은 "끝난 판" 통계가 섞이지 않는다.
        // 옛 저장 파일에는 이 값이 없지만, 숫자 기본값이 0이라 그대로 안전하게 읽힌다.
        public int abandons;
        public float abandonSurviveSeconds;

        // 로비에서 마일스톤·일일 퀘스트 코어를 "받은" 횟수와 받은 코어 합계.
        // 판 보상과 달리 이 코어는 어디서 들어왔는지 기록이 없어서 경제를 실제 데이터로 맞추기 어려웠다.
        // 옛 저장 파일에는 없지만 숫자 기본값이 0이라 그대로 안전하게 읽힌다.
        public int milestoneClaims;
        public int milestoneCoreTotal;
        public int questClaims;
        public int questCoreTotal;

        // ---- 판 시간 통계 (이 기능이 생긴 뒤에 기록된 판만 집계한다) ----
        // 예전에 기록된 판에는 "실제 소요 시간"이 없어서, 옛 판까지 섞으면 평균이 틀어지기 때문에 새 판만 따로 센다.
        // 시간 기록이 있는 판 수, 그 판들의 실제 소요 시간 합계(일시정지·레벨업 선택 화면 포함, 초)
        public int timedMatches;
        public float timedRealSeconds;

        // 시간 기록이 있는 판 중 클리어한 판 수와 그 실제 소요 시간 합계
        public int timedClears;
        public float timedClearRealSeconds;

        // 시간 기록이 있는 판 중 실패한 판 수와 그 생존 시간(게임 시간) 합계
        public int timedFails;
        public float timedFailSurviveSeconds;
    }
}
