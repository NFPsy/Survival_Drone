using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SurvivalDrone.Drones;
using SurvivalDrone.LevelUp;
using SurvivalDrone.UI;

namespace SurvivalDrone.EditorTools
{
    // 레벨업 선택 화면(InGame 씬의 LevelUpPanel)의 "단색 배경"을 입체감 있게 꾸미는 에디터 도구.
    // 이미지는 아이콘처럼 외부에서 내려받지 않고 코드로 직접 그린다. (한 장씩 그라데이션 계산)
    //
    // 꾸미는 것:
    //  화면 전체: 가장자리를 어둡게 하는 비네팅 + 카드 뒤 가운데의 은은한 시안색 빛 + 위아래 얇은 가로 장식선
    //  카드 한 장: 위쪽 종류색 띠 + 위쪽에서 아래로 옅어지는 빛 + 네 모서리 꺾쇠 장식 (LevelUpCardAccent가 종류 색으로 칠함)
    //
    // 메뉴:
    //  SurvivalDrone/UI/Generate Level Up Background Images  - 이미지 만들기
    //  SurvivalDrone/UI/Create Level Up Background Preview   - 씬은 저장하지 않고 전·후 모습을 전투 화면 위에 합성해서 이미지로 뽑기
    //  SurvivalDrone/UI/Apply Level Up Background            - InGame 씬에 실제로 붙이기 (여러 번 실행해도 안전)
    public static class LevelUpBackgroundApplier
    {
        private const string ScenePath = "Assets/Scenes/InGame.unity";
        private const string TexFolder = "Assets/Textures/UI/LevelUp";
        private const string OutFolder = "Assets/Screenshots/UI";

        private static readonly Color Cyan = new Color(0.310f, 0.847f, 0.910f);
        private static readonly Color NewColor = new Color(0.31f, 0.847f, 0.91f);
        private static readonly Color UpgradeColor = new Color(0.55f, 0.95f, 0.6f);
        private static readonly Color StatColor = new Color(0.72f, 0.78f, 0.8f);

        // ───────────────────────── 이미지 만들기 ─────────────────────────

        [MenuItem("SurvivalDrone/UI/Generate Level Up Background Images")]
        public static void GenerateTextures()
        {
            Directory.CreateDirectory(TexFolder);

            // 비네팅: 가운데는 투명, 가장자리로 갈수록 어두워진다. (가로로 늘려 쓰므로 16:9 비율로 그린다)
            WriteTexture("levelup_vignette", 256, 144, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v) / 1.4142f;
                return 0.75f * SmoothStep(0.35f, 1f, r);
            });

