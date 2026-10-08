using SurvivalDrone.Meta;
using SurvivalDrone.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static SurvivalDrone.EditorTools.SceneUIKit;

namespace SurvivalDrone.EditorTools
{
    // 이미 만들어진 뽑기 씬(Assets/Scenes/Gacha.unity)에 "뽑기 선택 화면"을 덧붙이는 에디터 전용 도구.
    // 메뉴 SurvivalDrone → Build → Add Gacha Select UI To Gacha Scene
    //
    // 만드는 것: 화면 전체를 덮는 GachaSelectPanel — 카드 두 장(일반 뽑기 / 락온 뽑기)과 뒤로 버튼, 코어 잔액.
    // 락온 뽑기가 켜져 있을 때(LockOnTable의 Enabled) GachaUI가 이 화면을 먼저 열어 준다. 꺼져 있으면 열지 않는다.
    // 이미 있으면 아무것도 하지 않는다. (씬을 처음부터 다시 만들지 않으므로 손으로 고친 부분이 보존된다)
    // 먼저 "Add Lock-On UI To Gacha Scene"을 실행해서 LockOnTable.asset과 락온 화면이 있어야 한다.
    public static class GachaSelectSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Gacha.unity";
        private const string TablePath = "Assets/Data/Meta/LockOnTable.asset";

        // 락온 카드의 강조색(주황 = 잠금). 일반 뽑기 카드는 시안.
        private static readonly Color LockColor = new Color(1f, 0.62f, 0.2f, 1f);

        [MenuItem("SurvivalDrone/Build/Add Gacha Select UI To Gacha Scene")]
        public static void AddToExistingScene()
        {
            if (!EditorSafety.CanRunEditModeTool("Add Gacha Select UI To Gacha Scene")) return;

            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath)
            {
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                active = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            }

            var gachaUI = Object.FindFirstObjectByType<GachaUI>();
            if (gachaUI == null)
            {
                Debug.LogWarning("[Gacha] 뽑기 씬에서 GachaUI를 찾지 못해 선택 화면을 추가하지 못했습니다.");
                return;
            }
            if (gachaUI.transform.Find("GachaSelectPanel") != null)
            {
                Debug.Log("[Gacha] 뽑기 선택 화면이 이미 있어서 아무것도 하지 않았습니다.");
                return;
            }

            var table = AssetDatabase.LoadAssetAtPath<LockOnTable>(TablePath);
            if (table == null)
            {
                Debug.LogWarning("[Gacha] LockOnTable.asset이 없습니다. 먼저 'Add Lock-On UI To Gacha Scene'을 실행해주세요.");
                return;
            }

            LoadFonts();
            var root = gachaUI.transform;

            var panel = NewImage("GachaSelectPanel", root, new Color(0.03f, 0.04f, 0.06f, 1f)); // 완전 불투명: 뒤의 뽑기 화면이 비쳐 보이지 않게
            Stretch(panel.rectTransform);

