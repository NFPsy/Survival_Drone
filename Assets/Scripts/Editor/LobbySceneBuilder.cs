using SurvivalDrone.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SurvivalDrone.EditorTools
{
    // 로비 씬(Assets/Scenes/Lobby.unity)을 코드로 만들어주는 에디터 전용 도구.
    // 메뉴 SurvivalDrone → Build → Create Lobby Scene 을 누르면 로비 씬을 처음부터 다시 만들어 저장한다.
    //
    // 화면 요소를 손으로 하나하나 배치하는 대신 코드로 만들면,
    //  - 배치/색/글자 크기가 코드에 그대로 남아서 나중에 고치거나 되돌리기 쉽고
    //  - 메인 메뉴와 같은 색·폰트 규칙을 한 곳에서 맞출 수 있다.
    // ※ 다시 실행하면 로비 씬이 덮어써진다. 씬을 손으로 고쳤다면 실행 전에 커밋해두자.
    //
    // 색 규칙(기존 메인 메뉴와 동일): 시안 = 성장/주요 버튼, 주황 = 선택한 위험·잠금 조건, 어두운 남색 = 배경/패널.
    public static class LobbySceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Lobby.unity";

        private static readonly Color BackgroundColor = new Color(0.039f, 0.051f, 0.071f, 1f);
        private static readonly Color GridColor = new Color(0.310f, 0.420f, 0.520f, 0.1f);
        private static readonly Color PanelColor = new Color(0.063f, 0.078f, 0.110f, 0.97f);
        private static readonly Color ButtonColor = new Color(0.086f, 0.106f, 0.149f, 1f);
        private static readonly Color Cyan = new Color(0.310f, 0.847f, 0.910f, 1f);
        private static readonly Color OutlineColor = new Color(0.300f, 0.360f, 0.460f, 0.9f);
        private static readonly Color LightText = new Color(0.890f, 0.961f, 0.961f, 1f);
        private static readonly Color MutedText = new Color(0.588f, 0.706f, 0.714f, 1f);
        private static readonly Color DarkText = new Color(0.043f, 0.055f, 0.078f, 1f);

        private static Font _sansFont;
        private static Font _monoFont;

        [MenuItem("SurvivalDrone/Build/Create Lobby Scene")]
        public static void Build()
        {
            _sansFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Geist-Variable.ttf");
            _monoFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/GeistMono-Variable.ttf");
            var gridSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/UI_GridPattern.png");

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- 카메라 / 이벤트 시스템 ----
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // ---- 캔버스 (메인 메뉴와 같은 1920x1080 기준) ----
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // 0.5 = 가로·세로를 절반씩 반영. 화면이 아주 넓거나 좁을 때 위아래(또는 좌우)가 한쪽으로만 쏠려 잘리거나 겹치는 것을 줄인다.
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasObject.transform;

            var background = NewImage("Background", root, BackgroundColor);
            Stretch(background.rectTransform);
            var grid = NewImage("GridOverlay", root, GridColor);
            Stretch(grid.rectTransform);
            grid.sprite = gridSprite;
            grid.type = Image.Type.Tiled;
            grid.raycastTarget = false;
            background.raycastTarget = false;

            // ---- 상단 바: 게임 이름 + 재화 ----
            var topBar = NewRect("TopBar", root);
            Stretch(topBar);
            var gameName = NewText("GameNameText", topBar, "드론 지휘관", 40, Cyan, _monoFont, TextAnchor.MiddleLeft);
            Place(gameName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -30f), new Vector2(500f, 60f));
            var creditText = NewText("CreditText", topBar, "크레딧  0", 34, LightText, _sansFont, TextAnchor.MiddleRight);
            Place(creditText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-380f, -30f), new Vector2(320f, 60f));
            var coreText = NewText("CoreText", topBar, "코어  0", 34, Cyan, _sansFont, TextAnchor.MiddleRight);
            Place(coreText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(320f, 60f));

            // ---- 스테이지 카드 (로비 가운데) ----
            var card = NewImage("StageCard", root, PanelColor);
            Place(card.rectTransform, Center, Center, Center, new Vector2(0f, 70f), new Vector2(760f, 460f));
            AddOutline(card.gameObject, OutlineColor);
            FillCardTexts(card.transform, 1f);
            var prev = NewButton("BtnPrev", card.transform, "<", 56, ButtonColor, LightText);
            Place(prev.rectTransform, Center, Center, Center, new Vector2(-470f, 0f), new Vector2(90f, 140f));
            var next = NewButton("BtnNext", card.transform, ">", 56, ButtonColor, LightText);
            Place(next.rectTransform, Center, Center, Center, new Vector2(470f, 0f), new Vector2(90f, 140f));

            var stageList = NewButton("BtnStageList", root, "스테이지 목록", 30, ButtonColor, LightText);
            Place(stageList.rectTransform, Center, Center, Center, new Vector2(0f, -215f), new Vector2(320f, 64f));

            var launch = NewButton("BtnLaunch", root, "출격", 46, Cyan, DarkText);
            Place(launch.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 80f), new Vector2(440f, 100f));

            var back = NewButton("BtnBack", root, "메인 메뉴", 30, ButtonColor, LightText);
            Place(back.rectTransform, BottomLeft, BottomLeft, BottomLeft, new Vector2(50f, 50f), new Vector2(260f, 70f));

            // ---- 스테이지 선택 패널 (평소에는 꺼져 있고, "스테이지 목록"을 누르면 뜬다) ----
            var panel = NewImage("StageSelectPanel", root, PanelColor);
            Place(panel.rectTransform, Center, Center, Center, new Vector2(0f, 20f), new Vector2(1640f, 800f));
            AddOutline(panel.gameObject, OutlineColor);
            panel.gameObject.AddComponent<StageSelectUI>();

            var panelTitle = NewText("Title", panel.transform, "스테이지 선택", 44, LightText, _monoFont, TextAnchor.UpperCenter);
            Place(panelTitle.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -40f), new Vector2(800f, 70f));

            var container = NewRect("CardContainer", panel.transform);
            Place(container, Center, Center, Center, new Vector2(0f, 10f), new Vector2(1560f, 480f));
            var layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // 카드 견본: 꺼 둔 채로 두고, 런타임에 스테이지 개수만큼 복제한다.
            var template = NewImage("CardTemplate", panel.transform, ButtonColor);
            Place(template.rectTransform, Center, Center, Center, Vector2.zero, new Vector2(480f, 460f));
            AddOutline(template.gameObject, OutlineColor);
            template.gameObject.AddComponent<Button>().targetGraphic = template;
            FillCardTexts(template.transform, 0.63f);
            template.gameObject.SetActive(false);

            var close = NewButton("BtnClose", panel.transform, "뒤로", 32, ButtonColor, LightText);
            Place(close.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(-190f, 40f), new Vector2(300f, 76f));
            var panelLaunch = NewButton("BtnLaunch", panel.transform, "출격", 34, Cyan, DarkText);
            Place(panelLaunch.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(190f, 40f), new Vector2(300f, 76f));
            panel.gameObject.SetActive(false);

            // ---- 로비 컨트롤러 (버튼 클릭 소리는 메인 메뉴와 같은 것을 쓴다) ----
            var clickSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/SFX_UIClick.wav");
            var lobbyUI = canvasObject.AddComponent<LobbyUI>();
            SetClickSound(lobbyUI, clickSound);
            SetClickSound(panel.GetComponent<StageSelectUI>(), clickSound);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            Debug.Log($"[Stage] 로비 씬을 만들어 저장했습니다: {ScenePath} ({System.DateTime.Now:HH:mm:ss})");
        }

        // 카드 안에 들어가는 글자 칸 7개를 만든다. scale은 카드 크기에 맞춘 글자 크기 배율(로비 큰 카드 1.0, 작은 카드 0.63).
        private static void FillCardTexts(Transform card, float scale)
        {
            AddCardText(card, "StageNumberText", "STAGE 1", 36, _monoFont, 170f, scale);
            AddCardText(card, "StageNameText", "스테이지 이름", 64, _sansFont, 100f, scale);
            AddCardText(card, "PowerText", "권장 전투력", 34, _sansFont, 30f, scale);
            AddCardText(card, "MyPowerText", "내 전투력", 34, _sansFont, -20f, scale);
            AddCardText(card, "MultiplierText", "적 체력·피해", 30, _sansFont, -75f, scale);
            AddCardText(card, "BestTimeText", "최고 생존", 30, _sansFont, -125f, scale);
            AddCardText(card, "LockText", "잠금", 32, _sansFont, -190f, scale);
        }

        private static void AddCardText(Transform card, string name, string content, int fontSize, Font font, float y, float scale)
        {
            var text = NewText(name, card, content, Mathf.RoundToInt(fontSize * scale), LightText, font, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            Place(text.rectTransform, Center, Center, Center, new Vector2(0f, y * scale), new Vector2(720f * scale, 70f * scale));
        }

        // 컴포넌트의 clickSound 칸에 효과음을 연결한다. (없으면 경고만 남기고 넘어간다)
        private static void SetClickSound(Component component, AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("[Stage] 클릭 효과음(SFX_UIClick.wav)을 찾지 못해 연결하지 않았습니다.");
                return;
            }
            var serialized = new SerializedObject(component);
            serialized.FindProperty("clickSound").objectReferenceValue = clip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == ScenePath) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------- UI 만들기 도우미 ----------------
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text NewText(string name, Transform parent, string content, int fontSize, Color color, Font font, TextAnchor alignment)
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
            return text;
        }

        // 버튼 = 배경 이미지 + Button + 테두리 + 글자. 글자는 버튼 전체를 채운다.
        private static Image NewButton(string name, Transform parent, string label, int fontSize, Color fill, Color textColor)
        {
            var image = NewImage(name, parent, fill);
            image.gameObject.AddComponent<Button>().targetGraphic = image;
            AddOutline(image.gameObject, fill == Cyan ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f) : OutlineColor);

            var text = NewText("Text", image.transform, label, fontSize, textColor, _sansFont, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            return image;
        }

        private static void AddOutline(GameObject go, Color color)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, 1f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
