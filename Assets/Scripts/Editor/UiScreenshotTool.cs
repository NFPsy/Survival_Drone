using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SurvivalDrone.Drones;
using SurvivalDrone.UI;

namespace SurvivalDrone.EditorTools
{
    // 로비·격납고·뽑기 같은 UI 씬을 "게임을 실행하지 않고" 이미지 파일(PNG)로 뽑아 주는 에디터 도구.
    // 아이콘 같은 UI를 바꾸기 전과 후를 같은 조건으로 찍어서 비교하려고 만들었다.
    //
    // 왜 게임을 실행하지 않나:
    //  로비나 격납고를 에디터에서 직접 플레이하면 사용자의 저장 파일에 접속 기록이 붙을 수 있다.
    //  그래서 씬을 읽기만 하고(저장하지 않고) 카메라로 한 번 그려서 파일로 저장한다.
    //
    // 어떻게 동작하는가:
    //  1) 씬을 겹쳐서(Additive) 연다.
    //  2) 게임이 실행될 때 코드가 채워 주는 내용(격납고 드론 목록, 뽑기 결과 카드, 레벨업 선택 카드, 숫자 등)은
    //     그대로는 비어 있으므로, "예시 내용"을 메모리에서만 채운다. (전·후 모두 같은 예시를 쓴다)
    //  3) 화면 위에 바로 그리는 캔버스(Overlay)는 카메라로 못 찍으므로, 메모리에서만 "카메라 기준 캔버스"로 바꾼다.
    //  4) 임시 카메라로 한 번 그려서 PNG로 저장한다.
    //  5) 씬을 저장하지 않고 닫는다. (메모리에서 바꾼 것은 모두 사라진다)
    //
    // 메뉴: SurvivalDrone/UI/Capture UI Screenshots (Before)  /  (After)
    public static class UiScreenshotTool
    {
        private const string OutputFolder = "Assets/Screenshots/UI";

        // 찍을 화면 목록: (파일 이름에 쓸 이름, 씬 경로, 예시 내용을 채우는 함수)
        private class Target
        {
            public string name;
            public string scenePath;
            public Action<Scene> prepare;
        }

        private static readonly Target[] Targets =
        {
            new Target { name = "lobby", scenePath = "Assets/Scenes/Lobby.unity", prepare = PrepareLobby },
            new Target { name = "hangar", scenePath = "Assets/Scenes/Hangar.unity", prepare = PrepareHangar },
            new Target { name = "gacha", scenePath = "Assets/Scenes/Gacha.unity", prepare = PrepareGacha },
            new Target { name = "gacha_result", scenePath = "Assets/Scenes/Gacha.unity", prepare = PrepareGachaResult },
            new Target { name = "levelup", scenePath = "Assets/Scenes/InGame.unity", prepare = PrepareLevelUp },
        };

        [MenuItem("SurvivalDrone/UI/Capture UI Screenshots (Before)")]
        public static void CaptureBefore() { CaptureAll("before"); }

        [MenuItem("SurvivalDrone/UI/Capture UI Screenshots (After)")]
        public static void CaptureAfter() { CaptureAll("after"); }