            var title = NewText("TitleText", panel.transform, "뽑기 선택", 48, LightText, MonoFont, TextAnchor.MiddleCenter);
            Place(title.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -30f), new Vector2(800f, 70f));
            var subtitle = NewText("SubtitleText", panel.transform, "어떤 뽑기를 할까요?", 28, MutedText, SansFont, TextAnchor.MiddleCenter);
            Place(subtitle.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -100f), new Vector2(800f, 44f));

            var back = NewButton("BtnBack", panel.transform, "<  뒤로", 30, ButtonColor, LightText);
            Place(back.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(40f, -30f), new Vector2(220f, 64f));
            var coreText = NewText("CoreText", panel.transform, "코어  0", 36, Cyan, SansFont, TextAnchor.MiddleRight);
            Place(coreText.rectTransform, TopRight, TopRight, TopRight, new Vector2(-40f, -30f), new Vector2(420f, 60f));

            // 두 카드: 왼쪽 일반 뽑기(시안), 오른쪽 락온 뽑기(주황, NEW 뱃지)
            BuildCard(panel.transform, "NormalCard", new Vector2(-440f, -50f), "일반 뽑기", Cyan, MutedText, "일반 뽑기 하러 가기  >", false);
            BuildCard(panel.transform, "LockOnCard", new Vector2(440f, -50f), "락온 뽑기", LockColor, LockColor, "락온 뽑기 하러 가기  >", true);

            // 컴포넌트 연결: 락온 수치표와 클릭 효과음
            var selectUI = panel.gameObject.AddComponent<GachaSelectUI>();
            var serialized = new SerializedObject(selectUI);
            serialized.FindProperty("_lockOnTable").objectReferenceValue = table;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetClickSound(selectUI);

            // 락온 화면이 이미 있으면 그 바로 아래에 끼워 넣는다: 락온 화면이 열리면 선택 화면 위로 덮이고, 닫으면 선택 화면이 다시 보인다.
            var lockOnPanel = root.Find("LockOnPanel");
            if (lockOnPanel != null) panel.transform.SetSiblingIndex(lockOnPanel.GetSiblingIndex());
            else panel.transform.SetAsLastSibling();
            panel.gameObject.SetActive(false); // 켜져 있을 때만 GachaUI가 실행 중에 열어 준다

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(active);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(active);
            Debug.Log("[Gacha] 뽑기 씬에 선택 화면(GachaSelectPanel)을 추가하고 저장했습니다.");
        }

        // 카드 한 장: 클릭할 수 있는 큰 버튼 + 제목 + 설명 + 정보 세 줄 + 아래 안내 글자(+ NEW 뱃지).
        // 글자 내용(설명·정보)은 실행 중에 GachaSelectUI가 수치표 값으로 채운다.
        private static void BuildCard(Transform parent, string name, Vector2 position, string titleText, Color accent, Color infoColor, string enterText, bool withBadge)
        {
            var card = NewButton(name, parent, "", 36, PanelColor, LightText);
            Place(card.rectTransform, Center, Center, Center, position, new Vector2(820f, 700f));
            card.GetComponent<Outline>().effectColor = new Color(accent.r, accent.g, accent.b, 0.9f);
            card.GetComponent<Outline>().effectDistance = new Vector2(2f, 2f);

            // NewButton이 만든 가운데 글자를 카드 아래쪽 안내 글자로 바꿔 쓴다.
            var enter = card.transform.Find("Text").GetComponent<Text>();
            enter.name = "EnterLabel";
            enter.text = enterText;
            enter.color = accent;
            enter.fontSize = 34;
            Place(enter.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 45f), new Vector2(760f, 70f));

            var cardTitle = NewText("TitleText", card.transform, titleText, 64, accent, SansFont, TextAnchor.MiddleCenter);
            Place(cardTitle.rectTransform, Center, Center, Center, new Vector2(0f, 255f), new Vector2(760f, 90f));
            var desc = NewText("DescText", card.transform, "", 32, LightText, SansFont, TextAnchor.MiddleCenter);
            Place(desc.rectTransform, Center, Center, Center, new Vector2(0f, 170f), new Vector2(760f, 50f));

            var info1 = NewText("Info1Text", card.transform, "", 28, LightText, SansFont, TextAnchor.MiddleCenter);
            info1.horizontalOverflow = HorizontalWrapMode.Wrap; // 긴 문장이 카드 밖으로 넘치지 않게 줄바꿈 (NewText의 기본은 줄바꿈 없음)
            info1.lineSpacing = 1.15f;
            Place(info1.rectTransform, Center, Center, Center, new Vector2(0f, 60f), new Vector2(680f, 110f));
            var info2 = NewText("Info2Text", card.transform, "", 28, LightText, SansFont, TextAnchor.MiddleCenter);
            info2.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(info2.rectTransform, Center, Center, Center, new Vector2(0f, -45f), new Vector2(680f, 50f));
            var info3 = NewText("Info3Text", card.transform, "", 28, infoColor, SansFont, TextAnchor.MiddleCenter);
            info3.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(info3.rectTransform, Center, Center, Center, new Vector2(0f, -115f), new Vector2(680f, 80f));

            if (withBadge)
            {
                var badge = NewImage("BadgeBack", card.transform, accent);
                badge.raycastTarget = false; // 뱃지가 카드 클릭을 가로채지 않게 한다
                Place(badge.rectTransform, TopRight, TopRight, TopRight, new Vector2(-28f, -28f), new Vector2(120f, 48f));
                var badgeText = NewText("BadgeText", badge.transform, "NEW", 28, DarkText, SansFont, TextAnchor.MiddleCenter);
                Stretch(badgeText.rectTransform);
            }
        }
    }
}
