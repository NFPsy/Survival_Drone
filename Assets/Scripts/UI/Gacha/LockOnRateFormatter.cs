using System.Text;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 락온 뽑기의 확률표와 규칙 문장을 LockOnTable의 값으로 만들어주는 도우미. ("표기 = 실제" 원칙)
    // GachaRateFormatter와 같은 방식: 화면에 적히는 숫자는 전부 여기서 LockOnTable을 읽어 만든다.
    public static class LockOnRateFormatter
    {
        // 칸 하나의 등급별 확률을 SSR부터 N 순서로 한 줄씩 만든다. 등급 색이 글자에 입혀진다. (예: "SR    15.0%")
        // 확률이 0인 등급은 "나오지 않음"으로 적는다.
        public static string BuildRatesRichText(LockOnTable table)
        {
            var builder = new StringBuilder();
            for (int rarity = (int)GachaRarity.SSR; rarity >= (int)GachaRarity.N; rarity--)
            {
                var r = (GachaRarity)rarity;
                if (builder.Length > 0) builder.Append('\n');
                float rate = table.GetRate(r);
                string rateText = rate > 0f ? $"{rate:0.0}%" : "나오지 않음";
                builder.Append($"<color={RarityColors.ToHex(r)}>{r}</color>    {rateText}");
            }
            return builder.ToString();
        }

        // 규칙을 짧은 문장 여러 개로 만든다. (확률 공개 팝업에 들어간다. 한 줄이 길면 팝업 밖으로 넘치므로 문장을 나눴다)
        public static string BuildRulesText(LockOnTable table)
        {
            string premium = table.RerollPremium > 0f ? $" (잠근 칸이 하나 늘 때마다 +{table.RerollPremium * 100f:0.#}%)" : "";
            string ssr = table.GetRate(GachaRarity.SSR) <= 0f
                ? "·  SSR은 나오지 않습니다 (SSR은 기본 뽑기에서만 나옵니다)\n"
                : "";
            return $"·  처음 공개 {table.FirstCost:N0}코어로 드론 {table.SlotCount}칸이 한꺼번에 공개됩니다\n" +
                   $"·  {table.LockMinRarity} 이상인 칸만 잠글 수 있고, 잠그지 않은 칸만 다시 뽑습니다\n" +
                   $"·  재뽑기 비용: 다시 뽑는 칸 × {table.RerollUnitCost:N0}코어{premium}\n" +
                   "·  확정하면 잠근 칸만 받습니다. 쓴 코어는 돌려받지 못합니다\n" +
                   $"·  받지 못한 칸은 조각으로 바뀝니다 (기본 뽑기 중복 조각의 {table.UnlockedShardPercent}%)\n" +
                   ssr +
                   "·  천장은 적용되지 않습니다";
        }

        // 화면 위쪽에 보여줄 한 줄 안내.
        public static string BuildSubtitle(LockOnTable table)
        {
            return $"{table.SlotCount}칸을 공개하고, 마음에 드는 칸({table.LockMinRarity} 이상)을 잠근 뒤 나머지만 다시 뽑으세요";
        }
    }
}
