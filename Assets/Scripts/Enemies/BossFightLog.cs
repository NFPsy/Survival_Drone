using UnityEngine;

namespace SurvivalDrone.Enemies
{
    // 보스전 한 판의 "상세 기록"을 모아서, 판이 끝날 때 테스트(CBT/OBT) 기록 한 줄에 덧붙일 글로 만드는 도구.
    // 보스에게 받은 피해 "합계"만으로는 예고를 보고 피할 수 있는지 알 수 없어서 만들었다.
    // (예: 내려찍기를 9번 썼는데 4번 맞았다 → 피하는 비율을 숫자로 볼 수 있다)
    //
    // 모으는 것:
    //  - 보스전에 들어갈 때 내 상태: 이동속도, 최대 체력과 그때 체력 비율, 레벨
    //  - 내려찍기·돌진을 보스가 몇 번 썼고 그중 몇 번 맞았는지 (분노 상태에서의 횟수는 따로 센다)
    //  - 분노가 보스전 시작 몇 초 뒤에 시작됐는지
    //  - 판이 끝났을 때 내 체력 비율 (죽었으면 0%)
    // DamageSourceLog·PerfLog와 같은 방식: static이라 씬을 다시 불러도 값이 남으므로 보스전이 시작될 때 Reset으로 비운다.
    public static class BossFightLog
    {
        private static int slamUsed;
        private static int slamHit;
        private static int rageSlamUsed;
        private static int rageSlamHit;
        private static int dashUsed;
        private static int dashHit;
        private static int rageDashUsed;
        private static int rageDashHit;
        private static float rageStartSeconds = -1f;   // 아직 분노하지 않았으면 -1

        private static float entrySpeed;
        private static float entryMaxHealth;
        private static float entryHealthRatio;
        private static int entryLevel;

        // 보스전이 시작될 때 지난 보스전의 값을 비운다.
        public static void Reset()
        {
            slamUsed = slamHit = rageSlamUsed = rageSlamHit = 0;
            dashUsed = dashHit = rageDashUsed = rageDashHit = 0;
            rageStartSeconds = -1f;
            entrySpeed = entryMaxHealth = entryHealthRatio = 0f;
            entryLevel = 0;
        }

        // 보스전에 들어가는 순간 내 상태를 적어 둔다. healthRatio는 0~1.
        public static void RecordEntry(float speed, float maxHealth, float healthRatio, int level)
        {
            entrySpeed = speed;
            entryMaxHealth = maxHealth;
            entryHealthRatio = healthRatio;
            entryLevel = level;
        }

        // 내려찍기가 터졌을 때 한 번. rage = 보스가 분노 상태였는지, hit = 플레이어가 맞았는지.
        public static void RecordSlam(bool rage, bool hit)
        {
            slamUsed++;
            if (hit) slamHit++;
            if (!rage) return;
            rageSlamUsed++;
            if (hit) rageSlamHit++;
        }

        // 돌진이 끝났을 때 한 번.
        public static void RecordDash(bool rage, bool hit)
        {
            dashUsed++;
            if (hit) dashHit++;
            if (!rage) return;
            rageDashUsed++;
            if (hit) rageDashHit++;
        }

        // 분노가 시작됐을 때. secondsSinceFightStart = 보스전이 시작된 지 몇 초째인지.
        public static void RecordRageStart(float secondsSinceFightStart)
        {
            if (rageStartSeconds < 0f) rageStartSeconds = secondsSinceFightStart;
        }

        // 기록 한 줄 끝에 덧붙일 글. 앞에는 항상 공백이 붙는다. healthRatioAtEnd = 판이 끝났을 때 내 체력 비율(0~1).
        // 맞은 횟수/쓴 횟수 순서이고, 분노 항목은 분노가 시작된 판에만 붙는다.
        // 예) " 보스입장=속도7.9/체력112(100%)/레벨23 분노시작=41.2초 내려찍기=4/9 분노내려찍기=4/4 돌진=0/4 분노돌진=0/2 보스전끝체력=60%"
        public static string BuildText(float healthRatioAtEnd)
        {
            var text = new System.Text.StringBuilder();
            text.Append($" 보스입장=속도{entrySpeed:F1}/체력{entryMaxHealth:F0}({Mathf.RoundToInt(entryHealthRatio * 100f)}%)/레벨{entryLevel}");
            if (rageStartSeconds >= 0f) text.Append($" 분노시작={rageStartSeconds:F1}초");
            text.Append($" 내려찍기={slamHit}/{slamUsed}");
            if (rageStartSeconds >= 0f) text.Append($" 분노내려찍기={rageSlamHit}/{rageSlamUsed}");
            text.Append($" 돌진={dashHit}/{dashUsed}");
            if (rageStartSeconds >= 0f) text.Append($" 분노돌진={rageDashHit}/{rageDashUsed}");
            text.Append($" 보스전끝체력={Mathf.RoundToInt(Mathf.Clamp01(healthRatioAtEnd) * 100f)}%");
            return text.ToString();
        }
    }
}
