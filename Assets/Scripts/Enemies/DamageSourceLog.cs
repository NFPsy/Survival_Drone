using System.Collections.Generic;

namespace SurvivalDrone.Enemies
{
    // 이번 판에서 플레이어가 "어떤 적에게 얼마나 맞았는지"를 세어 두는 기록용 도우미.
    // 판이 끝날 때 테스트(CBT) 기록 한 줄에 "사망원인"(마지막으로 맞은 적 종류)과 "받은피해"(적 종류별 피해 합계)를 덧붙이려고 만들었다.
    // (로그에 "몇 초에 죽었다"만 있고 "무엇에게 죽었는지"가 없어서, 빠른 로봇 등장 시점 같은 조정이 효과가 있었는지 알 수 없었다)
    //
    // 마지막으로 맞은 적만 적지 않고 종류별 피해 합계도 같이 적는 이유:
    // 마지막 일격은 우연히 약한 로봇일 수 있어서, 그것만 보면 "진짜로 체력을 깎은 적"을 잘못 짚을 수 있다.
    //
    // LevelUpPickLog와 같은 방식(static, 판이 시작될 때 Reset)이다.
    // static(어디서든 바로 부르는 방식)인 이유: 피해를 주는 곳(EnemyAI)과 기록을 남기는 곳(결과 화면)이 서로 다른 스크립트라,
    // 인스펙터 연결 없이 값을 주고받기 위해서다.
    public static class DamageSourceLog
    {
        // 적 종류 이름 → 지금까지 그 종류에게 받은 피해 합계. (이름이 들어간 순서가 곧 처음 맞은 순서)
        private static readonly Dictionary<string, float> damageByKind = new Dictionary<string, float>();

        // 가장 마지막에 플레이어를 때린 적 종류 이름. 아무에게도 안 맞았으면 빈 글자.
        private static string lastHitKind = "";

        // 새 판이 시작될 때 모든 숫자를 비운다. (GameManager가 판 시작 때 부른다)
        public static void Reset()
        {
            damageByKind.Clear();
            lastHitKind = "";
        }

        // 플레이어가 적에게 한 번 맞을 때 부른다. kindLabel = 적 종류 이름(예: "빠른"), damage = 이번에 받은 피해량.
        // 반드시 Health.TakeDamage를 부르기 "전에" 호출해야 한다.
        // 이 한 방으로 죽으면 TakeDamage 안에서 곧바로 결과 화면이 기록을 남기기 때문에, 나중에 부르면 마지막 일격이 빠진다.
        public static void Record(string kindLabel, float damage)
        {
            if (string.IsNullOrEmpty(kindLabel) || damage <= 0f) return;

            damageByKind.TryGetValue(kindLabel, out float sum);
            damageByKind[kindLabel] = sum + damage;
            lastHitKind = kindLabel;
        }

        // 적 종류를 기록용 짧은 한글 이름으로 바꾼다. 미니 보스는 보스 데이터를 줄여서 쓰므로 따로 구분한다.
        public static string LabelOf(EnemyKind kind, bool isMiniBoss)
        {
            if (isMiniBoss) return "미니보스";
            switch (kind)
            {
                case EnemyKind.Weak: return "약한";
                case EnemyKind.Tough: return "튼튼한";
                case EnemyKind.Fast: return "빠른";
                case EnemyKind.Strong: return "강한";
                case EnemyKind.Boss: return "보스";
                default: return kind.ToString();
            }
        }

        // 판 기록 끝에 덧붙일 문장을 만든다.
        //  패배(lost = true)이면 맨 앞에 "사망원인=빠른"을 붙인다. 예: "사망원인=빠른 받은피해=빠른88/약한40/튼튼한20"
        //  승리이면 "받은피해=..."만 남긴다 (이긴 판에서도 어떤 적이 가장 아팠는지 알 수 있다).
        //  한 번도 안 맞았으면 빈 글자를 돌려줘서, 기록에 아무것도 덧붙지 않게 한다.
        public static string BuildSummary(bool lost)
        {
            if (damageByKind.Count == 0) return "";

            // 피해가 큰 종류부터 보이도록 정렬한다.
            var list = new List<KeyValuePair<string, float>>(damageByKind);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));

            var parts = new List<string>();
            foreach (var pair in list) parts.Add($"{pair.Key}{pair.Value:F0}");

            string damage = "받은피해=" + string.Join("/", parts);
            return lost ? $"사망원인={lastHitKind} {damage}" : damage;
        }
    }
}
