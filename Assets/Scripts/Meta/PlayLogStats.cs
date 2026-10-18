using System;
using System.Collections.Generic;

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

        // 가상 상점(OBT 2단계): 상점 화면을 연 횟수, 상품의 구매 버튼을 누른 횟수, 데모 안내 창을 확인한 횟수.
        // 그리고 상품별 클릭 횟수 (상품 이름 목록과 같은 순서의 횟수 목록 — JsonUtility가 사전을 저장하지 못해서 두 줄로 나눠 둔다).
        // 옛 저장 파일에는 없지만 0·빈 목록으로 안전하게 읽힌다.
        public int shopOpens;

        // 위 열람 중에서 "코어 충전" 버튼(뽑기가 코어 부족으로 막혔을 때 나오는 버튼)으로 열린 횟수.
        // 코어가 모자라서 상점에 관심이 생겼는지를 보는 지표다.
        public int shopTopUpOpens;
        public int shopClicks;
        public int shopPopupCloses;
        public List<string> shopClickNames = new List<string>();
        public List<int> shopClickCounts = new List<int>();

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

        // ---- 시간 구성 통계 (10/18 이후 판만) ----
        // "실제 소요 시간"에는 일시정지 메뉴와 레벨업 선택 화면에 머문 시간이 섞여서, 메뉴를 열어 둔 채 방치한 판이 평균을 크게 끌어올렸다.
        // 그래서 일시정지한 시간과 레벨업 선택 화면에 있던 시간을 따로 모은다. 옛 저장 파일에는 없지만 0으로 안전하게 읽힌다.
        // 구성 기록이 있는 판 수, 그 판들의 실제 소요 시간 합계, 일시정지한 시간 합계, 레벨업 선택 화면에 있던 시간 합계(모두 초)
        public int breakdownMatches;
        public float breakdownRealSeconds;
        public float pausedSecondsTotal;
        public float selectSecondsTotal;
    }
}
