using UnityEngine;
using UnityEngine.UI;

namespace SurvivalDrone.LevelUp
{
    // 레벨업 선택 카드의 장식(위쪽 띠·위쪽 은은한 빛·네 모서리 꺾쇠)을 "카드 종류 색"으로 칠해 주는 스크립트.
    // 카드(선택지 버튼) 오브젝트에 붙이고, LevelUpUI가 선택지를 채울 때 SetColor로 색을 정해 준다.
    //   신규 드론 = 시안, 강화 = 연한 초록, 능력치 = 회색 (글자 라벨 색과 같은 색)
    // 장식 이미지가 연결돼 있지 않으면 아무것도 하지 않는다.
    public class LevelUpCardAccent : MonoBehaviour
    {
        // 카드 맨 위의 얇은 띠.
        [SerializeField] private Image band;

        // 카드 위쪽에서 아래로 옅어지는 빛.
        [SerializeField] private Image gradient;

        // 네 모서리의 꺾쇠 장식.
        [SerializeField] private Image[] corners;

        // 종류 색을 정한다. 띠는 진하게, 빛은 옅게, 꺾쇠는 중간 정도로 칠한다.
        public void SetColor(Color color)
        {
            if (band != null) band.color = WithAlpha(color, 1f);
            if (gradient != null) gradient.color = WithAlpha(color, 0.25f);
            if (corners == null) return;
            foreach (var corner in corners)
                if (corner != null) corner.color = WithAlpha(color, 0.8f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
