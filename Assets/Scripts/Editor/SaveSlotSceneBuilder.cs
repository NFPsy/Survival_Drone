using SurvivalDrone.Meta;
using SurvivalDrone.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static SurvivalDrone.EditorTools.SceneUIKit;

namespace SurvivalDrone.EditorTools
{
    // 이미 만들어진 메인 메뉴 씬(Assets/Scenes/MainMenu.unity)에 "저장 슬롯 선택 화면"을 덧붙이는 에디터 전용 도구.
    // 메뉴 SurvivalDrone → Build → Add Save Slot UI To MainMenu Scene
    //
    // 하는 일:
    //  1) Assets/Resources/SaveSlotSettings.asset이 없으면 만든다 (사용 여부는 꺼짐 = 예전처럼 save.json 하나만 쓴다).
    //  2) 메인 메뉴 캔버스 아래에 SlotSelectPanel(슬롯 카드 N장, 뒤로 버튼, 새로 시작 확인창)을 만들고 SaveSlotSelectUI를 붙인다.
    // 이미 있으면 아무것도 하지 않는다. (씬을 처음부터 다시 만들지 않으므로 손으로 고친 부분이 보존된다)
    //
    // 켜는 법: Assets/Resources/SaveSlotSettings.asset 을 선택해서 "Enabled"를 체크한다.
    public static class SaveSlotSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string SettingsFolder = "Assets/Resources";
        private const string SettingsPath = "Assets/Resources/SaveSlotSettings.asset";

        private static readonly Color NewColor = new Color(1f, 0.82f, 0.25f, 1f); // 금색: 새로 시작

        [MenuItem("SurvivalDrone/Build/Add Save Slot UI To MainMenu Scene")]
        public static void AddToExistingScene()
        {
            if (!EditorSafety.CanRunEditModeTool("Add Save Slot UI To MainMenu Scene")) return;

            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath)
            {
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                active = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            }

            var controller = Object.FindFirstObjectByType<MainMenuController>();
            if (controller == null)
            {
                Debug.LogWarning("[Save] 메인 메뉴 씬에서 MainMenuController를 찾지 못해 슬롯 UI를 추가하지 못했습니다.");
                return;
            }
            if (controller.transform.Find("SlotSelectPanel") != null)
            {
                Debug.Log("[Save] 슬롯 선택 화면이 이미 있어서 아무것도 하지 않았습니다.");
                return;
            }

            var settings = LoadOrCreateSettings();
            if (settings == null) return;

            LoadFonts();
            var root = controller.transform;

            // 화면 전체를 덮는 불투명 패널 (뒤의 타이틀 화면이 비쳐 보이지 않게)
            var panel = NewImage("SlotSelectPanel", root, new Color(0.03f, 0.04f, 0.06f, 1f));
            Stretch(panel.rectTransform);

