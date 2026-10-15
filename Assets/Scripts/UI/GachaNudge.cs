using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // "아직 뽑기를 한 번도 안 한 사람"에게 뽑기 화면으로 가는 길을 눈에 띄게 알려 주는 도우미.
    //
    // 왜 필요한가:
    //  CBT 3번 테스터가 "튜토리얼이 없어서 뽑기 기능을 못 보고 바로 들어갔다가 나중에야 로비에서 발견했다"고 했다.
    //  결과 화면에서는 "재도전" 버튼이 가장 눈에 띄고(시안색), 로비의 "뽑기" 버튼은 다른 버튼과 똑같은 색이라서
    //  시작 코어 2,700이 있어도 뽑기가 있다는 걸 모를 수 있다.
    //
    // 언제 나오는가 (아래 조건을 모두 만족할 때만):
    //  - 일반 뽑기와 락온 뽑기를 한 번도 안 했다.
    //  - 코어가 뽑기 1회 가격(300) 이상이다.
    //  첫 뽑기를 하면 조건이 깨져서 저절로 사라진다. 새 저장 칸은 만들지 않고, 기존 기록(logStats)만 읽는다.
    //
    // 화면은 코드로 만들고 씬 파일은 건드리지 않는다.
    public static class GachaNudge
    {
        // 일반 뽑기 1회 가격. GachaTable의 _singleCost(300)와 같은 값이다.
        // (로비·결과 화면에는 GachaTable이 없어서 여기에 적어 둔다. 가격을 바꾸면 이 값도 같이 바꾼다)
        public const int MinCore = 300;

        // 안내에 쓰는 강조색(시안). 로비의 "출격" 버튼과 같은 색이다.
        public static readonly Color HighlightColor = new Color(0.310f, 0.847f, 0.910f, 1f);

        // 저장 데이터를 보고 안내를 보여 줄지 정한다. 화면과 상관없는 계산이라 따로 떼어 검증 도구가 바로 부를 수 있다.
        public static bool ShouldShow(SaveData data)
        {
            if (data == null || data.logStats == null) return false;
            if (data.logStats.pullsTotal > 0 || data.logStats.lockOnConfirms > 0) return false;
            return data.core >= MinCore;
        }

        // 지금 불러온 저장 슬롯 기준. 메인 메뉴를 거치지 않고 씬만 단독 실행한 경우(CurrencyManager 없음)에는 보여 주지 않는다.
        public static bool ShouldShow()
        {
            if (CurrencyManager.Instance == null) return false;
            return ShouldShow(SaveManager.Data);
        }

        // 버튼의 "모양"(배경색, 글자색, 아이콘색)을 담아 두는 작은 그릇.
        public struct Look
        {
            public Color background;
            public Color text;
            public Color icon;
        }

        // 버튼의 배경색(Image)과 글자색(Text), 아이콘색(글자 옆의 작은 그림 Image)을 읽어 둔다.
        // 아이콘색도 같이 옮겨야 한다: 배경이 시안으로 바뀌었는데 아이콘이 그대로 시안이면 아이콘이 배경에 묻혀 안 보인다.
        public static Look Capture(Button button)
        {
            var look = new Look { background = Color.white, text = Color.white, icon = Color.white };
            if (button == null) return look;
            var background = button.targetGraphic as Image;
            if (background != null) look.background = background.color;
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) look.text = label.color;
            foreach (var image in button.GetComponentsInChildren<Image>(true))
            {
                if (image == background) continue;
                look.icon = image.color;
                break;
            }
            return look;
        }

        // 읽어 둔 모양을 버튼에 입힌다.
        public static void Apply(Button button, Look look)
        {
            if (button == null) return;
            var background = button.targetGraphic as Image;
            if (background != null) background.color = look.background;
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.color = look.text;
            foreach (var image in button.GetComponentsInChildren<Image>(true))
            {
                if (image == background) continue;
                image.color = look.icon;
            }
        }

        // 안내 글자를 하나 만든다. 위치는 부모 안에서의 앵커·피벗·좌표로 정한다.
        public static Text CreateText(Transform parent, Font font, string name, string content, int fontSize, TextAnchor alignment,
                                      Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = HighlightColor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false; // 글자가 버튼 클릭을 가로막지 않게
            return text;
        }
    }
}
