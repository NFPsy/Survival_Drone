using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Enemies;

namespace SurvivalDrone.UI
{
    // 보스전 동안 화면 위쪽 가운데(시간 표시 바로 아래)에 보이는 "BOSS" 체력바.
    //
    // 씬 파일에 미리 만들어 두지 않고, 보스전이 시작될 때 BossFight가 코드로 만든다. (씬 파일을 건드리지 않으려는 것)
    // 기존 HUD 오브젝트의 자식으로 붙인다. 그래야 레벨업 선택 화면·일시정지 화면이 뜰 때 그 어두운 판 뒤로 가려진다.
    //
    // 모양: 위쪽에 "BOSS" 글자와 남은 체력 %, 아래쪽에 붉은 막대. 체력이 줄 때 막대 뒤에 연한 색의 "줄어든 자국"이
    // 천천히 따라 내려와서 얼마나 깎였는지 눈으로 보이게 한다.
    public class BossHealthBar : MonoBehaviour
    {
        // 막대 전체 크기(px, 기준 해상도 1920x1080 기준)와 화면 위쪽에서 떨어진 거리.
        private const float BarWidth = 720f;
        private const float BarHeight = 22f;
        private const float TopOffset = 92f;

        // 줄어든 자국이 따라 내려오는 속도(초당 전체 체력의 비율).
        private const float TrailSpeed = 0.35f;

        private EnemyAI boss;
        private RectTransform fillRect;
        private RectTransform trailRect;
        private Text percentText;
        private float trailRatio = 1f;

        // 보스 체력바를 만든다. HUD를 찾지 못하면(씬 구성이 다르면) 만들지 않고 null을 돌려준다.
        public static BossHealthBar Create(EnemyAI boss)
        {
            var hud = FindFirstObjectByType<HUDController>();
            if (hud == null || boss == null) return null;

            // 글꼴은 HUD에 이미 쓰고 있는 글자의 것을 그대로 쓴다. (웹에서도 한글이 나오는 Pretendard)
            var anyText = hud.GetComponentInChildren<Text>(true);
            Font font = anyText != null ? anyText.font : null;

            var rootObject = new GameObject("BossHealthBar", typeof(RectTransform));
            rootObject.transform.SetParent(hud.transform, false);
            var bar = rootObject.AddComponent<BossHealthBar>();
            bar.Build(boss, font);
            return bar;
        }

        private void Build(EnemyAI target, Font font)
        {
            boss = target;

            // 전체 틀: 화면 위쪽 가운데.
            var root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -TopOffset);
            root.sizeDelta = new Vector2(BarWidth, BarHeight + 36f);

            // 아래쪽 막대 영역.
            var barArea = NewRect("BarArea", root);
            barArea.anchorMin = new Vector2(0f, 0f);
            barArea.anchorMax = new Vector2(1f, 0f);
            barArea.pivot = new Vector2(0.5f, 0f);
            barArea.sizeDelta = new Vector2(0f, BarHeight);
            barArea.anchoredPosition = Vector2.zero;

            AddImage(barArea, "Border", new Color(0.31f, 0.42f, 0.52f, 1f), -2f);        // 가는 테두리(막대보다 2px 크게)
            AddImage(barArea, "Background", new Color(0.04f, 0.05f, 0.07f, 0.95f), 0f);  // 어두운 바닥
            trailRect = AddFill(barArea, "Trail", new Color(1f, 0.78f, 0.7f, 0.85f));    // 줄어든 자국
            fillRect = AddFill(barArea, "Fill", new Color(0.92f, 0.2f, 0.16f, 1f));      // 남은 체력

            // 위쪽 글자: 왼쪽 "BOSS", 오른쪽 남은 체력 %.
            var title = AddLabel(root, "BossLabel", "BOSS", font, 26, new Color(1f, 0.42f, 0.34f), TextAnchor.LowerLeft);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(0f, BarHeight + 2f);
            title.rectTransform.offsetMax = Vector2.zero;

            percentText = AddLabel(root, "PercentLabel", "100%", font, 22, new Color(0.89f, 0.96f, 0.96f), TextAnchor.LowerRight);
            percentText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            percentText.rectTransform.anchorMax = new Vector2(1f, 1f);
            percentText.rectTransform.offsetMin = new Vector2(0f, BarHeight + 2f);
            percentText.rectTransform.offsetMax = Vector2.zero;

            SetFill(1f);
        }

        private void Update()
        {
            // 보스가 쓰러져 사라졌으면 0%로 두고 더 하지 않는다. (곧 BossFight가 이 체력바를 지운다)
            float ratio = 0f;
            if (boss != null && boss.Health != null && boss.Health.MaxHealth > 0f)
                ratio = Mathf.Clamp01(boss.Health.CurrentHealth / boss.Health.MaxHealth);

            // 줄어든 자국: 체력이 줄면 천천히 따라 내려오고, 늘면(회복 등) 바로 맞춘다.
            // 레벨업 선택 화면처럼 시간이 멈춘(timeScale 0) 동안에도 부드럽게 따라오도록 실제 시간으로 센다.
            trailRatio = trailRatio > ratio
                ? Mathf.MoveTowards(trailRatio, ratio, TrailSpeed * Time.unscaledDeltaTime)
                : ratio;

            SetFill(ratio);
            if (percentText != null) percentText.text = Mathf.CeilToInt(ratio * 100f) + "%";
        }

        private void SetFill(float ratio)
        {
            if (fillRect != null) fillRect.anchorMax = new Vector2(ratio, 1f);
            if (trailRect != null) trailRect.anchorMax = new Vector2(trailRatio, 1f);
        }

        // ── 화면 요소를 코드로 만드는 작은 도우미들 ──

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // 부모를 가득 채우는 이미지. grow가 음수면 사방으로 그만큼 커진다(테두리용).
        private static Image AddImage(RectTransform parent, string name, Color color, float grow)
        {
            var rect = NewRect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(grow, grow);
            rect.offsetMax = new Vector2(-grow, -grow);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // 왼쪽에서 오른쪽으로 차오르는 막대. anchorMax.x로 길이를 조절한다. (채우기 이미지는 그림이 있어야 해서 이 방식을 쓴다)
        private static RectTransform AddFill(RectTransform parent, string name, Color color)
        {
            var image = AddImage(parent, name, color, 0f);
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            return rect;
        }

        private static Text AddLabel(RectTransform parent, string name, string content, Font font, int fontSize, Color color, TextAnchor alignment)
        {
            var rect = NewRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
