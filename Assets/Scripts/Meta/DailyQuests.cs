using System;
using System.Globalization;

namespace SurvivalDrone.Meta
{
    // 일일 퀘스트 수치와 "오늘 것인지" 판단하는 규칙 모음. 퀘스트는 3개로 단순하다.
    //  - 0번: 3분 버티기, 1번: 6분 버티기, 2번: 클리어하기 (한 판 안에서 달성)
    // 달성(Done)은 판에서 자동으로 기록되고, 코어는 로비의 일일 퀘스트 창에서 "받기"를 눌러 받는다(Claimed).
    // 하루가 바뀌면(기기 날짜 기준) 달성·수령 기록이 모두 초기화된다.
    public static class DailyQuests
    {
        // 퀘스트 이름 (로비 창에 표시).
        public static readonly string[] Titles = { "3분 버티기", "6분 버티기", "클리어하기" };

        // 퀘스트 보상 코어.
        public static readonly int[] Cores = { 50, 100, 150 };

        // 퀘스트 개수.
        public static int Count => Cores.Length;

        // 기기의 오늘 날짜 글자("2026-10-01"). 언어 설정에 따라 숫자 모양이 달라지지 않도록 고정 형식으로 만든다.
        public static string Today() => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // 저장된 날짜가 오늘이 아니면 달성·수령 기록을 새로 시작한다. today는 검증용(비우면 기기의 오늘).
        public static void EnsureToday(SaveData data, string today = null)
        {
            today ??= Today();
            if (data.dailyQuestDate == today) return;

            data.dailyQuestDate = today;
            data.dailyQuestDoneMask = 0;
            data.dailyQuestClaimedMask = 0;
        }

        // 오늘의 퀘스트 하나를 달성 처리한다. 이미 달성했거나 번호가 잘못됐으면 아무것도 하지 않는다.
        public static void MarkDone(SaveData data, int index, string today = null)
        {
            if (index < 0 || index >= Count) return;
            EnsureToday(data, today);
            data.dailyQuestDoneMask |= 1 << index;
        }

        public static bool IsDone(SaveData data, int index, string today = null)
        {
            if (index < 0 || index >= Count) return false;
            EnsureToday(data, today);
            return (data.dailyQuestDoneMask & (1 << index)) != 0;
        }

        public static bool IsClaimed(SaveData data, int index, string today = null)
        {
            if (index < 0 || index >= Count) return false;
            EnsureToday(data, today);
            return (data.dailyQuestClaimedMask & (1 << index)) != 0;
        }
    }
}
