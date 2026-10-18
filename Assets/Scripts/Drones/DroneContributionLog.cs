using System.Collections.Generic;

namespace SurvivalDrone.Drones
{
    // 이번 판에서 드론 종류별로 "얼마나 기여했는지"(준 피해, 회복량)를 모아 두는 기록용 도우미.
    // 테스트(OBT) 의견에 "회복·폭발 드론이 약하다"가 있었는데, 숫자로 비교할 자료가 없어서 만들었다.
    // 판이 끝날 때 기록 한 줄의 "최종드론=..." 뒤에 " 드론기여=Sniper피해1234/Heal회복210" 모양으로 덧붙는다.
    //
    // 센 것:
    //  - 공격 드론(저격·근접·폭발): 적의 체력을 "실제로 깎은 양". 남은 체력보다 센 공격(마지막 일격)은 남은 체력만큼만 센다.
    //    (그래야 한 방에 죽는 약한 적을 광역으로 쓸어도 기여가 부풀려지지 않는다)
    //  - 회복 드론: 플레이어 체력을 "실제로 채운 양". 체력이 가득일 때의 회복은 0으로 센다.
    // 수집 드론은 피해도 회복도 없어서 세지 않는다.
    //
    // DamageSourceLog·LevelUpPickLog와 같은 방식이다: static이라 씬을 다시 불러도 값이 남으므로 판이 시작될 때 Reset으로 비운다.
    // static(어디서든 바로 부르는 방식)인 이유: 피해를 주는 곳(드론들)과 기록을 남기는 곳(결과 화면)이 서로 다른 스크립트라서다.
    public static class DroneContributionLog
    {
        // 드론 종류 → 이번 판에서 준 피해 합계. (같은 종류 드론이 여러 마리여도 합친다)
        private static readonly Dictionary<DroneType, float> damageByType = new Dictionary<DroneType, float>();

        // 회복 드론이 실제로 채운 체력 합계.
        private static float healed;

        // 새 판이 시작될 때 모든 숫자를 비운다. (GameManager가 판 시작 때 부른다)
        public static void Reset()
        {
            damageByType.Clear();
            healed = 0f;
        }

        // 공격 드론이 적에게 피해를 줬을 때 부른다. dealt = 실제로 깎인 체력(EnemyAI.ApplyDamage가 돌려준 값).
        public static void RecordDamage(DroneType type, float dealt)
        {
            if (dealt <= 0f) return;

            damageByType.TryGetValue(type, out float sum);
            damageByType[type] = sum + dealt;
        }

        // 회복 드론이 체력을 채웠을 때 부른다. amount = 실제로 채워진 양.
        public static void RecordHeal(float amount)
        {
            if (amount > 0f) healed += amount;
        }

        // 기록 끝에 덧붙일 문장. 앞에는 항상 공백이 붙는다. 피해가 큰 종류부터 나열하고 회복량이 맨 뒤에 온다.
        // 예) " 드론기여=Sniper피해1234/Explosion피해560/Heal회복210"  (아무 기여도 없으면 빈 글자)
        public static string BuildSummary()
        {
            if (damageByType.Count == 0 && healed <= 0f) return "";

            var list = new List<KeyValuePair<DroneType, float>>(damageByType);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));

            var parts = new List<string>();
            foreach (var pair in list) parts.Add($"{pair.Key}피해{pair.Value:F0}");
            if (healed > 0f) parts.Add($"Heal회복{healed:F0}");

            return " 드론기여=" + string.Join("/", parts);
        }
    }
}