            var title = NewText("TitleText", panel.transform, "슬롯 선택", 52, LightText, MonoFont, TextAnchor.MiddleCenter);
            Place(title.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -40f), new Vector2(900f, 80f));
            var subtitle = NewText("SubtitleText", panel.transform, "이어서 할 슬롯을 고르거나, 비어 있는 슬롯에서 새로 시작하세요  (슬롯은 삭제할 수 없어요)", 28, MutedText, SansFont, TextAnchor.MiddleCenter);
            Place(subtitle.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -125f), new Vector2(1600f, 44f));

            var back = NewButton("BtnBack", panel.transform, "<  뒤로", 30, ButtonColor, LightText);
            Place(back.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(40f, -40f), new Vector2(220f, 64f));

            // 슬롯 카드: 슬롯 개수만큼. 가운데를 기준으로 가로로 나란히 놓는다.
            int count = Mathf.Clamp(settings.SlotCount, 1, 5);
            float spacing = count <= 3 ? 560f : 400f;
            float cardWidth = count <= 3 ? 500f : 370f;
            for (int i = 1; i <= count; i++)
            {
                float x = (i - 1 - (count - 1) * 0.5f) * spacing;
                BuildSlotCard(panel.transform, i, new Vector2(x, -70f), cardWidth);
            }

            // 새로 시작 확인창 (평소엔 꺼져 있음)
            var box = NewPopup("ConfirmPopup", panel.transform, new Vector2(900f, 460f), out var confirmRoot);
            var message = NewText("MessageText", box, "새로 시작할까요?", 32, LightText, SansFont, TextAnchor.MiddleCenter);
            message.lineSpacing = 1.15f;
            Place(message.rectTransform, Center, Center, Center, new Vector2(0f, 60f), new Vector2(820f, 260f));
            var yes = NewButton("BtnYes", box, "새로 시작", 34, Cyan, DarkText);
            Place(yes.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(-170f, 36f), new Vector2(280f, 76f));
            var no = NewButton("BtnNo", box, "취소", 34, ButtonColor, LightText);
            Place(no.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(170f, 36f), new Vector2(280f, 76f));
            confirmRoot.gameObject.SetActive(false);

            var selectUI = panel.gameObject.AddComponent<SaveSlotSelectUI>();
            SetClickSound(selectUI);

            panel.transform.SetAsLastSibling();
            panel.gameObject.SetActive(false); // 슬롯 기능이 켜져 있고 "게임 시작"을 눌렀을 때만 MainMenuController가 열어 준다

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(active);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(active);
            Debug.Log($"[Save] 메인 메뉴 씬에 슬롯 선택 화면(슬롯 {count}개)을 추가하고 저장했습니다. (사용 여부: {(settings.Enabled ? "켜짐" : "꺼짐 — SaveSlotSettings.asset의 Enabled를 체크하면 켜집니다")})");
        }

        // 슬롯 카드 하나: 클릭할 수 있는 큰 버튼 + 제목(슬롯 N) + 상태(비어 있음/저장됨) + 요약 + 아래 안내 글자.
        // 글자 내용은 실행 중에 SaveSlotSelectUI가 저장 파일을 읽어서 채운다.
        private static void BuildSlotCard(Transform parent, int index, Vector2 position, float width)
        {
            var card = NewButton($"Slot_{index}", parent, "", 36, PanelColor, LightText);
            Place(card.rectTransform, Center, Center, Center, position, new Vector2(width, 560f));
            card.GetComponent<Outline>().effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f);
            card.GetComponent<Outline>().effectDistance = new Vector2(2f, 2f);

            // NewButton이 만든 가운데 글자를 카드 아래쪽 안내 글자로 바꿔 쓴다.
            var enter = card.transform.Find("Text").GetComponent<Text>();
            enter.name = "EnterLabel";
            enter.text = "";
            enter.fontSize = 34;
            Place(enter.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 45f), new Vector2(width - 40f, 70f));

            var cardTitle = NewText("TitleText", card.transform, $"슬롯 {index}", 56, LightText, SansFont, TextAnchor.MiddleCenter);
            Place(cardTitle.rectTransform, Center, Center, Center, new Vector2(0f, 205f), new Vector2(width - 40f, 80f));
            var status = NewText("StatusText", card.transform, "", 34, NewColor, SansFont, TextAnchor.MiddleCenter);
            Place(status.rectTransform, Center, Center, Center, new Vector2(0f, 135f), new Vector2(width - 40f, 50f));
            var info = NewText("InfoText", card.transform, "", 28, LightText, SansFont, TextAnchor.MiddleCenter);
            info.horizontalOverflow = HorizontalWrapMode.Wrap; // 긴 문장이 카드 밖으로 넘치지 않게 줄바꿈 (NewText의 기본은 줄바꿈 없음)
            info.lineSpacing = 1.2f;
            Place(info.rectTransform, Center, Center, Center, new Vector2(0f, 5f), new Vector2(width - 50f, 190f));
        }

        // 설정 파일을 불러오고, 없으면 기본값(사용 여부 꺼짐)으로 만든다.
        private static SaveSlotSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SaveSlotSettings>(SettingsPath);
            if (settings != null) return settings;

            if (!AssetDatabase.IsValidFolder(SettingsFolder)) AssetDatabase.CreateFolder("Assets", "Resources");
            settings = ScriptableObject.CreateInstance<SaveSlotSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Save] 슬롯 설정 파일을 새로 만들었습니다: {SettingsPath} (사용 여부 꺼짐)");
            return settings;
        }
    }
}
