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

        // 드론 강화 성공 횟수
        public int upgrades;

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
