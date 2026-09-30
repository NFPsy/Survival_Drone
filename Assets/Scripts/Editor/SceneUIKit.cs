using SurvivalDrone.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SurvivalDrone.EditorTools
{
    // 로비·뽑기 같은 UI 씬을 코드로 만들 때 함께 쓰는 도구 상자 (에디터 전용).
    // 메인 메뉴와 같은 색·폰트 규칙을 한 곳에 모아 두어서, 씬 빌더마다 같은 코드를 반복해서 적지 않게 한다.
    //
    // 색 규칙(기존 메인 메뉴와 동일): 시안 = 성장/주요 버튼, 주황 = 선택한 위험·잠금 조건,
    // 빨강 = 피해·경고·재화 부족, 금색 = SSR·천장, 어두운 남색 = 배경/패널.
    public static class SceneUIKit
    {
        public static readonly Color BackgroundColor = new Color(0.039f, 0.051f, 0.071f, 1f);
        public static readonly Color GridColor = new Color(0.310f, 0.420f, 0.520f, 0.1f);
        public static readonly Color PanelColor = new Color(0.063f, 0.078f, 0.110f, 0.97f);
        public static readonly Color ButtonColor = new Color(0.086f, 0.106f, 0.149f, 1f);
        public static readonly Color Cyan = new Color(0.310f, 0.847f, 0.910f, 1f);
        public static readonly Color OutlineColor = new Color(0.300f, 0.360f, 0.460f, 0.9f);
        public static readonly Color LightText = new Color(0.890f, 0.961f, 0.961f, 1f);
        public static readonly Color MutedText = new Color(0.588f, 0.706f, 0.714f, 1f);
        public static readonly Color DarkText = new Color(0.043f, 0.055f, 0.078f, 1f);
        public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);

        // 앵커(화면의 어느 지점을 기준으로 배치할지) 자주 쓰는 값.
        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);
        public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        public static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        public static Font SansFont { get; private set; }
        public static Font MonoFont { get; private set; }

        // 새 UI 씬을 만든다: 카메라, 이벤트 시스템, 캔버스(1920x1080 기준), 배경, 격자무늬까지.
        // 만든 캔버스의 Transform을 돌려주고, 화면 요소는 그 아래에 이어서 만들면 된다.
        // 저장하지 않은 변경이 있는 씬이 열려 있으면 저장할지 묻고, 취소하면 null을 돌려준다.
        public static Transform CreateCanvasScene(out Scene scene)
        {
            SansFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Geist-Variable.ttf");
            MonoFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/GeistMono-Variable.ttf");
            var gridSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/UI_GridPattern.png");

            scene = default;
            if (!EditorSafety.CanRunEditModeTool("씬 생성 도구")) return null;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // 0.5 = 가로·세로를 절반씩 반영. 화면이 아주 넓거나 좁을 때 위아래(또는 좌우)가 한쪽으로만 쏠려 잘리거나 겹치는 것을 줄인다.
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasObject.transform;

            var background = NewImage("Background", root, BackgroundColor);
            Stretch(background.rectTransform);
            background.raycastTarget = false;
            var grid = NewImage("GridOverlay", root, GridColor);
            Stretch(grid.rectTransform);
            grid.sprite = gridSprite;
            grid.type = Image.Type.Tiled;
            grid.raycastTarget = false;
            return root;
        }

        // 만든 씬을 파일로 저장하고 빌드 설정(Build Settings)의 씬 목록에 없으면 추가한다.
        public static void SaveAndRegister(Scene scene, string scenePath)
        {
            EditorSceneManager.SaveScene(scene, scenePath);

            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == scenePath) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // 컴포넌트의 clickSound 칸에 버튼 클릭 효과음(메인 메뉴와 같은 것)을 연결한다. 없으면 경고만 남기고 넘어간다.
        public static void SetClickSound(Component component)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/SFX_UIClick.wav");
            if (clip == null)
            {
                Debug.LogWarning("[Stage] 클릭 효과음(SFX_UIClick.wav)을 찾지 못해 연결하지 않았습니다.");
                return;
            }
            var serialized = new SerializedObject(component);
            serialized.FindProperty("clickSound").objectReferenceValue = clip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- UI 요소 만들기 ----------------

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text NewText(string name, Transform parent, string content, int fontSize, Color color, Font font, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.font = font;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // 버튼 = 배경 이미지 + Button + 테두리 + 글자. 글자는 버튼 전체를 채운다.
        public static Image NewButton(string name, Transform parent, string label, int fontSize, Color fill, Color textColor)
        {
            var image = NewImage(name, parent, fill);
            image.gameObject.AddComponent<Button>().targetGraphic = image;
            AddOutline(image.gameObject, fill == Cyan ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f) : OutlineColor);

            var text = NewText("Text", image.transform, label, fontSize, textColor, SansFont, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return image;
        }

        // 어두운 배경을 깔아서 뒤쪽 화면을 눌리지 않게 막는 전체 화면 팝업 뿌리. 그 안에 상자(Box)를 만들어 돌려준다.
        public static RectTransform NewPopup(string name, Transform parent, Vector2 boxSize, out Image root)
        {
            root = NewImage(name, parent, DimColor);
            Stretch(root.rectTransform);

            var box = NewImage("Box", root.transform, PanelColor);
            Place(box.rectTransform, Center, Center, Center, Vector2.zero, boxSize);
            AddOutline(box.gameObject, OutlineColor);
            box.gameObject.AddComponent<UIPopupAnimator>(); // 기존 팝업과 같은 "살짝 커지며 나타나는" 연출 (CanvasGroup도 함께 붙는다)
            return box.rectTransform;
        }

        public static void AddOutline(GameObject go, Color color)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, 1f);
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
