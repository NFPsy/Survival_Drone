using UnityEngine;
using UnityEngine.UI;

namespace SurvivalDrone.UI
{
    // 글자(Text) 바로 왼쪽에 아이콘을 붙여 주는 컴포넌트.
    // 글자가 바뀌어도(숫자가 커져도, 다른 말로 바뀌어도) 아이콘이 글자의 왼쪽 끝을 따라간다.
    //
    // 왜 필요한가:
    //  "코어  1,200" 같은 글자는 오른쪽 정렬이라 숫자가 길어질수록 글자의 왼쪽 끝이 왼쪽으로 늘어난다.
    //  아이콘을 한 자리에 고정해 두면 숫자가 짧을 때는 아이콘과 글자 사이가 멀어진다.
    //  그래서 "지금 글자가 실제로 차지한 폭"을 재서 그 왼쪽에 아이콘을 놓는다.
    //
    // 쓰는 법: 글자(Text) 오브젝트에 이 컴포넌트를 붙이고, 그 글자의 자식으로 Image(아이콘)를 만들어 icon 칸에 연결한다.
    // 아이콘이 꺼져 있으면(예: 아이콘이 필요 없는 선택지) 아무것도 하지 않는다.
    //
    // ExecuteAlways: 게임을 실행하지 않은 에디터에서도 한 번 위치를 잡아 준다. (화면 캡처·씬 편집 때 위치가 맞게 보이도록)
    [ExecuteAlways]
    [RequireComponent(typeof(Text))]
    public class TextIconPrefix : MonoBehaviour
    {
        [SerializeField] private RectTransform icon;

        // 아이콘과 글자 사이 간격(픽셀).
        [SerializeField] private float gap = 10f;

        private Text text;

        private void OnEnable()
        {
            text = GetComponent<Text>();
            Reposition();
        }

        // 실행 중에는 글자가 자주 바뀌므로 매 프레임 한 번씩 위치를 다시 맞춘다. (값이 같으면 아무것도 바꾸지 않아 가볍다)
        private void LateUpdate()
        {
            if (Application.isPlaying) Reposition();
        }

        public void Reposition()
        {
            if (icon == null) return;
            if (text == null) text = GetComponent<Text>();
            if (text == null || !icon.gameObject.activeSelf) return;

            // 글자가 비어 있으면(예: 아직 값이 없는 칸) 아이콘만 덩그러니 남지 않도록 아이콘 그림을 끈다.
            var iconGraphic = icon.GetComponent<Graphic>();
            bool hasText = !string.IsNullOrEmpty(text.text);
            if (iconGraphic != null && iconGraphic.enabled != hasText) iconGraphic.enabled = hasText;
            if (!hasText) return;

            var rect = text.rectTransform.rect;
            float halfW = rect.width * 0.5f;
            float halfH = rect.height * 0.5f;

            // 글자가 실제로 차지하는 폭. 상자보다 넓을 수는 없다.
            float textWidth = Mathf.Min(text.preferredWidth, rect.width);

            // 글자 정렬에 따라 "글자의 왼쪽 끝"이 상자 중심에서 얼마나 떨어져 있는지 계산한다.
            float leftEdge;
            switch (text.alignment)
            {
                case TextAnchor.UpperLeft:
                case TextAnchor.MiddleLeft:
                case TextAnchor.LowerLeft:
                    leftEdge = -halfW;
                    break;
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                case TextAnchor.LowerRight:
                    leftEdge = halfW - textWidth;
                    break;
                default: // 가운데 정렬
                    leftEdge = -textWidth * 0.5f;
                    break;
            }

            float iconWidth = icon.rect.width;
            float x = leftEdge - gap - iconWidth * 0.5f;

            // 세로 위치: 위쪽 정렬이면 첫 줄 높이에, 아래쪽 정렬이면 마지막 줄 높이에, 가운데면 가운데에 맞춘다.
            float lineHeight = text.fontSize * 1.2f;
            float y;
            switch (text.alignment)
            {
                case TextAnchor.UpperLeft:
                case TextAnchor.UpperCenter:
                case TextAnchor.UpperRight:
                    y = halfH - lineHeight * 0.5f;
                    break;
                case TextAnchor.LowerLeft:
                case TextAnchor.LowerCenter:
                case TextAnchor.LowerRight:
                    y = -halfH + lineHeight * 0.5f;
                    break;
                default:
                    y = 0f;
                    break;
            }

            // 아이콘은 글자 상자의 한가운데를 기준점으로 삼도록 맞춰 두고 위치만 바꾼다.
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            var target = new Vector2(x, y);
            if ((icon.anchoredPosition - target).sqrMagnitude > 0.0001f) icon.anchoredPosition = target;
        }
    }
}
