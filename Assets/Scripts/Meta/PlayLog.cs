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
        public static void RecordPull(SaveData data, bool isTen, int spentCore, int[] rarityCounts, int newCount, int promotedCount, int shardsGained, int coreAfter, int pityAfter, int pityLimit)
        {
            var s = data.logStats;
            int count = 0;
            foreach (int c in rarityCounts) count += c;

            if (s.matchesBeforeFirstPull < 0) s.matchesBeforeFirstPull = s.matches;
            s.pullsTotal += count;
            if (isTen) s.tenPullActions++; else s.singlePullActions++;
            s.ssrTotal += rarityCounts[(int)GachaRarity.SSR];
            s.coreSpentOnPulls += spentCore;

            Add(data, "pull",
                $"종류={(isTen ? "10연" : "1회")} 사용코어={spentCore} N={rarityCounts[0]} R={rarityCounts[1]} SR={rarityCounts[2]} SSR={rarityCounts[3]}" +
                $" 신규={newCount} 승급={promotedCount} 조각={shardsGained} 남은코어={coreAfter} 천장={pityAfter}/{pityLimit}");
        }

        // 코어가 모자라서 뽑기를 못 했을 때.
        public static void RecordPullBlocked(SaveData data, bool isTen, int needCore, int haveCore)
        {
            data.logStats.blockedPulls++;
            Add(data, "pull_blocked", $"종류={(isTen ? "10연" : "1회")} 필요코어={needCore} 보유코어={haveCore}");
        }

        // 한 판이 끝났을 때.
        public static void RecordMatch(SaveData data, int stageNumber, bool cleared, float surviveSeconds, int playerLevel, int droneCount, int combatPower, int rewardCore, int rewardCredit)
        {
            var s = data.logStats;
            s.matches++;
            if (cleared) s.clears++;
            s.totalSurviveSeconds += surviveSeconds;
            s.rewardCoreTotal += rewardCore;
            s.rewardCreditTotal += rewardCredit;

            Add(data, "match",
                $"스테이지={stageNumber} 결과={(cleared ? "클리어" : "실패")} 생존={surviveSeconds:F1}초 도달레벨={playerLevel} 드론수={droneCount}" +
                $" 내전투력={combatPower} 보상코어={rewardCore} 보상크레딧={rewardCredit}");
        }

        // 드론 강화에 성공했을 때.
        public static void RecordUpgrade(SaveData data, string droneName, int newLevel, int combatPower)
        {
            data.logStats.upgrades++;
            Add(data, "upgrade", $"드론={droneName} 레벨={newLevel} 내전투력={combatPower}");
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
                float avgSurvive = s.totalSurviveSeconds / s.matches;
                builder.AppendLine($"- 판 수 {s.matches} (클리어 {s.clears}, 클리어율 {clearRate:F1}%) · 평균 생존 {FormatSeconds(avgSurvive)}");
                builder.AppendLine($"- 판당 평균 보상: 코어 {(float)s.rewardCoreTotal / s.matches:F1} / 크레딧 {(float)s.rewardCreditTotal / s.matches:F1}");
            }
            else
            {
                builder.AppendLine("- 판 수 0 (아직 플레이한 판이 없음)");
            }

            builder.AppendLine($"- 뽑기 총 {s.pullsTotal}회 (1회 {s.singlePullActions}번, 10연 {s.tenPullActions}번) · SSR {s.ssrTotal}개 · 뽑기에 쓴 코어 {s.coreSpentOnPulls:N0} · 코어 부족으로 막힘 {s.blockedPulls}번");
            builder.AppendLine(s.matchesBeforeFirstPull >= 0
                ? $"- 첫 뽑기 전에 플레이한 판 수: {s.matchesBeforeFirstPull}"
                : "- 아직 뽑기를 한 번도 안 함");
            builder.AppendLine($"- 드론 강화 {s.upgrades}번 · 접속 {s.sessions}번");
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
