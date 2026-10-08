using SurvivalDrone.Meta;
using SurvivalDrone.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static SurvivalDrone.EditorTools.SceneUIKit;

namespace SurvivalDrone.EditorTools
{
    // 이미 만들어진 뽑기 씬(Assets/Scenes/Gacha.unity)에 "락온 뽑기" 화면을 덧붙이는 에디터 전용 도구.
    // 메뉴 SurvivalDrone → Build → Add Lock-On UI To Gacha Scene
    //
    // 하는 일:
    //  1) Assets/Data/Meta/LockOnTable.asset이 없으면 만든다 (사용 여부는 꺼짐 = CBT 1차에는 버튼이 안 보인다).
    //  2) 뽑기 화면 왼쪽 위에 "락온 뽑기" 버튼(BtnLockOn)을 만든다. (사용 여부가 꺼져 있으면 실행 중에 숨겨진다)
    //  3) 화면 전체를 덮는 LockOnPanel(카드 3장, 잠금·재뽑기·확정 버튼, 확률 팝업, 확인창)을 만들고 LockOnUI를 붙인다.
    // 이미 있으면 아무것도 하지 않는다. (씬을 처음부터 다시 만들지 않으므로 손으로 고친 부분이 보존된다)
    //
    // 켜는 법: Assets/Data/Meta/LockOnTable.asset 을 선택해서 "Enabled"를 체크한다. (2차 테스트부터)
    public static class LockOnSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Gacha.unity";
        private const string TablePath = "Assets/Data/Meta/LockOnTable.asset";

        // 락온(잠금)을 나타내는 주황색. 공통 UI 색 규칙: 주황 = 선택한 위험·잠금 조건.
        private static readonly Color LockColor = new Color(1f, 0.62f, 0.2f, 1f);

        [MenuItem("SurvivalDrone/Build/Add Lock-On UI To Gacha Scene")]
        public static void AddToExistingScene()
        {
            if (!EditorSafety.CanRunEditModeTool("Add Lock-On UI To Gacha Scene")) return;

            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath)
            {
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                active = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            }

            var gachaUI = Object.FindFirstObjectByType<GachaUI>();
            if (gachaUI == null)
            {
                Debug.LogWarning("[LockOn] 뽑기 씬에서 GachaUI를 찾지 못해 락온 UI를 추가하지 못했습니다.");
                return;
            }
            if (gachaUI.transform.Find("LockOnPanel") != null)
            {
                Debug.Log("[LockOn] 락온 UI가 이미 있어서 아무것도 하지 않았습니다.");
                return;
            }

            var table = LoadOrCreateTable();
            if (table == null) return;

            LoadFonts();
            var root = gachaUI.transform;

            // ---- 뽑기 화면의 "락온 뽑기" 버튼 (뒤로 버튼 아래) ----
            var open = NewButton("BtnLockOn", root, "락온 뽑기", 30, ButtonColor, LockColor);
            Place(open.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(40f, -112f), new Vector2(300f, 64f));
            // 확률·코어 부족 팝업이 이 버튼 위를 덮도록 팝업 바로 앞에 끼워 넣는다.
            var popup = root.Find("ProbabilityPopup");
            if (popup != null) open.transform.SetSiblingIndex(popup.GetSiblingIndex());

            // ---- 락온 화면 (화면 전체를 덮는 불투명 패널) ----
            var panel = NewImage("LockOnPanel", root, new Color(0.03f, 0.04f, 0.06f, 1f));
            Stretch(panel.rectTransform);

