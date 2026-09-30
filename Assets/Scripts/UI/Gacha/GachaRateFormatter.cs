using System.Text;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 확률표와 규칙 문장을 GachaTable의 값으로 만들어주는 도우미. ("표기 = 실제" 원칙)
    // 화면에 적히는 숫자는 전부 여기서 GachaTable을 읽어 만든다. 표기용 숫자를 따로 적지 않으므로
    // 화면에 보이는 확률과 실제 뽑기 확률이 어긋날 수 없다.
    public static class GachaRateFormatter
    {
        // 등급별 확률을 SSR부터 N 순서로 한 줄씩 만든다. 등급 색이 글자에 입혀진다. (예: "SSR  2.5%")
        public static string BuildRatesRichText(GachaTable table)
        {
            var builder = new StringBuilder();
            for (int rarity = (int)GachaRarity.SSR; rarity >= (int)GachaRarity.N; rarity--)
            {
                var r = (GachaRarity)rarity;
                if (builder.Length > 0) builder.Append('\n');
                builder.Append($"<color={RarityColors.ToHex(r)}>{r}</color>    {table.GetRate(r):0.0}%");
            }
            return builder.ToString();
        }

        // 천장·초기화·10연 규칙을 문장으로 만든다. (확률 공개 팝업에 들어간다)
        public static string BuildRulesText(GachaTable table)
        {
            float tenPullRatio = table.SingleCost > 0 ? (float)table.TenPullCost / table.SingleCost : 0f;
            return $"·  누적 {table.PityCount}회 뽑으면 SSR이 확정됩니다\n" +
                   "·  SSR을 얻으면 누적 횟수는 0으로 돌아갑니다\n" +
                   $"·  10연은 {tenPullRatio:0.##}회 가격입니다";
        }

        // 10연 할인율(%). 예: 1회 300 × 10 = 3,000 대신 2,700이면 10.
        public static int GetTenPullDiscountPercent(GachaTable table)
        {
            int full = table.SingleCost * 10;
            return full > 0 ? UnityEngine.Mathf.RoundToInt((1f - (float)table.TenPullCost / full) * 100f) : 0;
        }
    }
}
