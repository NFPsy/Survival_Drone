using System;
using System.Collections.Generic;

namespace SurvivalDrone.Meta
{
    // 저장 파일(JSON)에 들어가는 내용을 담는 "저장용 상자".
    // [Serializable]을 붙이면 유니티의 JsonUtility가 이 클래스를 글자(JSON)로 바꾸거나 다시 되돌릴 수 있다.
    //
    // 지금은 재화(코어·크레딧), 천장 카운트, 보유 드론·장착, 스테이지 진행(해금, 최고 기록)이 들어 있다.
    // 새 시스템을 만들 때마다 여기에 필드를 하나씩 추가한다. 예전 저장 파일에 없는 필드는 0/기본값으로 읽히므로 그대로 이어서 쓸 수 있다.
    [Serializable]
    public class SaveData
    {
        // 저장 형식의 버전. 나중에 저장 구조가 크게 바뀌었을 때 옛 파일을 구분하기 위한 번호.
        public int saveVersion = 1;

        // 재화의 "처음 지급"을 이미 받았는지. false인 새 데이터일 때만 CurrencyTable의 시작 재화를 넣어준다.
        public bool isCurrencyInitialized;

        // 코어(하드 재화)의 현재 보유량.
        public int core;

        // 크레딧(소프트 재화)의 현재 보유량.
        public int credit;

        // 달성한 마일스톤을 비트로 기록한다 (MatchMilestones의 번호 n이면 n번째 비트). 달성만 하고 아직 코어를 안 받은 것도 포함.
        // 로비의 마일스톤 창에서 "획득"을 눌러야 코어를 받는다.
        public int milestoneDoneMask;

        // 이미 받은 마일스톤 보상을 비트로 기록한다 (MatchMilestones의 번호 n이면 n번째 비트).
        // 예: 3분(0번)과 6분(1번)을 받았으면 3. 계정 전체에서 한 번씩만 받기 위한 표시다.
        public int milestoneClaimedMask;

        // ---- 일일 퀘스트 ----
        // 퀘스트 기록이 어느 날짜의 것인지 ("2026-10-01"). 오늘과 다르면 아래 기록을 초기화한다.
        public string dailyQuestDate = "";

        // 오늘 달성한 퀘스트(비트, DailyQuests의 번호 n이면 n번째 비트)와 이미 코어를 받은 퀘스트.
        public int dailyQuestDoneMask;
        public int dailyQuestClaimedMask;

        // ---- 테스트(CBT) 기록 ----
        // 테스터를 구분하는 임의의 짧은 번호 (이름이나 개인정보가 아니다). 처음 켤 때 한 번 만들어진다.
        public string testerId = "";

        // 기록 문장 목록. 최대 개수를 넘으면 오래된 것부터 지운다.
        public List<string> playLog = new List<string>();

        // 기록의 누적 숫자 (요약에 쓴다).
        public PlayLogStats logStats = new PlayLogStats();

        // 천장 카운트: 마지막 SSR 이후 누적 뽑기 횟수. SSR이 나오면 0으로 돌아가고, 게임을 껐다 켜도 이어진다.
        public int gachaPityCount;

        // 시작 드론을 이미 받았는지. false인 새 데이터일 때만 시작 드론(근접 N + 저격 N)을 지급하고 장착시킨다.
        public bool isInventoryInitialized;

        // 내가 가진 드론 목록 (종류당 최대 1개).
        public List<OwnedDroneData> ownedDrones = new List<OwnedDroneData>();

        // 출격 시 장착한 드론. 칸 번호가 장착 슬롯 번호이고, 값은 드론 종류 번호(DroneType을 숫자로 바꾼 것)다.
        // 비어 있는 슬롯은 -1로 저장한다.
        public List<int> equippedDrones = new List<int>();

        // 해금된 스테이지 개수. 1이면 스테이지 1만 열려 있다. (이전 스테이지를 클리어하면 하나씩 늘어난다)
        public int unlockedStageCount = 1;

        // 스테이지별 최고 생존 시간(초). 0번 칸이 스테이지 1이다. 아직 플레이 안 한 스테이지는 칸이 없거나 0이다.
        public List<float> bestSurvivalSeconds = new List<float>();
    }
}