        public static void CaptureAll(string tag)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[UiScreenshotTool] 플레이 모드 중에는 쓸 수 없습니다.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);
            foreach (var target in Targets)
            {
                string outPath = $"{OutputFolder}/{target.name}_{tag}.png";
                CaptureScene(target.scenePath, outPath, 1920, 1080, target.prepare);
            }
            AssetDatabase.Refresh();
            Debug.Log($"[UiScreenshotTool] '{tag}' 화면 {Targets.Length}장을 {OutputFolder}에 저장했습니다.");
        }

        // 씬 하나를 열어서 한 장 찍고 닫는다. (씬은 저장하지 않는다)
        public static void CaptureScene(string scenePath, string outPath, int width, int height, Action<Scene> prepare)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            var camObj = new GameObject("TempUiCapture");
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.039f, 0.051f, 0.071f, 1f);
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.cullingMask = 0; // 아래에서 캔버스가 속한 레이어만 켠다.
            // UI 캔버스는 카메라를 따라다니므로, 카메라를 씬의 3D 물체(플레이어 등)에서 아주 멀리 치워 두면 UI만 찍힌다.
            camObj.transform.position = new Vector3(50000f, 50000f, 0f);

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            // 이 씬의 맨 위 캔버스들을 카메라 기준으로 바꾼다. (메모리에서만)
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (!canvas.isRootCanvas) continue;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = 10f;
                    cam.cullingMask |= 1 << canvas.gameObject.layer;
                }
            }

            // 예시 내용을 채운다. (게임이 실행될 때 코드가 하는 일을 흉내 낸다)
            if (prepare != null) prepare(scene);

            // 글자 옆에 아이콘을 붙이는 컴포넌트는 글자가 바뀐 뒤에 위치를 다시 잡아 준다.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var prefix in root.GetComponentsInChildren<TextIconPrefix>(false))
                    prefix.Reposition();

            // 글자·레이아웃이 계산되도록 여러 번 갱신하며 그린다.
            for (int i = 0; i < 3; i++)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var prefix in root.GetComponentsInChildren<TextIconPrefix>(false))
                        prefix.Reposition();
                cam.Render();
            }

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            File.WriteAllBytes(outPath, tex.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camObj);

            // 저장하지 않고 닫는다.
            EditorSceneManager.CloseScene(scene, true);
        }

        // ───────────────────────── 예시 내용 채우기 ─────────────────────────

        private static Transform FindByName(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            }
            return null;
        }

        private static Transform FindByPath(Scene scene, string path)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = root.transform.Find(path);
                if (t != null) return t;
            }
            return null;
        }

        private static void SetText(Transform root, string path, string value)
        {
            var t = root != null ? root.Find(path) : null;
            var text = t != null ? t.GetComponent<Text>() : null;
            if (text != null) text.text = value;
        }

        private static void PrepareLobby(Scene scene)
        {
            var canvas = FindByName(scene, "Canvas");
            SetText(canvas, "TopBar/CreditText", "크레딧  1,250");
            SetText(canvas, "TopBar/CoreText", "코어  2,700");
        }

        private static void PrepareHangar(Scene scene)
        {
            var canvas = FindByName(scene, "Canvas");
            SetText(canvas, "TopBar/CreditText", "크레딧  1,250");
            SetText(canvas, "TopBar/CoreText", "코어  2,700");
            SetText(canvas, "DetailPanel/RarityLevelText", "SR   Lv3 / 5");
            SetText(canvas, "DetailPanel/PowerText", "전투력  128");
            SetText(canvas, "DetailPanel/ShardsText", "조각  42 / 80");
            SetText(canvas, "DetailPanel/CostText", "크레딧  800");
            SetText(canvas, "TotalPowerText", "내 전투력  214");

            // 상세 패널 제목 옆 아이콘 (코드가 실행 중에 하는 일)
            var nameIcon = canvas != null ? canvas.Find("DetailPanel/NameText/Icon") : null;
            if (nameIcon != null) UiIcons.SetDroneIcon(nameIcon.GetComponent<Image>(), DroneType.Melee);

            // 드론 목록 줄 5개 (코드가 견본을 복제해서 만드는 것을 흉내 낸다)
            var template = canvas != null ? canvas.Find("ListPanel/RowTemplate") : null;
            var container = canvas != null ? canvas.Find("ListPanel/RowContainer") : null;
            if (template == null || container == null) return;

            string[] info = { "SR   Lv3", "N   Lv1", "R   Lv2", "미획득", "미획득" };
            string[] tags = { "장착 중  (슬롯 1)", "장착 중  (슬롯 2)", "", "", "" };
            bool[] owned = { true, true, true, false, false };
            int i = 0;
            foreach (DroneType type in Enum.GetValues(typeof(DroneType)))
            {
                var row = UnityEngine.Object.Instantiate(template.gameObject, container);
                row.name = "Row_" + type;
                row.SetActive(true);
                var nameText = row.transform.Find("NameText").GetComponent<Text>();
                nameText.text = DroneNames.Get(type);
                nameText.color = owned[i] ? new Color(0.89f, 0.96f, 0.96f) : new Color(0.35f, 0.4f, 0.45f);
                var infoText = row.transform.Find("InfoText").GetComponent<Text>();
                infoText.text = info[i];
                infoText.color = owned[i] ? new Color(0.72f, 0.55f, 1f) : new Color(0.35f, 0.4f, 0.45f);
                var tagText = row.transform.Find("TagText").GetComponent<Text>();
                tagText.text = tags[i];
                var iconTransform = row.transform.Find("Icon");
                if (iconTransform != null)
                {
                    var icon = iconTransform.GetComponent<Image>();
                    UiIcons.SetDroneIcon(icon, type);
                    if (!owned[i]) icon.color = new Color(0.35f, 0.4f, 0.45f);
                }
                i++;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(container as RectTransform);
        }

        private static void PrepareGacha(Scene scene)
        {
            var canvas = FindByName(scene, "Canvas");
            if (canvas == null) return;

            string core = "코어  2,700";
            foreach (var path in new[] { "TopBar/CoreText", "GachaSelectPanel/CoreText", "LockOnPanel/CoreText" })
                SetText(canvas, path, core);

            // 뽑기 버튼 아래 가격 글자 (코드가 실행 중에 채운다)
            foreach (var buttonName in new[] { "BtnPullSingle", "BtnPullTen" })
            {
                var button = canvas.Find(buttonName);
                if (button == null) continue;
                foreach (var text in button.GetComponentsInChildren<Text>(true))
                {
                    if (text.text.Contains("코어")) text.text = buttonName == "BtnPullSingle" ? "300 코어" : "2,700 코어";
                }
            }
        }

        private static void PrepareGachaResult(Scene scene)
        {
            var panel = FindByName(scene, "ResultPanel");
            if (panel == null) return;
            panel.gameObject.SetActive(true);

            var template = panel.Find("CardTemplate");
            var container = panel.Find("CardContainer");
            if (template == null || container == null) return;

            var grid = container.GetComponent<GridLayoutGroup>();
            if (grid != null) grid.constraintCount = 5;

            DroneType[] drones = { DroneType.Melee, DroneType.Sniper, DroneType.Heal, DroneType.Explosion, DroneType.Collector };
            string[] rarities = { "N", "SR", "R", "SSR", "R" };
            string[] tags = { "NEW", "승급  R → SR", "중복  조각 +10", "NEW", "중복  조각 +5" };
            Color[] rarityColors =
            {
                new Color(0.6f, 0.65f, 0.7f), new Color(0.72f, 0.45f, 1f), new Color(0.4f, 0.7f, 1f), new Color(1f, 0.82f, 0.3f), new Color(0.4f, 0.7f, 1f),
            };

            for (int i = 0; i < drones.Length; i++)
            {
                var card = UnityEngine.Object.Instantiate(template.gameObject, container);
                card.name = "Card_" + (i + 1);
                card.SetActive(true);
                card.transform.Find("Back").gameObject.SetActive(false);
                card.transform.Find("Face").gameObject.SetActive(true);

                var rarity = card.transform.Find("Face/RarityText").GetComponent<Text>();
                rarity.text = rarities[i];
                rarity.color = rarityColors[i];
                card.transform.Find("Face/DroneNameText").GetComponent<Text>().text = DroneNames.Get(drones[i]);
                var tag = card.transform.Find("Face/TagText").GetComponent<Text>();
                tag.text = tags[i];
                tag.color = new Color(0.31f, 0.847f, 0.91f);

                var iconTransform = card.transform.Find("Face/DroneNameText/Icon");
                if (iconTransform != null) UiIcons.SetDroneIcon(iconTransform.GetComponent<Image>(), drones[i]);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(container as RectTransform);
        }

        private static void PrepareLevelUp(Scene scene)
        {
            var panel = FindByName(scene, "LevelUpPanel");
            if (panel == null) return;
            panel.gameObject.SetActive(true);

            // 씬에는 결과 화면·피격 경고 등이 켜진 채 저장돼 있다. (게임이 시작되면 코드가 끈다) 레벨업 화면만 보이도록 끈다.
            foreach (var other in new[] { "ResultPanel", "LowHealthOverlay", "OverdriveOverlay" })
            {
                var t = FindByName(scene, other);
                if (t != null) t.gameObject.SetActive(false);
            }

            string[] titles = { "신규 드론: Explosion", "강화: Melee", "이동 속도 +6%" };
            string[] descs = { "새로운 드론을 장착합니다.", "보유한 드론의 레벨을 올립니다.", "이동 속도를 증가시킵니다." };
            DroneType?[] drones = { DroneType.Explosion, DroneType.Melee, null };

            // LevelUpUI에 연결된 아이콘 칸(없으면 아직 아이콘을 안 붙인 상태)
            Image[] icons = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var comp in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (comp == null || comp.GetType().Name != "LevelUpUI") continue;
                    var so = new SerializedObject(comp);
                    var prop = so.FindProperty("optionIcons");
                    if (prop != null && prop.arraySize > 0)
                    {
                        icons = new Image[prop.arraySize];
                        for (int k = 0; k < prop.arraySize; k++) icons[k] = prop.GetArrayElementAtIndex(k).objectReferenceValue as Image;
                    }
                }
            }

            for (int i = 0; i < 3; i++)
            {
                var option = panel.Find("Option" + i);
                if (option == null) continue;
                SetText(option, "Title", titles[i]);
                SetText(option, "Desc", descs[i]);

                if (icons != null && i < icons.Length && icons[i] != null)
                {
                    if (drones[i].HasValue) UiIcons.SetDroneIcon(icons[i], drones[i].Value);
                    else icons[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
