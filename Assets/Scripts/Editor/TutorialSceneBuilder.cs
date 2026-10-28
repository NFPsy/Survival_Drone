using SurvivalDrone.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static SurvivalDrone.EditorTools.SceneUIKit;

namespace SurvivalDrone.EditorTools
{
    // 튜토리얼 씬(Assets/Scenes/Tutorial.unity)을 만들어 주는 에디터 전용 도구.
    // 메뉴 SurvivalDrone → Build → Create Tutorial Scene 을 누르면 InGame 씬을 복사해서 튜토리얼 씬을 새로 만들고,
    // 그 위에 안내 상자·건너뛰기 버튼·완료 창과 진행 스크립트(TutorialController)를 얹어 저장한다.
    // (이미 있던 Tutorial 씬은 지우고 다시 만든다. 손으로 고쳤다면 먼저 커밋해 두자.)
    //
    // 씬을 InGame 복사본으로 만드는 이유: 플레이어·드론·적·보스·HUD를 그대로 쓰려는 것이다.
    // 복사본에서 InGame과 달라지는 동작(스포너 정지, 저장 기록 건너뛰기 등)은 모두 코드가 씬 이름(GameManager.IsTutorial)으로 판단한다.
    //
    // 화면 배치 (1920x1080 기준):
    //   아래 가운데: 안내 상자 (단계 이름 / 해야 할 일 / 힌트 / 진행 상황)
    //   아래 오른쪽: 건너뛰기 버튼
    //   가운데 팝업: 튜토리얼 완료 창 (평소엔 꺼 둠. 메인메뉴로 / 다시 하기)
    public static class TutorialSceneBuilder
    {
        private const string SourcePath = "Assets/Scenes/InGame.unity";
        private const string ScenePath = "Assets/Scenes/Tutorial.unity";

        [MenuItem("SurvivalDrone/Build/Create Tutorial Scene")]
        public static void Build()
        {
            if (!EditorSafety.CanRunEditModeTool("튜토리얼 씬 생성")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            LoadFonts();
            AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SourcePath, ScenePath))
            {
                Debug.LogError($"[Tutorial] {SourcePath}를 {ScenePath}로 복사하지 못했습니다.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform canvas = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Canvas") canvas = go.transform;
            if (canvas == null)
            {
                Debug.LogError("[Tutorial] 복사한 씬에서 Canvas를 찾지 못했습니다.");
                return;
            }

            var root = NewRect("TutorialRoot", canvas);
            Stretch(root);

            // ---- 안내 상자 ----
            var guide = NewImage("Guide", root, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.82f));
            guide.raycastTarget = false;
            Place(guide.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 40f), new Vector2(1300f, 230f));
            AddOutline(guide.gameObject, OutlineColor);

            var step = NewText("StepText", guide.transform, "튜토리얼 1/3  ·  이동", 28, Cyan, MonoFont, TextAnchor.MiddleCenter);
            Place(step.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -14f), new Vector2(1240f, 40f));
            var message = NewText("MessageText", guide.transform, "WASD 키로 움직이세요", 46, LightText, SansFont, TextAnchor.MiddleCenter);
            Place(message.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -56f), new Vector2(1240f, 64f));
            var hint = NewText("HintText", guide.transform, "하늘색 원 안으로 들어가 보세요", 28, MutedText, SansFont, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -126f), new Vector2(1240f, 40f));
            var progress = NewText("ProgressText", guide.transform, "0 / 3", 30, Cyan, SansFont, TextAnchor.MiddleCenter);
            Place(progress.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -170f), new Vector2(1240f, 40f));

            // ---- 건너뛰기 ----
            var skip = NewButton("BtnSkip", root, "건너뛰기  >", 28, ButtonColor, LightText);
            Place(skip.rectTransform, BottomRight, BottomRight, BottomRight, new Vector2(-40f, 40f), new Vector2(240f, 64f));

            // ---- 완료 창 ----
            var completeBox = NewPopup("CompletePanel", root, new Vector2(900f, 440f), out var completeRoot);
            var completeTitle = NewText("TitleText", completeBox, "튜토리얼 완료!", 56, LightText, MonoFont, TextAnchor.MiddleCenter);
            Place(completeTitle.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -60f), new Vector2(820f, 80f));
            var completeMessage = NewText("MessageText", completeBox, "이제 게임을 시작해서 드론을 모아 보세요", 32, MutedText, SansFont, TextAnchor.MiddleCenter);
            Place(completeMessage.rectTransform, Center, Center, Center, new Vector2(0f, 20f), new Vector2(820f, 50f));
            var toMenu = NewButton("BtnMainMenu", completeBox, "메인메뉴로", 34, Cyan, DarkText);
            Place(toMenu.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(-200f, 50f), new Vector2(340f, 84f));
            var retry = NewButton("BtnRetry", completeBox, "다시 하기", 34, ButtonColor, LightText);
            Place(retry.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(200f, 50f), new Vector2(340f, 84f));
            completeRoot.gameObject.SetActive(false);

            // ---- 진행 스크립트 ----
            var controller = root.gameObject.AddComponent<TutorialController>();
            SetClickSound(controller);

            SaveAndRegister(scene, ScenePath);
            Debug.Log($"[Tutorial] 튜토리얼 씬을 만들어 저장했습니다: {ScenePath} ({System.DateTime.Now:HH:mm:ss})");
        }
    }
}
