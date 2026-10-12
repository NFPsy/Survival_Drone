using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SurvivalDrone.EditorTools
{
    // UI에 쓸 아이콘 14개를 "코드로 그려서" PNG 파일로 만들어 주는 에디터 도구.
    // 외부 이미지를 내려받지 않고, 도형(원·사각형·다각형)을 조합해서 직접 그린다.
    // 그래서 라이선스 걱정이 없고, 모양을 바꾸고 싶으면 아래 그리기 함수만 고쳐서 다시 만들면 된다.
    //
    // 아이콘은 "흰색 + 투명 배경"으로 만든다. 화면에 붙일 때 Image의 색(Color)으로 칠해서
    // 용도별 색(메뉴 = 시안, 크레딧 = 금색 등)을 낸다.
    //
    // 어떻게 그리는가:
    //  - 아이콘 한 장은 128x128 픽셀이고, 좌표는 가운데가 (0,0), 가장자리가 -1~1이다. (위쪽이 +y)
    //  - 각 아이콘은 "이 좌표가 아이콘 안인가?"를 알려 주는 함수다.
    //  - 가장자리가 계단처럼 깨지지 않도록 한 픽셀을 4x4(16개)로 쪼개서 그 중 몇 개가 안쪽인지로 투명도를 정한다.
    //
    // 메뉴: SurvivalDrone/UI/Generate Icons  /  SurvivalDrone/UI/Create Icon Preview Sheet
    public static class UiIconGenerator
    {
        private const int Size = 128;
        private const int Samples = 4;
        public const string IconFolder = "Assets/Textures/UI/Icons";
        private const string PreviewPath = "Assets/Screenshots/UI/icons_preview.png";

        // 아이콘 이름 → 그리기 함수. 이름이 곧 파일 이름이다. (화면 붙이기 단계에서도 이 이름으로 찾는다)
        private static readonly List<KeyValuePair<string, Func<float, float, bool>>> Icons = new List<KeyValuePair<string, Func<float, float, bool>>>
        {
            // 로비 메뉴 6개
            Entry("icon_launch", Launch),
            Entry("icon_gacha", Gacha),
            Entry("icon_hangar", Hangar),
            Entry("icon_quest", Quest),
            Entry("icon_milestone", Milestone),
            Entry("icon_log", Log),
            // 재화 3개
            Entry("icon_core", Core),
            Entry("icon_credit", Credit),
            Entry("icon_shard", Shard),
            // 드론 종류 5개
            Entry("icon_drone_melee", Melee),
            Entry("icon_drone_sniper", Sniper),
            Entry("icon_drone_explosion", Explosion),
            Entry("icon_drone_heal", Heal),
            Entry("icon_drone_collector", Collector),
            // 능력치 강화 2개
            Entry("icon_stat_speed", StatSpeed),
            Entry("icon_stat_health", StatHealth),
        };

        private static KeyValuePair<string, Func<float, float, bool>> Entry(string name, Func<float, float, bool> shape)
        {
            return new KeyValuePair<string, Func<float, float, bool>>(name, shape);
        }

        // ───────────────────────── 메뉴 ─────────────────────────

        [MenuItem("SurvivalDrone/UI/Generate Icons")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(IconFolder);
            foreach (var icon in Icons) WritePng(icon.Key, icon.Value);
            AssetDatabase.Refresh();

            foreach (var icon in Icons)
            {
                string path = $"{IconFolder}/{icon.Key}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }
            Debug.Log($"[UiIconGenerator] 아이콘 {Icons.Count}개를 {IconFolder}에 만들었습니다.");
        }

        // 승인용 미리보기 한 장: 큰 아이콘(용도별 색) + 실제 UI에서 쓸 작은 크기(48px) + 이름을 한 화면에 늘어놓는다.
        [MenuItem("SurvivalDrone/UI/Create Icon Preview Sheet")]
        public static void CreatePreviewSheet()
        {
            if (EditorApplication.isPlaying) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var camObj = new GameObject("PreviewCam");
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.039f, 0.051f, 0.071f, 1f);
            cam.orthographic = true;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            var canvasObj = new GameObject("PreviewCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Pretendard-Regular.otf");

            // 제목
            AddText(canvasObj.transform, "UI 아이콘 미리보기 (흰색 아이콘을 용도별 색으로 칠한 모습)", 34, new Color(0.89f, 0.96f, 0.96f), font, new Vector2(0f, 480f), new Vector2(1500f, 60f));

            // 이름(한글)과 색. 순서는 Icons 목록과 같다.
            string[] labels = { "출격", "뽑기", "격납고", "일일 퀘스트", "마일스톤", "테스트 로그", "코어", "크레딧", "조각", "근접 드론", "저격 드론", "폭발 드론", "회복 드론", "수집 드론", "이동 속도", "최대 체력" };
            Color cyan = new Color(0.31f, 0.847f, 0.91f);
            Color[] colors =
            {
                cyan, cyan, cyan, cyan, cyan, cyan,
                cyan, new Color(1f, 0.82f, 0.30f), new Color(0.72f, 0.55f, 1f),
                new Color(0.55f, 0.92f, 1f), new Color(1f, 0.62f, 0.28f), new Color(1f, 0.45f, 0.25f), new Color(0.45f, 1f, 0.6f), new Color(1f, 0.88f, 0.3f),
                new Color(0.55f, 0.92f, 1f), new Color(1f, 0.45f, 0.5f),
            };

            int cols = 7;
            float cellW = 250f, cellH = 330f;
            for (int i = 0; i < Icons.Count; i++)
            {
                int col = i % cols, row = i / cols;
                float cx = (col - (cols - 1) * 0.5f) * cellW;
                float cy = 260f - row * cellH;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconFolder}/{Icons[i].Key}.png");

                var card = NewImage(canvasObj.transform, "Card" + i, new Color(0.063f, 0.078f, 0.11f, 1f), new Vector2(cx, cy), new Vector2(cellW - 20f, cellH - 20f));
                var big = NewImage(card.transform, "Big", colors[i], new Vector2(0f, 40f), new Vector2(150f, 150f));
                big.sprite = sprite;
                var small = NewImage(card.transform, "Small", colors[i], new Vector2(-30f, -90f), new Vector2(48f, 48f));
                small.sprite = sprite;
                var tiny = NewImage(card.transform, "Tiny", colors[i], new Vector2(30f, -90f), new Vector2(32f, 32f));
                tiny.sprite = sprite;
                AddText(card.transform, labels[i], 24, new Color(0.89f, 0.96f, 0.96f), font, new Vector2(0f, -140f), new Vector2(230f, 40f));
            }

            for (int i = 0; i < 3; i++)
            {
                Canvas.ForceUpdateCanvases();
                cam.Render();
            }

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));
            File.WriteAllBytes(PreviewPath, tex.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
            Debug.Log("[UiIconGenerator] 미리보기를 " + PreviewPath + "에 저장했습니다.");
        }

        private static Image NewImage(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text AddText(Transform parent, string content, int fontSize, Color color, Font font, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.font = font;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        // ───────────────────────── 그리기 ─────────────────────────

        private static void WritePng(string name, Func<float, float, bool> shape)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color[Size * Size];
            int total = Samples * Samples;

            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            // 픽셀 안의 작은 칸 한가운데 좌표를 -1~1 범위로 바꾼다. 위쪽이 +y가 되도록 py를 뒤집는다.
                            float u = ((px + (sx + 0.5f) / Samples) / Size) * 2f - 1f;
                            float v = 1f - ((py + (sy + 0.5f) / Samples) / Size) * 2f;
                            if (shape(u, v)) hits++;
                        }
                    }
                    pixels[(Size - 1 - py) * Size + px] = new Color(1f, 1f, 1f, hits / (float)total);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes($"{IconFolder}/{name}.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        // ── 도형 도우미 ──

        private static bool Circle(float x, float y, float cx, float cy, float r)
        {
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static bool Ring(float x, float y, float cx, float cy, float rOuter, float rInner)
        {
            return Circle(x, y, cx, cy, rOuter) && !Circle(x, y, cx, cy, rInner);
        }

        private static bool Box(float x, float y, float x0, float x1, float y0, float y1)
        {
            return x >= x0 && x <= x1 && y >= y0 && y <= y1;
        }

        // 선분(a→b)에서 두께(thickness)만큼 떨어진 영역. 굵은 선을 그릴 때 쓴다.
        private static bool Line(float x, float y, float ax, float ay, float bx, float by, float thickness)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Mathf.Clamp01(((x - ax) * abx + (y - ay) * aby) / (abx * abx + aby * aby));
            float px = ax + abx * t, py = ay + aby * t;
            float dx = x - px, dy = y - py;
            float r = thickness * 0.5f;
            return dx * dx + dy * dy <= r * r;
        }

        // 다각형 안쪽인지 검사(광선 교차법). 점 목록은 x,y,x,y,... 순서다.
        private static bool Poly(float x, float y, params float[] pts)
        {
            bool inside = false;
            int n = pts.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = pts[i * 2], yi = pts[i * 2 + 1];
                float xj = pts[j * 2], yj = pts[j * 2 + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        // 꼭짓점이 위로 뾰족한 정육각형(반지름 r) 안쪽인지.
        private static bool Hex(float x, float y, float r)
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float h = r * 0.8660254f; // 좌우 폭의 절반(cos 30도)
            return ax <= h && ay <= r - ax * 0.5773503f;
        }

        // 별(뾰족한 폭발 모양): 바깥 반지름 rOut, 안쪽 반지름 rIn, 뾰족한 점 개수 points.
        private static bool Star(float x, float y, int points, float rOut, float rIn)
        {
            var pts = new float[points * 4];
            for (int i = 0; i < points * 2; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / points;
                float r = (i % 2 == 0) ? rOut : rIn;
                pts[i * 2] = Mathf.Cos(angle) * r;
                pts[i * 2 + 1] = Mathf.Sin(angle) * r;
            }
            return Poly(x, y, pts);
        }

        // ── 로비 메뉴 아이콘 ──

        // 출격: 위로 솟는 로켓.
        private static bool Launch(float x, float y)
        {
            float ax = Mathf.Abs(x);
            bool body = Poly(x, y, 0f, 0.95f, 0.2f, 0.78f, 0.32f, 0.55f, 0.38f, 0.3f, 0.38f, -0.5f, -0.38f, -0.5f, -0.38f, 0.3f, -0.32f, 0.55f, -0.2f, 0.78f);
            bool fins = Poly(Mathf.Abs(x), y, 0.38f, -0.05f, 0.78f, -0.62f, 0.38f, -0.5f);
            bool flame = Poly(x, y, -0.2f, -0.56f, 0f, -0.95f, 0.2f, -0.56f);
            bool window = Circle(x, y, 0f, 0.28f, 0.16f);
            return (body || fins || flame) && !window;
        }

        // 뽑기: 눈이 5개 찍힌 주사위. ("무작위로 뽑는다"는 뜻이 누구에게나 바로 전해진다)
        private static bool Gacha(float x, float y)
        {
            // 모서리가 둥근 정사각형: 가운데 십자 영역 + 네 모서리의 둥근 원.
            const float half = 0.82f;
            const float corner = 0.22f;
            bool body = Box(x, y, -half, half, -half + corner, half - corner)
                        || Box(x, y, -half + corner, half - corner, -half, half)
                        || Circle(x, y, half - corner, half - corner, corner)
                        || Circle(x, y, -half + corner, half - corner, corner)
                        || Circle(x, y, half - corner, -half + corner, corner)
                        || Circle(x, y, -half + corner, -half + corner, corner);

            // 주사위 눈 5개(네 모서리 쪽 + 가운데)는 구멍으로 비운다.
            const float p = 0.4f;
            const float r = 0.12f;
            bool pips = Circle(x, y, p, p, r) || Circle(x, y, -p, p, r) || Circle(x, y, p, -p, r) || Circle(x, y, -p, -p, r) || Circle(x, y, 0f, 0f, r);
            return body && !pips;
        }

        // 격납고: 둥근 지붕 건물과 셔터 문.
        private static bool Hangar(float x, float y)
        {
            bool house = (Box(x, y, -0.88f, 0.88f, -0.75f, 0.12f)) || (Circle(x, y, 0f, 0.12f, 0.88f) && y > 0.12f);
            bool door = Box(x, y, -0.4f, 0.4f, -0.75f, -0.02f);
            bool slats = Box(x, y, -0.4f, 0.4f, -0.16f, -0.08f) || Box(x, y, -0.4f, 0.4f, -0.36f, -0.28f) || Box(x, y, -0.4f, 0.4f, -0.56f, -0.48f);
            return (house && !door) || slats;
        }

        // 일일 퀘스트: 체크 표시가 있는 클립보드.
        private static bool Quest(float x, float y)
        {
            bool frame = Box(x, y, -0.68f, 0.68f, -0.9f, 0.78f) && !Box(x, y, -0.54f, 0.54f, -0.76f, 0.64f);
            bool clip = Box(x, y, -0.3f, 0.3f, 0.64f, 0.95f);
            bool check = Line(x, y, -0.34f, -0.06f, -0.1f, -0.3f, 0.15f) || Line(x, y, -0.1f, -0.3f, 0.36f, 0.26f, 0.15f);
            return frame || clip || check;
        }

        // 마일스톤: 깃발.
        private static bool Milestone(float x, float y)
        {
            bool pole = Box(x, y, -0.66f, -0.52f, -0.92f, 0.92f);
            bool flag = Poly(x, y, -0.52f, 0.86f, 0.82f, 0.48f, -0.52f, 0.1f);
            bool baseBar = Box(x, y, -0.88f, -0.3f, -0.92f, -0.78f);
            return pole || flag || baseBar;
        }

        // 테스트 로그: 줄이 그어진 문서.
        private static bool Log(float x, float y)
        {
            bool frame = Box(x, y, -0.62f, 0.62f, -0.9f, 0.9f) && !Box(x, y, -0.48f, 0.48f, -0.76f, 0.76f);
            bool lines = Box(x, y, -0.3f, 0.3f, 0.36f, 0.46f) || Box(x, y, -0.3f, 0.3f, 0.06f, 0.16f) || Box(x, y, -0.3f, 0.3f, -0.24f, -0.14f) || Box(x, y, -0.3f, 0.1f, -0.54f, -0.44f);
            return frame || lines;
        }

        // ── 재화 아이콘 ──

        // 코어: 육각 테두리 안의 에너지 알맹이.
        private static bool Core(float x, float y)
        {
            bool ring = Hex(x, y, 0.92f) && !Hex(x, y, 0.72f);
            bool inner = Hex(x, y, 0.42f);
            return ring || inner;
        }

        // 크레딧: 동전(이중 테두리와 가운데 마름모).
        private static bool Credit(float x, float y)
        {
            bool ring = Ring(x, y, 0f, 0f, 0.92f, 0.76f);
            bool diamond = Poly(x, y, 0f, 0.46f, 0.34f, 0f, 0f, -0.46f, -0.34f, 0f);
            return ring || diamond;
        }

        // 조각: 크리스털 조각 두 개.
        private static bool Shard(float x, float y)
        {
            bool main = Poly(x, y, -0.05f, 0.95f, 0.6f, 0.15f, 0.3f, -0.92f, -0.55f, -0.2f);
            bool facet = Line(x, y, -0.05f, 0.88f, 0.12f, -0.82f, 0.07f);
            bool small = Poly(x, y, 0.62f, -0.05f, 0.95f, -0.5f, 0.64f, -0.9f);
            return (main && !facet) || small;
        }

        // ── 드론 종류 아이콘 ──

        // 근접 드론: 할퀴는 칼자국 세 줄. (위는 넓고 아래로 갈수록 뾰족해지는 가는 삼각형 세 개)
        private static bool Melee(float x, float y)
        {
            return Slash(x, y, -0.5f) || Slash(x, y, 0f) || Slash(x, y, 0.5f);
        }

        // 칼자국 한 줄. offset만큼 옆으로 옮겨서 세 줄을 나란히 만든다.
        private static bool Slash(float x, float y, float offset)
        {
            return Poly(x, y, 0.12f + offset, 0.92f, 0.4f + offset, 0.92f, -0.3f + offset, -0.92f);
        }

        // 저격 드론: 조준선(십자 + 원).
        private static bool Sniper(float x, float y)
        {
            bool ring = Ring(x, y, 0f, 0f, 0.62f, 0.5f);
            bool ticks = Box(x, y, -0.06f, 0.06f, 0.4f, 0.95f) || Box(x, y, -0.06f, 0.06f, -0.95f, -0.4f)
                         || Box(x, y, 0.4f, 0.95f, -0.06f, 0.06f) || Box(x, y, -0.95f, -0.4f, -0.06f, 0.06f);
            bool dot = Circle(x, y, 0f, 0f, 0.12f);
            return ring || ticks || dot;
        }

        // 폭발 드론: 사방으로 터지는 별.
        private static bool Explosion(float x, float y)
        {
            return Star(x, y, 8, 0.95f, 0.48f) && !Circle(x, y, 0f, 0f, 0.14f);
        }

        // 회복 드론: 원 안의 더하기.
        private static bool Heal(float x, float y)
        {
            bool ring = Ring(x, y, 0f, 0f, 0.92f, 0.78f);
            bool plus = Box(x, y, -0.13f, 0.13f, -0.5f, 0.5f) || Box(x, y, -0.5f, 0.5f, -0.13f, 0.13f);
            return ring || plus;
        }

        // ── 능력치 강화 아이콘 ──

        // 이동 속도: 오른쪽을 가리키는 꺾쇠 세 개(>>>).
        private static bool StatSpeed(float x, float y)
        {
            return Chevron(x, y, -0.3f) || Chevron(x, y, 0.1f) || Chevron(x, y, 0.5f);
        }

        // 꺾쇠 하나. offset만큼 옆으로 옮겨서 나란히 놓는다.
        private static bool Chevron(float x, float y, float offset)
        {
            return Poly(x, y, -0.5f + offset, 0.75f, -0.2f + offset, 0.75f, 0.28f + offset, 0f, -0.2f + offset, -0.75f, -0.5f + offset, -0.75f, -0.06f + offset, 0f);
        }

        // 최대 체력: 하트.
        private static bool StatHealth(float x, float y)
        {
            return Circle(x, y, -0.34f, 0.28f, 0.44f) || Circle(x, y, 0.34f, 0.28f, 0.44f) || Poly(x, y, -0.74f, 0.14f, 0.74f, 0.14f, 0f, -0.88f);
        }

        // 수집 드론: 말굽 자석.
        private static bool Collector(float x, float y)
        {
            bool arc = Ring(x, y, 0f, 0.05f, 0.82f, 0.5f) && y <= 0.05f;
            bool legs = Box(x, y, 0.5f, 0.82f, 0.05f, 0.78f) || Box(x, y, -0.82f, -0.5f, 0.05f, 0.78f);
            bool tipGap = Box(x, y, -0.9f, 0.9f, 0.5f, 0.57f);
            return (arc || legs) && !tipGap;
        }
    }
}
