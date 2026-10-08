using System;
using System.Text;
using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 테스트(CBT) 플레이 기록을 쌓고, 내보낼 글(요약 + 기록)로 만들어주는 도구.
    //
    // 왜 필요한가:
    //  웹(WebGL) 게임에서는 테스터 브라우저 안에만 파일이 생겨서 개발자가 그 기록을 받을 수 없다.
    //  그래서 게임이 스스로 기록을 쌓아두고, 테스터가 로비의 "테스트 로그" 창에서 복사하거나 파일로 저장해 보내게 한다.
    //  이 기록이 시뮬레이터(엑셀) 가정값을 실제 플레이 데이터로 보정하는 근거가 된다.
    //   - 판당 코어 수입, 스테이지 클리어율, 뽑기 횟수·코어 수지, 이탈 지점(마지막으로 본 화면, 첫 뽑기 전에 몇 판 했는지)
    //
    // 기록은 SaveData 안에 들어 있어서 새로고침·재접속 후에도 이어지고, 누적 숫자(PlayLogStats)는 따로 쌓아서
    // 오래된 기록이 잘려 나가도 요약이 틀어지지 않는다.
    //
    // 모든 함수가 기록할 SaveData를 인자로 받는다. 각 매니저(GachaController 등)가 이미 들고 있는 저장 데이터를 그대로 넘기면 되고,
    // 검증 도구가 임시 데이터로 검사할 때 진짜 저장 파일을 건드리지 않는다.
    // 기록만 하고 파일 저장은 호출한 쪽이 한다 (호출한 쪽이 어차피 자기 변경을 저장하기 때문).
    public static class PlayLog
    {
        // 기록 문장을 최대 몇 개까지 보관할지. 넘으면 가장 오래된 것부터 지운다.
        public const int MaxEntries = 500;

        // 기록 시각을 정하는 함수. 검증 도구가 시각을 고정해서 검사하려고 바꿀 수 있게 열어 두었다.
        public static Func<DateTime> Clock = () => DateTime.Now;

        // ---------------- 기록하기 ----------------

        // 게임을 켰을 때 한 번. 테스터 번호가 없으면 새로 만든다.
        public static void StartSession(SaveData data, string version, string platform, string resolution)
        {
            if (string.IsNullOrEmpty(data.testerId))
                data.testerId = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();

            data.logStats.sessions++;
            Add(data, "session_start", $"tester={data.testerId} 빌드={version} 플랫폼={platform} 화면={resolution} 접속횟수={data.logStats.sessions}");
        }

        // 화면(씬)이 바뀔 때. "마지막으로 본 화면"이 이탈 지점을 알려준다.
        public static void RecordScreen(SaveData data, string sceneName)
        {
            Add(data, "screen", sceneName);
        }

        // 뽑기 1회 또는 10연을 했을 때.
        public static void RecordPull(SaveData data, bool isTen, int spentCore, int[] rarityCounts, int newCount, int promotedCount, int shardsGained, int coreAfter, int pityAfter, int pityLimit,
                                      int softPityAfter = 0, int softPityLimit = 0, int softPityHits = 0)
        {
            var s = data.logStats;
            int count = 0;
            foreach (int c in rarityCounts) count += c;

            if (s.matchesBeforeFirstPull < 0) s.matchesBeforeFirstPull = s.matches;
            s.pullsTotal += count;
            if (isTen) s.tenPullActions++; else s.singlePullActions++;
            s.ssrTotal += rarityCounts[(int)GachaRarity.SSR];
            s.coreSpentOnPulls += spentCore;
            s.softPityTriggers += softPityHits;

            // 소천장(SR 이상 보장)을 쓰는 설정일 때만 기록 끝에 소천장 정보를 덧붙인다. (쓰지 않으면 옛 기록 모양 그대로)
            string soft = softPityLimit > 0 ? $" 소천장={softPityAfter}/{softPityLimit} 소천장발동={softPityHits}" : "";
            Add(data, "pull",
                $"종류={(isTen ? "10연" : "1회")} 사용코어={spentCore} N={rarityCounts[0]} R={rarityCounts[1]} SR={rarityCounts[2]} SSR={rarityCounts[3]}" +
                $" 신규={newCount} 승급={promotedCount} 조각={shardsGained} 남은코어={coreAfter} 천장={pityAfter}/{pityLimit}{soft}");
        }

        // 코어가 모자라서 뽑기를 못 했을 때.
        public static void RecordPullBlocked(SaveData data, bool isTen, int needCore, int haveCore)
        {
            data.logStats.blockedPulls++;
            Add(data, "pull_blocked", $"종류={(isTen ? "10연" : "1회")} 필요코어={needCore} 보유코어={haveCore}");
        }

        // 한 판이 끝났을 때.
        //  surviveSeconds = 게임 시간(시간이 멈춘 동안은 세지 않음), realSeconds = 실제로 걸린 시간(일시정지·레벨업 선택 화면 포함)
        //  pickSummary = 이번 판에서 레벨업 선택지를 뭘 골랐는지 요약한 문장. 비어 있으면(옛 방식 호출) 기록에 덧붙이지 않는다.
        //  damageSummary = 이번 판에서 어떤 적에게 얼마나 맞았는지(패배한 판은 사망원인 포함) 요약한 문장. 비어 있으면 덧붙이지 않는다.
        public static void RecordMatch(SaveData data, int stageNumber, bool cleared, float surviveSeconds, float realSeconds, int playerLevel, int droneCount, int combatPower, int rewardCore, int rewardCredit, string pickSummary = null, string damageSummary = null)
        {
            var s = data.logStats;
            s.matches++;
            if (cleared) s.clears++;
            s.totalSurviveSeconds += surviveSeconds;
            s.rewardCoreTotal += rewardCore;
            s.rewardCreditTotal += rewardCredit;

            s.timedMatches++;
            s.timedRealSeconds += realSeconds;
            if (cleared)
            {
                s.timedClears++;
                s.timedClearRealSeconds += realSeconds;
            }
            else
            {
                s.timedFails++;
                s.timedFailSurviveSeconds += surviveSeconds;
            }

            Add(data, "match",
                $"스테이지={stageNumber} 결과={(cleared ? "클리어" : "실패")} 생존={surviveSeconds:F1}초 실제소요={realSeconds:F1}초 도달레벨={playerLevel} 드론수={droneCount}" +
                $" 내전투력={combatPower} 보상코어={rewardCore} 보상크레딧={rewardCredit}{PickSuffix(pickSummary)}{PickSuffix(damageSummary)}");
        }

        // 레벨업 선택 요약이 있으면 앞에 공백을 붙여 기록 끝에 덧붙일 글자로, 없으면 빈 글자로 만든다.
        private static string PickSuffix(string pickSummary)
        {
            return string.IsNullOrEmpty(pickSummary) ? "" : " " + pickSummary;
        }

        // 판이 끝나기 전에 일시정지 메뉴에서 나갔을 때. (사망·클리어로 끝난 판은 위의 RecordMatch가 남긴다)
        //  exitMethod = "재시작" 또는 "메인메뉴". 어느 시점에서 많이 나가는지가 "지루하거나 어려워서 나간 구간"을 알려준다.
        //  판 수·클리어율 같은 "끝난 판" 통계에는 넣지 않고, 이탈 횟수와 이탈 시점만 따로 쌓는다.
        //  pickSummary = 그 시점까지 레벨업 선택지를 뭘 골랐는지 요약한 문장 (비어 있으면 덧붙이지 않음)
        public static void RecordAbandon(SaveData data, int stageNumber, float surviveSeconds, float realSeconds, int playerLevel, int droneCount, int combatPower, string exitMethod, string pickSummary = null)
        {
            var s = data.logStats;
            s.abandons++;
            s.abandonSurviveSeconds += surviveSeconds;

            Add(data, "match_abandon",
                $"스테이지={stageNumber} 나간방법={exitMethod} 생존={surviveSeconds:F1}초 실제소요={realSeconds:F1}초 도달레벨={playerLevel} 드론수={droneCount} 내전투력={combatPower}{PickSuffix(pickSummary)}");
        }

        // 드론 강화에 성공했을 때.
        public static void RecordUpgrade(SaveData data, string droneName, int newLevel, int combatPower)
        {
            data.logStats.upgrades++;
            Add(data, "upgrade", $"드론={droneName} 레벨={newLevel} 내전투력={combatPower}");
        }

        // 크레딧으로 조각을 샀을 때. countToday = 오늘 몇 번째 교환인지, limit = 하루 한도.
        public static void RecordShardExchange(SaveData data, string droneName, int shards, int credit, int countToday, int limit)
        {
            var s = data.logStats;
            s.shardExchanges++;
            s.shardsBought += shards;
            s.creditSpentOnShards += credit;
            Add(data, "shard_exchange", $"드론={droneName} 조각=+{shards} 사용크레딧={credit} 오늘={countToday}/{limit}");
        }

        // 드론을 장착하거나 해제했을 때. droneName이 null이면 해제.
        public static void RecordEquip(SaveData data, int slotNumber, string droneName, int combatPower)
        {
            Add(data, "equip", $"슬롯={slotNumber} 드론={(droneName ?? "비움")} 내전투력={combatPower}");
        }

        // 다음 스테이지가 열렸을 때.
        public static void RecordUnlock(SaveData data, int stageNumber)
        {
            Add(data, "unlock", $"스테이지={stageNumber}");
        }

        // ---- 락온 뽑기 (시작 → 재뽑기 → 확정, 코어 부족으로 막힘) ----

        // 락온 뽑기를 시작(첫 공개)했을 때. slots = 공개된 칸 요약 (예: "SR:Melee/N:Sniper/R:Heal").
        public static void RecordLockOnStart(SaveData data, int spentCore, int coreAfter, string slots)
        {
            Add(data, "lockon_start", $"사용코어={spentCore} 칸={slots} 남은코어={coreAfter}");
        }

        // 재뽑기를 했을 때. rerollNumber = 이번 판에서 몇 번째 재뽑기인지, lockedCount = 재뽑기 직전에 잠근 칸 수.
        public static void RecordLockOnReroll(SaveData data, int rerollNumber, int spentCore, int lockedCount, string slots, int coreAfter)
        {
            data.logStats.lockOnRerolls++;
            Add(data, "lockon_reroll", $"번째={rerollNumber} 사용코어={spentCore} 잠근칸={lockedCount} 칸={slots} 남은코어={coreAfter}");
        }

        // 확정했을 때. reason = "확정" 또는 화면을 나가며 자동 확정한 "나가기". totalSpentCore = 이번 판에서 쓴 코어 합계.
        // dupShards = 잠근 칸이 중복이라 받은 조각, convShards = 안 가져간 칸을 환산해 받은 조각.
        public static void RecordLockOnConfirm(SaveData data, string reason, int lockedCount, int totalSpentCore, int rerolls, int newCount, int promotedCount,
                                               int dupShards, int convShards, int coreAfter, string slots)
        {
            var s = data.logStats;
            s.lockOnConfirms++;
            s.lockOnCoreSpent += totalSpentCore;
            if (s.matchesBeforeFirstPull < 0) s.matchesBeforeFirstPull = s.matches;
            Add(data, "lockon_confirm",
                $"방법={reason} 잠근칸={lockedCount} 총사용코어={totalSpentCore} 재뽑기={rerolls} 신규={newCount} 승급={promotedCount} 중복조각={dupShards} 환산조각={convShards} 남은코어={coreAfter} 결과={slots}");
        }

        // 코어가 모자라 시작이나 재뽑기를 못 했을 때. step = "시작" 또는 "재뽑기".
        public static void RecordLockOnBlocked(SaveData data, string step, int needCore, int haveCore)
        {
            data.logStats.lockOnBlocked++;
            Add(data, "lockon_blocked", $"단계={step} 필요코어={needCore} 보유코어={haveCore}");
        }

        // ---------------- 내보내기 ----------------

        // 테스터가 복사하거나 파일로 저장할 전체 글 = 머리말 + 요약 + 기록 전부.
        public static string BuildExportText(SaveData data, string version, string platform)
        {
            var builder = new StringBuilder();
            builder.AppendLine("=== 드론 지휘관 테스트 기록 ===");
            builder.AppendLine($"테스터: {data.testerId}   빌드: {version}   플랫폼: {platform}   내보낸 시각: {Clock():yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine();
            builder.AppendLine("[요약]");
            builder.Append(BuildSummaryText(data));
            builder.AppendLine();
            builder.AppendLine($"[기록] (최근 {data.playLog.Count}개, 오래된 것부터)");
            foreach (var line in data.playLog) builder.AppendLine(line);
            return builder.ToString();
        }

        // 요약만 (로그 창에 미리보기로 보여줄 때도 쓴다).
        public static string BuildSummaryText(SaveData data)
        {
            var s = data.logStats;
            var builder = new StringBuilder();

            if (s.matches > 0)
            {
                float clearRate = (float)s.clears / s.matches * 100f;
                builder.AppendLine($"- 판 수 {s.matches} (클리어 {s.clears}, 클리어율 {clearRate:F1}%)");
                builder.AppendLine($"- 판당 평균 보상: 코어 {(float)s.rewardCoreTotal / s.matches:F1} / 크레딧 {(float)s.rewardCreditTotal / s.matches:F1}");

                // 판 길이 판단에 쓰는 시간 통계. 클리어한 판(생존이 항상 판 길이)이 섞이면 평균 생존이 의미가 없어서 따로 보여준다.
                if (s.timedFails > 0)
                    builder.AppendLine($"- 실패한 판 평균 생존: {FormatSeconds(s.timedFailSurviveSeconds / s.timedFails)} ({s.timedFails}판, 게임 시간 기준)");
                else if (s.timedMatches > 0)
                    builder.AppendLine("- 실패한 판 평균 생존: 실패한 판 없음");

                if (s.timedMatches > 0)
                {
                    builder.AppendLine($"- 판당 평균 실제 소요 시간: {FormatSeconds(s.timedRealSeconds / s.timedMatches)} ({s.timedMatches}판, 일시정지·레벨업 선택 화면 포함)");
                    if (s.timedClears > 0)
                        builder.AppendLine($"- 클리어한 판 평균 실제 소요 시간: {FormatSeconds(s.timedClearRealSeconds / s.timedClears)} ({s.timedClears}판)");
                }

                if (s.timedMatches < s.matches)
                    builder.AppendLine($"  ※ 시간 통계는 실제 소요 시간 기록이 생긴 뒤의 {s.timedMatches}판만 집계 (전체 {s.matches}판)");
            }
            else
            {
                builder.AppendLine("- 판 수 0 (아직 플레이한 판이 없음)");
            }

            // 판 도중에 나간 기록. 위의 "판 수"는 사망·클리어로 끝난 판만 센 것이라, 나간 판은 여기에 따로 보여준다.
            if (s.abandons > 0)
                builder.AppendLine($"- 판 도중에 나감 {s.abandons}번 (평균 {FormatSeconds(s.abandonSurviveSeconds / s.abandons)} 시점, 위 판 수에는 포함 안 됨)");

            builder.AppendLine($"- 뽑기 총 {s.pullsTotal}회 (1회 {s.singlePullActions}번, 10연 {s.tenPullActions}번) · SSR {s.ssrTotal}개 · 뽑기에 쓴 코어 {s.coreSpentOnPulls:N0} · 코어 부족으로 막힘 {s.blockedPulls}번");
            if (s.softPityTriggers > 0)
                builder.AppendLine($"- 소천장(SR 이상 보장) 발동 {s.softPityTriggers}번");
            builder.AppendLine(s.matchesBeforeFirstPull >= 0
                ? $"- 첫 뽑기 전에 플레이한 판 수: {s.matchesBeforeFirstPull}"
                : "- 아직 뽑기를 한 번도 안 함");
            builder.AppendLine($"- 드론 강화 {s.upgrades}번 · 접속 {s.sessions}번");
            if (s.shardExchanges > 0)
                builder.AppendLine($"- 조각 교환 {s.shardExchanges}번 (조각 {s.shardsBought:N0}개, 크레딧 {s.creditSpentOnShards:N0} 사용)");
            // 락온 뽑기는 쓴 적이 있을 때만 한 줄 덧붙인다. (쓰지 않으면 옛 요약 모양 그대로)
            if (s.lockOnConfirms > 0 || s.lockOnBlocked > 0)
                builder.AppendLine($"- 락온 뽑기 {s.lockOnConfirms}판 확정 (재뽑기 {s.lockOnRerolls}번, 쓴 코어 {s.lockOnCoreSpent:N0}) · 코어 부족으로 막힘 {s.lockOnBlocked}번");
            return builder.ToString();
        }

        // 화면 미리보기용: 요약 + 가장 최근 기록 몇 줄.
        public static string BuildPreviewText(SaveData data, int recentCount = 4)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"테스터 번호: {data.testerId}");
            builder.AppendLine();
            builder.Append(BuildSummaryText(data));
            builder.AppendLine();
            builder.AppendLine("최근 기록");
            int start = Mathf.Max(0, data.playLog.Count - recentCount);
            for (int i = start; i < data.playLog.Count; i++) builder.AppendLine(data.playLog[i]);
            return builder.ToString();
        }

        // ---------------- 도우미 ----------------
        private static void Add(SaveData data, string type, string detail)
        {
            data.playLog.Add($"{Clock():MM-dd HH:mm:ss} | {type} | {detail}");
            while (data.playLog.Count > MaxEntries) data.playLog.RemoveAt(0);
        }

        private static string FormatSeconds(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
