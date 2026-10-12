using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalDrone.EditorTools
{
    // itch.io 스크린샷을 "실제 플레이 화면"에서 뽑기 위한 에디터 도구. (플레이 모드에서만 동작)
    //
    // 두 가지 일을 한다.
    //  1) CapturePlayMode: 지금 게임 화면(3D + HUD + 데미지 숫자)을 1920x1080 PNG로 저장한다.
    //     - 게임 화면 창(Game view)을 직접 캡처하면 창이 가려져 있을 때 안 그려질 수 있어서,
    //       메인 카메라를 복제한 임시 카메라로 따로 그려서 저장한다.
    //     - HUD 같은 "화면 위에 바로 그리는 캔버스"는 카메라로 못 찍으므로, 찍는 순간만 카메라 기준 캔버스로 바꿨다가 되돌린다.
    //  2) AutoPlayStart/Stop: 사람이 없어도 판이 진행되게 해 준다. 플레이어를 죽지 않게 하고(피해 0),
    //     레벨업 선택 화면이 뜨면 "신규 드론" 선택지를 우선으로 골라서 드론이 여러 종류 모이게 한다.
    //     (화면이 풍성한 장면을 찍으려고, 연출용으로만 쓴다. 게임 규칙은 바꾸지 않는다)
    public static class StoreShotTool
    {
        private static bool _autoPlay;

        public static void CapturePlayMode(string outPath, int width = 1920, int height = 1080)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[StoreShotTool] 플레이 모드에서만 쓸 수 있습니다."); return; }
            var main = Camera.main;
            if (main == null) { Debug.LogWarning("[StoreShotTool] 메인 카메라를 찾지 못했습니다."); return; }

            // 메인 카메라를 복제하되, 움직임·소리 같은 스크립트는 모두 빼고 URP 설정(후처리 등)만 남긴다.
            var clone = UnityEngine.Object.Instantiate(main.gameObject);
            clone.name = "TempStoreShotCamera";
            clone.tag = "Untagged";
            foreach (var behaviour in clone.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null) continue;
                if (behaviour.GetType().Name == "UniversalAdditionalCameraData") continue;
                UnityEngine.Object.DestroyImmediate(behaviour);
            }
            var listener = clone.GetComponent<AudioListener>();
            if (listener != null) UnityEngine.Object.DestroyImmediate(listener);

            var cam = clone.GetComponent<Camera>();
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;

            // 화면 위에 바로 그리는 캔버스를 잠깐 카메라 기준으로 바꾼다.
            var changed = new System.Collections.Generic.List<Canvas>();
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.5f;
                changed.Add(canvas);
            }
            Canvas.ForceUpdateCanvases();

            cam.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllBytes(outPath, tex.EncodeToPNG());

            // 원래대로 되돌린다.
            foreach (var canvas in changed)
            {
                if (canvas == null) continue;
                canvas.worldCamera = null;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            UnityEngine.Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(clone);
        }

        // ───────────────────────── 로비·뽑기 결과 화면 (플레이 모드 아님) ─────────────────────────
        // 로비와 뽑기 결과는 저장 파일을 건드리지 않으려고 게임을 실행하지 않고 씬만 읽어서 찍는다. (UiScreenshotTool과 같은 방식)
        // 다만 UiScreenshotTool의 예시 내용은 "스테이지 이름" 같은 빈 칸이 남아 있어서, 스토어용으로 실제 같은 내용을 채운다.
        public static void CaptureStoreUiShots(string folder)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[StoreShotTool] 플레이 모드 중에는 쓸 수 없습니다."); return; }
            Directory.CreateDirectory(folder);
            UiScreenshotTool.CaptureScene("Assets/Scenes/Lobby.unity", folder + "/ui_lobby.png", 1920, 1080, PrepareLobbyForStore);
            UiScreenshotTool.CaptureScene("Assets/Scenes/Gacha.unity", folder + "/ui_gacha_result.png", 1920, 1080, PrepareGachaResultForStore);
        }

        private static Transform FindDeep(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            return null;
        }

        private static void SetText(Transform root, string path, string value, Color? color = null)
        {
            var t = root != null ? root.Find(path) : null;
            var text = t != null ? t.GetComponent<Text>() : null;
            if (text == null) return;
            text.text = value;
            if (color.HasValue) text.color = color.Value;
        }

        private static void PrepareLobbyForStore(UnityEngine.SceneManagement.Scene scene)
        {
            var canvas = FindDeep(scene, "Canvas");
            SetText(canvas, "TopBar/CreditText", "크레딧  1,250");
            SetText(canvas, "TopBar/CoreText", "코어  2,700");
            SetText(canvas, "StageCard/StageNumberText", "STAGE 1");
            SetText(canvas, "StageCard/StageNameText", "외곽 순찰로");
            SetText(canvas, "StageCard/PowerText", "권장 전투력  100");
            SetText(canvas, "StageCard/MyPowerText", "내 전투력  214", new Color(0.35f, 0.9f, 0.5f));
            SetText(canvas, "StageCard/MultiplierText", "적 체력·피해  x1.0");
            SetText(canvas, "StageCard/BestTimeText", "최고 생존  05:32");
            var lockText = canvas != null ? canvas.Find("StageCard/LockText") : null;
            if (lockText != null) lockText.gameObject.SetActive(false);
        }

        private static void PrepareGachaResultForStore(UnityEngine.SceneManagement.Scene scene)
        {
            var panel = FindDeep(scene, "ResultPanel");
            if (panel == null) return;
            panel.gameObject.SetActive(true);

            var template = panel.Find("CardTemplate");
            var container = panel.Find("CardContainer");
            if (template == null || container == null) return;
            var grid = container.GetComponent<GridLayoutGroup>();
            if (grid != null) grid.constraintCount = 5;

            SurvivalDrone.Drones.DroneType[] drones =
            {
                SurvivalDrone.Drones.DroneType.Melee, SurvivalDrone.Drones.DroneType.Sniper, SurvivalDrone.Drones.DroneType.Collector,
                SurvivalDrone.Drones.DroneType.Explosion, SurvivalDrone.Drones.DroneType.Heal,
                SurvivalDrone.Drones.DroneType.Sniper, SurvivalDrone.Drones.DroneType.Melee, SurvivalDrone.Drones.DroneType.Heal,
                SurvivalDrone.Drones.DroneType.Explosion, SurvivalDrone.Drones.DroneType.Collector,
            };
            string[] rarities = { "N", "R", "N", "SR", "N", "R", "N", "N", "SSR", "R" };
            string[] tags = { "중복  조각 +3", "NEW", "중복  조각 +3", "승급  R → SR", "중복  조각 +3", "중복  조각 +10", "중복  조각 +3", "NEW", "NEW", "중복  조각 +10" };
            Color n = new Color(0.6f, 0.65f, 0.7f), r = new Color(0.4f, 0.7f, 1f), sr = new Color(0.72f, 0.45f, 1f), ssr = new Color(1f, 0.82f, 0.3f);
            Color[] colors = { n, r, n, sr, n, r, n, n, ssr, r };

            for (int i = 0; i < drones.Length; i++)
            {
                var card = UnityEngine.Object.Instantiate(template.gameObject, container);
                card.name = "Card_" + (i + 1);
                card.SetActive(true);
                card.transform.Find("Back").gameObject.SetActive(false);
                card.transform.Find("Face").gameObject.SetActive(true);

                var rarityText = card.transform.Find("Face/RarityText").GetComponent<Text>();
                rarityText.text = rarities[i];
                rarityText.color = colors[i];
                card.transform.Find("Face/DroneNameText").GetComponent<Text>().text = SurvivalDrone.UI.DroneNames.Get(drones[i]);
                var tag = card.transform.Find("Face/TagText").GetComponent<Text>();
                tag.text = tags[i];
                tag.color = tags[i].StartsWith("중복") ? new Color(0.588f, 0.706f, 0.714f) : new Color(0.31f, 0.847f, 0.91f);
                var icon = card.transform.Find("Face/DroneNameText/Icon");
                if (icon != null) SurvivalDrone.UI.UiIcons.SetDroneIcon(icon.GetComponent<Image>(), drones[i]);

                // 카드 테두리: 등급 색 (SSR은 두껍게)
                var outline = card.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = colors[i];
                    outline.effectDistance = rarities[i] == "SSR" ? new Vector2(4f, 4f) : new Vector2(2f, 2f);
                }
            }

            // 다 열린 뒤의 화면: "모두 개봉" 버튼은 숨기고 확인/다시 뽑기를 보여준다.
            var skip = panel.Find("BtnSkip");
            if (skip != null) skip.gameObject.SetActive(false);
            SetText(panel, "BtnAgain/LabelText", "10연 다시 뽑기");
            SetText(panel, "BtnAgain/PriceText", "2,700 코어");
            LayoutRebuilder.ForceRebuildLayoutImmediate(container as RectTransform);
        }

        // ───────────────────────── 자동 진행 ─────────────────────────

        public static void AutoPlayStart()
        {
            if (_autoPlay) return;
            _autoPlay = true;
            EditorApplication.update += AutoPlayTick;
        }

        public static void AutoPlayStop()
        {
            _autoPlay = false;
            EditorApplication.update -= AutoPlayTick;
        }

        private static void AutoPlayTick()
        {
            if (!EditorApplication.isPlaying) { AutoPlayStop(); return; }

            // 플레이어를 죽지 않게 한다.
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var health = player.GetComponentInChildren<SurvivalDrone.Core.Health>();
                if (health != null) health.DamageTakenMultiplier = 0f;
            }

            // 레벨업 선택 화면이 열려 있으면 하나 고른다. ("신규 드론"이 있으면 그것을 우선)
            foreach (var ui in Resources.FindObjectsOfTypeAll<SurvivalDrone.LevelUp.LevelUpUI>())
            {
                if (!ui.gameObject.scene.IsValid()) continue;
                var panelField = typeof(SurvivalDrone.LevelUp.LevelUpUI).GetField("panel", BindingFlags.NonPublic | BindingFlags.Instance);
                var buttonsField = typeof(SurvivalDrone.LevelUp.LevelUpUI).GetField("optionButtons", BindingFlags.NonPublic | BindingFlags.Instance);
                var titlesField = typeof(SurvivalDrone.LevelUp.LevelUpUI).GetField("optionTitles", BindingFlags.NonPublic | BindingFlags.Instance);
                var panel = panelField.GetValue(ui) as GameObject;
                if (panel == null || !panel.activeSelf) continue;

                var buttons = buttonsField.GetValue(ui) as Button[];
                var titles = titlesField.GetValue(ui) as Text[];
                int pick = 0;
                for (int i = 0; i < titles.Length; i++)
                    if (titles[i] != null && titles[i].text.Contains("신규 드론")) { pick = i; break; }
                if (buttons != null && pick < buttons.Length && buttons[pick] != null) buttons[pick].onClick.Invoke();
            }
        }
    }
}