            var title = NewText("TitleText", panel.transform, "락온 뽑기", 44, LightText, MonoFont, TextAnchor.MiddleCenter);
            Place(title.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -30f), new Vector2(800f, 70f));
            var subtitle = NewText("SubtitleText", panel.transform, "", 26, MutedText, SansFont, TextAnchor.MiddleCenter);
            Place(subtitle.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -100f), new Vector2(1500f, 40f));

            var back = NewButton("BtnBack", panel.transform, "<  뒤로", 30, ButtonColor, LightText);
            Place(back.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(40f, -30f), new Vector2(220f, 64f));
            var coreText = NewText("CoreText", panel.transform, "코어  0", 36, Cyan, SansFont, TextAnchor.MiddleRight);
            Place(coreText.rectTransform, TopRight, TopRight, TopRight, new Vector2(-40f, -30f), new Vector2(420f, 60f));
            var rates = NewButton("BtnRates", panel.transform, "확률·규칙 보기", 28, ButtonColor, LightText);
            Place(rates.rectTransform, TopRight, TopRight, TopRight, new Vector2(-40f, -100f), new Vector2(300f, 60f));

            // ---- 칸 카드 3장 ----
            var container = NewRect("SlotContainer", panel.transform);
            Place(container, Center, Center, Center, new Vector2(0f, 40f), new Vector2(1300f, 470f));
            float[] xs = { -430f, 0f, 430f };
            for (int i = 0; i < xs.Length; i++) BuildSlot(container, i, xs[i]);

            // ---- 안내 / 메시지 ----
            var info = NewText("InfoText", panel.transform, "", 28, MutedText, SansFont, TextAnchor.MiddleCenter);
            Place(info.rectTransform, Center, Center, Center, new Vector2(0f, -225f), new Vector2(1600f, 44f));
            var message = NewText("MessageText", panel.transform, "", 30, new Color(1f, 0.35f, 0.35f), SansFont, TextAnchor.MiddleCenter);
            Place(message.rectTransform, Center, Center, Center, new Vector2(0f, -275f), new Vector2(1000f, 44f));
            message.gameObject.SetActive(false);

            // ---- 아래 버튼 (단계에 따라 일부만 보인다) ----
            var start = NewButton("BtnStart", panel.transform, "락온 시작", 36, Cyan, DarkText);
            Place(start.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 40f), new Vector2(560f, 110f));
            var reroll = NewButton("BtnReroll", panel.transform, "재뽑기", 34, ButtonColor, LightText);
            Place(reroll.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(-300f, 40f), new Vector2(520f, 110f));
            var confirm = NewButton("BtnConfirm", panel.transform, "확정", 34, Cyan, DarkText);
            Place(confirm.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(300f, 40f), new Vector2(520f, 110f));
            var again = NewButton("BtnAgain", panel.transform, "한 번 더", 34, Cyan, DarkText);
            Place(again.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(-300f, 40f), new Vector2(520f, 110f));
            var close = NewButton("BtnClose", panel.transform, "나가기", 34, ButtonColor, LightText);
            Place(close.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(300f, 40f), new Vector2(520f, 110f));
            foreach (var b in new[] { start, reroll, confirm, again, close }) b.gameObject.SetActive(false);

            // ---- 시뮬레이터 (전환 버튼 / 안내 글자 / 누적 초기화 버튼) ----
            BuildSimulatorUI(panel.transform);

            // ---- 팝업 두 개 (평소엔 꺼져 있음): 확률·규칙 공개 / 확인창 ----
            var ratesBox = NewPopup("RatesPopup", panel.transform, new Vector2(1000f, 800f), out var ratesRoot);
            var ratesTitle = NewText("TitleText", ratesBox, "락온 뽑기 확률·규칙", 44, LightText, MonoFont, TextAnchor.MiddleCenter);
            Place(ratesTitle.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -40f), new Vector2(900f, 70f));
            var ratesText = NewText("RatesText", ratesBox, "SSR", 44, LightText, SansFont, TextAnchor.MiddleLeft);
            ratesText.supportRichText = true;
            Place(ratesText.rectTransform, Center, Center, Center, new Vector2(0f, 190f), new Vector2(480f, 260f));
            var rulesText = NewText("RulesText", ratesBox, "규칙", 26, MutedText, SansFont, TextAnchor.UpperLeft);
            rulesText.lineSpacing = 1.2f;
            rulesText.horizontalOverflow = HorizontalWrapMode.Wrap; // 긴 문장이 칸 밖으로 넘치지 않게 줄바꿈 (NewText의 기본은 줄바꿈 없음)
            Place(rulesText.rectTransform, Center, Center, Center, new Vector2(0f, -120f), new Vector2(900f, 320f));
            var ratesClose = NewButton("BtnClose", ratesBox, "닫기", 32, ButtonColor, LightText);
            Place(ratesClose.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 40f), new Vector2(280f, 72f));
            ratesRoot.gameObject.SetActive(false);

            var confirmBox = NewPopup("ConfirmPopup", panel.transform, new Vector2(900f, 480f), out var confirmRoot);
            var confirmMessage = NewText("MessageText", confirmBox, "확정할까요?", 32, LightText, SansFont, TextAnchor.MiddleCenter);
            confirmMessage.lineSpacing = 1.15f;
            Place(confirmMessage.rectTransform, Center, Center, Center, new Vector2(0f, 60f), new Vector2(820f, 280f));
            var yes = NewButton("BtnYes", confirmBox, "예", 34, Cyan, DarkText);
            Place(yes.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(-170f, 36f), new Vector2(280f, 76f));
            var no = NewButton("BtnNo", confirmBox, "아니오", 34, ButtonColor, LightText);
            Place(no.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(170f, 36f), new Vector2(280f, 76f));
            confirmRoot.gameObject.SetActive(false);

            // ---- 컴포넌트 연결: 수치표(LockOnTable)와 클릭 효과음 ----
            var lockOnUI = panel.gameObject.AddComponent<LockOnUI>();
            var serialized = new SerializedObject(lockOnUI);
            serialized.FindProperty("_table").objectReferenceValue = table;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetClickSound(lockOnUI);

            // 맨 앞에 두어서 뽑기 화면의 다른 요소·팝업보다 위에 보이게 한다.
            panel.transform.SetAsLastSibling();
            panel.gameObject.SetActive(false);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(active);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(active);
            Debug.Log($"[LockOn] 뽑기 씬에 락온 뽑기 UI를 추가하고 저장했습니다. (사용 여부: {(table.Enabled ? "켜짐" : "꺼짐 — LockOnTable.asset의 Enabled를 체크하면 버튼이 보입니다")})");
        }

        // 시뮬레이터 안내 글자 색 (일반 뽑기 시뮬레이터와 같은 분홍색).
        private static readonly Color SimColor = new Color(1f, 0.45f, 0.75f, 1f);

        // 락온 시뮬레이터 UI 3개를 만든다:
        //   BtnSim(왼쪽 위 전환 버튼), SimInfoText(켜졌을 때만 보이는 안내·누적 글자), BtnSimReset(켜졌을 때만 보이는 누적 초기화 버튼).
        // LockOnUI가 이 이름으로 찾아서 연결하므로 이름을 바꾸면 안 된다.
        // 팝업이 이미 있으면 그 "앞"에 끼워 넣어서, 팝업이 이 버튼들을 덮도록 한다.
        private static void BuildSimulatorUI(Transform panel)
        {
            var toggle = NewButton("BtnSim", panel, "시뮬레이터: 꺼짐", 28, ButtonColor, LightText);
            Place(toggle.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(40f, -110f), new Vector2(340f, 60f));

            var info = NewText("SimInfoText", panel, "SIMULATION", 26, SimColor, SansFont, TextAnchor.MiddleCenter);
            Place(info.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -150f), new Vector2(1750f, 40f));
            info.gameObject.SetActive(false);

            var reset = NewButton("BtnSimReset", panel, "누적 초기화", 28, ButtonColor, LightText);
            Place(reset.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 175f), new Vector2(340f, 56f));
            reset.gameObject.SetActive(false);

            var popup = panel.Find("RatesPopup");
            if (popup != null)
            {
                toggle.transform.SetSiblingIndex(popup.GetSiblingIndex());
                info.transform.SetSiblingIndex(popup.GetSiblingIndex());
                reset.transform.SetSiblingIndex(popup.GetSiblingIndex());
            }
        }

        // 이미 만들어져 저장된 락온 화면에 시뮬레이터 UI만 덧붙인다. (화면을 처음부터 다시 만들지 않으므로 손으로 고친 부분이 보존된다)
        // 메뉴 SurvivalDrone → Build → Add Lock-On Simulator UI To Gacha Scene
        [MenuItem("SurvivalDrone/Build/Add Lock-On Simulator UI To Gacha Scene")]
        public static void AddSimulatorToExistingScene()
        {
            if (!EditorSafety.CanRunEditModeTool("Add Lock-On Simulator UI To Gacha Scene")) return;

            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath)
            {
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                active = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            }

            var lockOnUI = Object.FindFirstObjectByType<LockOnUI>(FindObjectsInactive.Include);
            if (lockOnUI == null)
            {
                Debug.LogWarning("[LockOn] 뽑기 씬에서 LockOnUI를 찾지 못해 시뮬레이터 UI를 추가하지 못했습니다. 먼저 'Add Lock-On UI To Gacha Scene'을 실행해주세요.");
                return;
            }
            if (lockOnUI.transform.Find("BtnSim") != null)
            {
                Debug.Log("[LockOn] 시뮬레이터 UI가 이미 있어서 아무것도 하지 않았습니다.");
                return;
            }

            LoadFonts();
            BuildSimulatorUI(lockOnUI.transform);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(active);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(active);
            Debug.Log("[LockOn] 락온 화면에 시뮬레이터 UI를 추가하고 저장했습니다.");
        }

        // 칸 카드 하나: 뒷면("?") / 앞면(등급·드론 이름·태그) / 잠금 버튼.
        private static void BuildSlot(Transform container, int index, float x)
        {
            var card = NewImage($"Slot_{index}", container, ButtonColor);
            Place(card.rectTransform, Center, Center, Center, new Vector2(x, 0f), new Vector2(380f, 470f));
            AddOutline(card.gameObject, OutlineColor);

            var cardBack = NewText("Back", card.transform, "?", 120, MutedText, MonoFont, TextAnchor.MiddleCenter);
            Stretch(cardBack.rectTransform);

            var face = NewRect("Face", card.transform);
            Stretch(face);
            var rarity = NewText("RarityText", face, "SR", 80, RarityColors.Get(GachaRarity.SR), MonoFont, TextAnchor.MiddleCenter);
            Place(rarity.rectTransform, Center, Center, Center, new Vector2(0f, 110f), new Vector2(340f, 100f));
            var droneName = NewText("DroneNameText", face, "근접 드론", 42, LightText, SansFont, TextAnchor.MiddleCenter);
            Place(droneName.rectTransform, Center, Center, Center, new Vector2(0f, 30f), new Vector2(340f, 56f));
            var tag = NewText("TagText", face, "", 28, MutedText, SansFont, TextAnchor.MiddleCenter);
            tag.lineSpacing = 1.1f;
            Place(tag.rectTransform, Center, Center, Center, new Vector2(0f, -45f), new Vector2(340f, 80f));

            var lockButton = NewButton("BtnLock", card.transform, "잠금", 32, ButtonColor, LockColor);
            Place(lockButton.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 28f), new Vector2(300f, 72f));
            lockButton.gameObject.SetActive(false);
        }

        // 수치표 파일을 불러오고, 없으면 기본값(사용 여부 꺼짐)으로 만든다.
        private static LockOnTable LoadOrCreateTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<LockOnTable>(TablePath);
            if (table != null) return table;

            table = ScriptableObject.CreateInstance<LockOnTable>();
            AssetDatabase.CreateAsset(table, TablePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LockOn] 수치표를 새로 만들었습니다: {TablePath} (사용 여부 꺼짐)");
            return table;
        }
    }
}
