using UnityEngine;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 등급별 색을 한 곳에서 정해두는 도우미. 뽑기 화면, 확률 팝업, 뽑기 결과 카드가 모두 같은 색을 쓰도록 한다.
    //  N 회색 / R 파랑 / SR 보라 / SSR 금색  (금색은 천장 게이지에도 쓴다)
    public static class RarityColors
    {
        public static readonly Color Gold = new Color(1f, 0.82f, 0.25f);

        public static Color Get(GachaRarity rarity)
        {
            switch (rarity)
            {
                case GachaRarity.N: return new Color(0.65f, 0.68f, 0.72f);
                case GachaRarity.R: return new Color(0.35f, 0.60f, 1f);
                case GachaRarity.SR: return new Color(0.72f, 0.45f, 1f);
                case GachaRarity.SSR: return Gold;
                default: return Color.white;
            }
        }

        // 글자 일부만 색을 입힐 때 쓰는 "#RRGGBB" 문자열.
        public static string ToHex(GachaRarity rarity) => "#" + ColorUtility.ToHtmlStringRGB(Get(rarity));
    }
}
