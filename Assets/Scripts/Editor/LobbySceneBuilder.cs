using SurvivalDrone.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static SurvivalDrone.EditorTools.SceneUIKit;

namespace SurvivalDrone.EditorTools
{
    // 로비 씬(Assets/Scenes/Lobby.unity)을 코드로 만들어주는 에디터 전용 도구.
    // 메뉴 SurvivalDrone → Build → Create Lobby Scene 을 누르면 로비 씬을 처음부터 다시 만들어 저장한다.
    //
    // 화면 요소를 손으로 하나하나 배치하는 대신 코드로 만들면,
    //  - 배치/색/글자 크기가 코드에 그대로 남아서 나중에 고치거나 되돌리기 쉽고
    //  - 메인 메뉴와 같은 색·폰트 규칙을 한 곳(SceneUIKit)에서 맞출 수 있다.
    // ※ 다시 실행하면 로비 씬이 덮어써진다. 씬을 손으로 고쳤다면 실행 전에 커밋해두자.
    public static class LobbySceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Lobby.unity";

        [MenuItem("SurvivalDrone/Build/Create Lobby Scene")]
        public static void Build()
        {
            var root = CreateCanvasScene(out var scene);
            if (root == null) return;

            // ---- 상단 바: 게임 이름 + 재화 ----
            var topBar = NewRect("TopBar", root);
            Stretch(topBar);
            var gameName = NewText("GameNameText", topBar, "드론 지휘관", 40, Cyan, MonoFont, TextAnchor.MiddleLeft);
            Place(gameName.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(50f, -30f), new Vector2(500f, 60f));
            var creditText = NewText("CreditText", topBar, "크레딧  0", 34, LightText, SansFont, TextAnchor.MiddleRight);
            Place(creditText.rectTransform, TopRight, TopRight, TopRight, new Vector2(-380f, -30f), new Vector2(320f, 60f));
            var coreText = NewText("CoreText", topBar, "코어  0", 34, Cyan, SansFont, TextAnchor.MiddleRight);
            Place(coreText.rectTransform, TopRight, TopRight, TopRight, new Vector2(-40f, -30f), new Vector2(320f, 60f));

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

            // 오른쪽 아래: 다른 화면으로 가는 버튼들 (격납고·상점은 해당 화면을 만들 때 추가)
            var gacha = NewButton("BtnGacha", root, "뽑기", 30, ButtonColor, LightText);
            Place(gacha.rectTransform, BottomRight, BottomRight, BottomRight, new Vector2(-50f, 50f), new Vector2(260f, 70f));

            // ---- 스테이지 선택 패널 (평소에는 꺼져 있고, "스테이지 목록"을 누르면 뜬다) ----
            var panel = NewImage("StageSelectPanel", root, PanelColor);
            Place(panel.rectTransform, Center, Center, Center, new Vector2(0f, 20f), new Vector2(1640f, 800f));
            AddOutline(panel.gameObject, OutlineColor);
            panel.gameObject.AddComponent<StageSelectUI>();

            var panelTitle = NewText("Title", panel.transform, "스테이지 선택", 44, LightText, MonoFont, TextAnchor.UpperCenter);
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

            // ---- 로비 컨트롤러 ----
            var lobbyUI = root.gameObject.AddComponent<LobbyUI>();
            SetClickSound(lobbyUI);
            SetClickSound(panel.GetComponent<StageSelectUI>());

            SaveAndRegister(scene, ScenePath);
            Debug.Log($"[Stage] 로비 씬을 만들어 저장했습니다: {ScenePath} ({System.DateTime.Now:HH:mm:ss})");
        }

        // 카드 안에 들어가는 글자 칸 7개를 만든다. scale은 카드 크기에 맞춘 글자 크기 배율(로비 큰 카드 1.0, 작은 카드 0.63).
        private static void FillCardTexts(Transform card, float scale)
        {
            AddCardText(card, "StageNumberText", "STAGE 1", 36, MonoFont, 170f, scale);
            AddCardText(card, "StageNameText", "스테이지 이름", 64, SansFont, 100f, scale);
            AddCardText(card, "PowerText", "권장 전투력", 34, SansFont, 30f, scale);
            AddCardText(card, "MyPowerText", "내 전투력", 34, SansFont, -20f, scale);
            AddCardText(card, "MultiplierText", "적 체력·피해", 30, SansFont, -75f, scale);
            AddCardText(card, "BestTimeText", "최고 생존", 30, SansFont, -125f, scale);
            AddCardText(card, "LockText", "잠금", 32, SansFont, -190f, scale);
        }

        private static void AddCardText(Transform card, string name, string content, int fontSize, Font font, float y, float scale)
        {
            var text = NewText(name, card, content, Mathf.RoundToInt(fontSize * scale), LightText, font, TextAnchor.MiddleCenter);
            Place(text.rectTransform, Center, Center, Center, new Vector2(0f, y * scale), new Vector2(720f * scale, 70f * scale));
        }
    }
}