            // 빛: 가운데가 가장 밝고 바깥으로 부드럽게 사라지는 둥근 빛.
            WriteTexture("levelup_glow", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
            });

            // 카드 위쪽 빛: 위(v=1)가 가장 밝고 카드 절반 아래부터는 투명해진다. (글자가 읽기 어려워지지 않게 옅게 쓴다)
            WriteTexture("levelup_card_gradient", 4, 128, (u, v) =>
            {
                float t = (v + 1f) * 0.5f;
                return Mathf.Pow(Mathf.Clamp01((t - 0.5f) / 0.5f), 1.5f);
            });

            // 가로 장식선: 양 끝으로 갈수록 흐려진다.
            WriteTexture("levelup_line", 256, 4, (u, v) =>
            {
                float x01 = (u + 1f) * 0.5f;
                return Mathf.Pow(Mathf.Sin(Mathf.PI * x01), 0.6f);
            });

            // 모서리 꺾쇠(왼쪽 위 기준 ┌ 모양). 다른 모서리는 돌려서 쓴다.
            const int cornerSize = 64;
            WriteTexture("levelup_corner", cornerSize, cornerSize, (u, v) =>
            {
                float x = (u + 1f) * 0.5f * cornerSize;          // 왼쪽에서 몇 픽셀
                float yTop = (1f - v) * 0.5f * cornerSize;        // 위에서 몇 픽셀
                const float thickness = 5f, length = 36f, margin = 2f;
                bool horizontal = yTop >= margin && yTop < margin + thickness && x >= margin && x < margin + length;
                bool vertical = x >= margin && x < margin + thickness && yTop >= margin && yTop < margin + length;
                return (horizontal || vertical) ? 1f : 0f;
            });

            AssetDatabase.Refresh();
            foreach (var name in new[] { "levelup_vignette", "levelup_glow", "levelup_card_gradient", "levelup_line", "levelup_corner" })
            {
                string path = $"{TexFolder}/{name}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Debug.Log($"[LevelUpBackground] 이미지 5개를 {TexFolder}에 만들었습니다.");
        }

        // alpha(u, v): 좌표 u, v는 가운데가 (0,0), 가장자리가 -1~1 (위쪽이 +v). 흰색 + 투명도로 저장한다.
        private static void WriteTexture(string name, int width, int height, Func<float, float, float> alpha)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    float u = ((px + 0.5f) / width) * 2f - 1f;
                    float v = ((py + 0.5f) / height) * 2f - 1f;   // 텍스처는 아래가 0이므로 위쪽이 +v
                    float a = Mathf.Clamp01(alpha(u, v));
                    pixels[py * width + px] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes($"{TexFolder}/{name}.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static Sprite LoadSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{TexFolder}/{name}.png");
        }

        // ───────────────────────── 화면에 꾸미기 ─────────────────────────

        // 레벨업 패널에 배경 장식과 카드 장식을 만든다. 이미 있으면 모양만 맞춘다.
        public static void Build(Transform panel)
        {
            // 화면 전체 배경: 격자(GridOverlay) 바로 위에 빛 → 비네팅 → 가로선 순서로 깐다. (카드는 그 위)
            int order = panel.Find("GridOverlay") != null ? panel.Find("GridOverlay").GetSiblingIndex() + 1 : 0;

            var glow = EnsureImage(panel, "BgGlow", LoadSprite("levelup_glow"), new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f), order++);
            SetRect(glow.rectTransform, new Vector2(0f, -20f), new Vector2(1500f, 900f));

            var vignette = EnsureImage(panel, "BgVignette", LoadSprite("levelup_vignette"), new Color(0.01f, 0.02f, 0.04f, 1f), order++);
            Stretch(vignette.rectTransform);

            var lineTop = EnsureImage(panel, "BgLineTop", LoadSprite("levelup_line"), new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f), order++);
            SetRect(lineTop.rectTransform, new Vector2(0f, 385f), new Vector2(1000f, 4f));
            var lineBottom = EnsureImage(panel, "BgLineBottom", LoadSprite("levelup_line"), new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f), order++);
            SetRect(lineBottom.rectTransform, new Vector2(0f, -290f), new Vector2(1000f, 4f));

            // 카드 장식
            for (int i = 0; i < 3; i++)
            {
                var card = panel.Find("Option" + i);
                if (card == null) continue;

                var gradient = EnsureImage(card, "AccentGradient", LoadSprite("levelup_card_gradient"), new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f), 0);
                Stretch(gradient.rectTransform);

                var band = EnsureImage(card, "AccentBand", null, Cyan, 1);
                band.rectTransform.anchorMin = new Vector2(0f, 1f);
                band.rectTransform.anchorMax = new Vector2(1f, 1f);
                band.rectTransform.pivot = new Vector2(0.5f, 1f);
                band.rectTransform.anchoredPosition = Vector2.zero;
                band.rectTransform.sizeDelta = new Vector2(0f, 6f);

                // 꺾쇠: 왼쪽 위(그대로), 오른쪽 위(시계 방향 90도), 오른쪽 아래(180도), 왼쪽 아래(반시계 90도)
                var corners = new Image[4];
                string[] names = { "AccentCornerTL", "AccentCornerTR", "AccentCornerBR", "AccentCornerBL" };
                float[] angles = { 0f, -90f, 180f, 90f };
                Vector2[] positions = { new Vector2(-140f, 200f), new Vector2(140f, 200f), new Vector2(140f, -200f), new Vector2(-140f, -200f) };
                for (int k = 0; k < 4; k++)
                {
                    corners[k] = EnsureImage(card, names[k], LoadSprite("levelup_corner"), new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f), 2 + k);
                    SetRect(corners[k].rectTransform, positions[k], new Vector2(28f, 28f));
                    corners[k].rectTransform.localRotation = Quaternion.Euler(0f, 0f, angles[k]);
                }

                var accent = card.GetComponent<LevelUpCardAccent>();
                if (accent == null) accent = card.gameObject.AddComponent<LevelUpCardAccent>();
                var so = new SerializedObject(accent);
                so.FindProperty("band").objectReferenceValue = band;
                so.FindProperty("gradient").objectReferenceValue = gradient;
                var cornerProp = so.FindProperty("corners");
                cornerProp.arraySize = 4;
                for (int k = 0; k < 4; k++) cornerProp.GetArrayElementAtIndex(k).objectReferenceValue = corners[k];
                so.ApplyModifiedPropertiesWithoutUndo();
                accent.SetColor(NewColor);
                EditorUtility.SetDirty(card.gameObject);
            }
        }

        [MenuItem("SurvivalDrone/UI/Apply Level Up Background")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[LevelUpBackground] 플레이 모드 중에는 쓸 수 없습니다.");
                return;
            }
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded)
            {
                Debug.LogWarning("[LevelUpBackground] InGame 씬이 이미 열려 있습니다. 다른 씬을 먼저 열고 다시 실행해 주세요.");
                return;
            }

            GenerateTextures();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var panel = FindByName(scene, "LevelUpPanel");
            if (panel == null)
            {
                Debug.LogWarning("[LevelUpBackground] LevelUpPanel을 찾지 못했습니다.");
                EditorSceneManager.CloseScene(scene, true);
                return;
            }
            Build(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[LevelUpBackground] 레벨업 선택 화면 배경을 InGame 씬에 붙였습니다.");
        }

        // ───────────────────────── 미리보기 ─────────────────────────

        [MenuItem("SurvivalDrone/UI/Create Level Up Background Preview")]
        public static void CreatePreview()
        {
            if (EditorApplication.isPlaying) return;
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded)
            {
                Debug.LogWarning("[LevelUpBackground] InGame 씬이 이미 열려 있습니다. 다른 씬을 먼저 열고 다시 실행해 주세요.");
                return;
            }

            GenerateTextures();
            Directory.CreateDirectory(OutFolder);

            // 전투 장면 위에 얹힌 모습을 보려면 "화면 뒤가 비치는 정도(투명도)"를 알아야 해서, 검정 배경과 흰 배경 두 번 찍는다.
            // 두 장의 차이로 투명도를 계산해 전투 스크린샷과 합치는 일은 미리보기 도구(파이썬)가 한다.
            foreach (var tag in new[] { "before", "after" })
            {
                bool styled = tag == "after";
                UiScreenshotTool.CaptureScene(ScenePath, $"{OutFolder}/levelup_bg_{tag}_black.png", 1920, 1080, scene => PreparePreview(scene, styled, Color.black));
                UiScreenshotTool.CaptureScene(ScenePath, $"{OutFolder}/levelup_bg_{tag}_white.png", 1920, 1080, scene => PreparePreview(scene, styled, Color.white));
            }
            AssetDatabase.Refresh();
            Debug.Log("[LevelUpBackground] 미리보기 원본(검정·흰 배경 전·후 4장)을 " + OutFolder + "에 저장했습니다. (씬은 저장하지 않았습니다)");
        }

        // 메모리에 열린 InGame 복사본에 예시 내용을 채우고, 필요하면 새 배경을 만든다. (저장하지 않는다)
        private static void PreparePreview(Scene scene, bool styled, Color cameraBackground)
        {
            var panel = FindByName(scene, "LevelUpPanel");
            if (panel == null) return;
            panel.gameObject.SetActive(true);
            foreach (var other in new[] { "HUD", "ResultPanel", "PausePanel", "LowHealthOverlay", "OverdriveOverlay" })
            {
                var t = FindByName(scene, other);
                if (t != null) t.gameObject.SetActive(false);
            }

            // 패널 전체를 덮는 어두운 바탕(알파 0.78)은 이 캡처 방식에서는 뒤를 비추지 못하고 불투명하게 찍히므로 미리보기에서는 끄고, 파이썬 합성 단계에서 전투 스크린샷 위에 다시 깐다.
            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null) panelImage.enabled = false;

            // 화면 뒤가 비치는 정도를 알아내려고, 캔버스 맨 뒤에 검정 또는 흰색 꽉 찬 이미지를 깐다.
            // (캡처용 카메라의 배경색은 이 프로젝트의 렌더러에서 먹히지 않아서 이미지로 대신한다. 메모리의 복사본이라 저장되지 않는다)
            var canvas = FindByName(scene, "Canvas");
            if (canvas != null)
            {
                var matte = new GameObject("PreviewMatte", typeof(RectTransform), typeof(Image));
                matte.transform.SetParent(canvas, false);
                matte.transform.SetAsFirstSibling();
                Stretch((RectTransform)matte.transform);
                matte.GetComponent<Image>().color = cameraBackground;
            }

            if (styled) Build(panel);

            string[] tags = { "NEW", "강화", "능력치" };
            Color[] kindColors = { NewColor, UpgradeColor, StatColor };
            string[] titles = { "폭발 드론", "근접 드론", "이동 속도 +6%" };
            string[] descs = { "일정 간격으로 주변에 폭발을 일으켜요.", "플레이어 주위를 돌며 가까운 적을 자동으로 베어요.", "이동 속도를 증가시킵니다." };
            string[] changes = { "Lv 1 로 시작", "Lv 2 → 3", "5.0 → 5.3" };
            for (int i = 0; i < 3; i++)
            {
                var card = panel.Find("Option" + i);
                if (card == null) continue;
                SetText(card, "Title", titles[i]);
                SetText(card, "Desc", descs[i]);
                SetText(card, "Tag", tags[i]);
                SetText(card, "Change", changes[i]);
                var tag = card.Find("Tag");
                if (tag != null) tag.GetComponent<Text>().color = kindColors[i];
                var icon = card.Find("BigIcon");
                if (icon != null)
                {
                    var image = icon.GetComponent<Image>();
                    if (i == 0) UiIcons.SetDroneIcon(image, DroneType.Explosion);
                    else if (i == 1) UiIcons.SetDroneIcon(image, DroneType.Melee);
                    else UiIcons.SetStatIcon(image, true);
                }
                var accent = card.GetComponent<LevelUpCardAccent>();
                if (accent != null) accent.SetColor(kindColors[i]);
            }
        }

        // ───────────────────────── 도우미 ─────────────────────────

        private static Transform FindByName(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            return null;
        }

        private static void SetText(Transform root, string path, string value)
        {
            var t = root.Find(path);
            var text = t != null ? t.GetComponent<Text>() : null;
            if (text != null) text.text = value;
        }

        // 이름으로 자식 이미지를 찾고 없으면 만든다. 형제 순서(siblingIndex)를 맞춘다.
        private static Image EnsureImage(Transform parent, string name, Sprite sprite, Color color, int siblingIndex)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
            }
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            go.transform.SetSiblingIndex(siblingIndex);
            return image;
        }

        private static void SetRect(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
