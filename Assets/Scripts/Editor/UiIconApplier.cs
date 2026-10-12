using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SurvivalDrone.UI;

namespace SurvivalDrone.EditorTools
{
    // 만들어 둔 아이콘 14개를 실제 UI 씬(로비·격납고·뽑기·InGame 레벨업)에 붙여 주는 에디터 도구.
    // "SurvivalDrone/UI/Generate Icons"로 아이콘 이미지를 먼저 만든 뒤에 실행한다.
    //
    // 여러 번 실행해도 안전하다(이미 붙은 아이콘은 다시 만들지 않고 그림·색·크기만 맞춘다).
    // 글자 자체는 건드리지 않고, 글자 왼쪽에 아이콘을 붙이기만 한다.
    //
    // 붙이는 방식:
    //  - 대부분은 글자(Text)의 자식으로 "Icon" 이미지를 만들고 TextIconPrefix를 붙인다. 글자가 바뀌어도 아이콘이 따라간다.
    //  - 격납고 드론 목록 줄은 왼쪽 끝에 아이콘 자리를 두고 글자를 오른쪽으로 조금 밀어 준다.
    //  - 드론 종류에 따라 바뀌는 아이콘(격납고·뽑기 결과·레벨업)은 그림을 코드가 실행 중에 넣는다. 여기서는 자리만 만든다.
    //
    // 메뉴: SurvivalDrone/UI/Apply Icons To Scenes
    public static class UiIconApplier
    {
        private const string IconSetPath = "Assets/Resources/UiIconSet.asset";

        // 글자 옆 아이콘이 글자보다 살짝 커 보이도록 글자 크기에 곱하는 값.
        private const float TextIconScale = 1.15f;

        [MenuItem("SurvivalDrone/UI/Apply Icons To Scenes")]
        public static void ApplyAll()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[UiIconApplier] 플레이 모드 중에는 쓸 수 없습니다.");
                return;
            }

            EnsureIconSet();

            ApplyScene("Assets/Scenes/Lobby.unity", ApplyLobby);
            ApplyScene("Assets/Scenes/Hangar.unity", ApplyHangar);
            ApplyScene("Assets/Scenes/Gacha.unity", ApplyGacha);
            ApplyScene("Assets/Scenes/InGame.unity", ApplyInGame);

            AssetDatabase.SaveAssets();
            Debug.Log("[UiIconApplier] 로비·격납고·뽑기·InGame 씬에 아이콘을 붙였습니다.");
        }

        private static void ApplyScene(string path, System.Action<Scene> apply)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        // ───────────────────────── 공통 도우미 ─────────────────────────

        private static Sprite LoadSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{UiIconGenerator.IconFolder}/{name}.png");
        }

        // 실행 중에 코드가 찾아 쓰는 아이콘 모음(Resources/UiIconSet.asset)을 만들고 채운다.
        private static void EnsureIconSet()
        {
            System.IO.Directory.CreateDirectory("Assets/Resources");
            var set = AssetDatabase.LoadAssetAtPath<UiIconSet>(IconSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<UiIconSet>();
                AssetDatabase.CreateAsset(set, IconSetPath);
            }

            set.core = LoadSprite("icon_core");
            set.credit = LoadSprite("icon_credit");
            set.shard = LoadSprite("icon_shard");
            set.droneMelee = LoadSprite("icon_drone_melee");
            set.droneSniper = LoadSprite("icon_drone_sniper");
            set.droneExplosion = LoadSprite("icon_drone_explosion");
            set.droneHeal = LoadSprite("icon_drone_heal");
            set.droneCollector = LoadSprite("icon_drone_collector");
            set.statSpeed = LoadSprite("icon_stat_speed");
            set.statHealth = LoadSprite("icon_stat_health");
            EditorUtility.SetDirty(set);
        }

        private static Transform FindByName(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            }
            return null;
        }

        private static Text GetText(Transform root, string path)
        {
            var t = root != null ? root.Find(path) : null;
            var text = t != null ? t.GetComponent<Text>() : null;
            if (text == null) Debug.LogWarning($"[UiIconApplier] 글자 칸을 찾지 못했습니다: {path}");
            return text;
        }

        // 글자 왼쪽에 아이콘을 붙인다. 이미 있으면 그림·색·크기만 맞춘다.
        private static Image AddPrefixIcon(Text text, Sprite sprite, Color color, float size, float gap)
        {
            if (text == null) return null;

            var existing = text.transform.Find("Icon");
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(text.transform, false);
            }

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);

            var prefix = text.GetComponent<TextIconPrefix>();
            if (prefix == null) prefix = text.gameObject.AddComponent<TextIconPrefix>();
            var so = new SerializedObject(prefix);
            so.FindProperty("icon").objectReferenceValue = go.GetComponent<RectTransform>();
            so.FindProperty("gap").floatValue = gap;
            so.ApplyModifiedPropertiesWithoutUndo();
            prefix.Reposition();

            EditorUtility.SetDirty(text.gameObject);
            return image;
        }

        // 버튼 글자 왼쪽에 메뉴 아이콘을 붙인다. 채워진(밝은) 버튼이면 어두운 아이콘, 어두운 버튼이면 시안 아이콘.
        private static void AddButtonIcon(Transform button, string spriteName)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<Text>(true);
            var fill = button.GetComponent<Image>();
            if (label == null) return;

            float luminance = fill != null ? fill.color.r * 0.299f + fill.color.g * 0.587f + fill.color.b * 0.114f : 0f;
            Color color = luminance > 0.5f ? UiIcons.Dark : UiIcons.Cyan;
            float height = ((RectTransform)button).rect.height;
            AddPrefixIcon(label, LoadSprite(spriteName), color, Mathf.Round(height * 0.5f), 12f);
        }

        // 화면 위쪽 재화 글자(코어·크레딧) 옆에 아이콘을 붙인다.
        private static void AddCurrencyIcon(Text text, Sprite sprite, Color color)
        {
            if (text == null) return;
            AddPrefixIcon(text, sprite, color, Mathf.Round(text.fontSize * TextIconScale), 10f);
        }

        // ───────────────────────── 씬별 ─────────────────────────

        private static void ApplyLobby(Scene scene)
        {
            var canvas = FindByName(scene, "Canvas");
            if (canvas == null) return;

            AddButtonIcon(canvas.Find("BtnLaunch"), "icon_launch");
            AddButtonIcon(canvas.Find("StageSelectPanel/BtnLaunch"), "icon_launch");
            AddButtonIcon(canvas.Find("BtnGacha"), "icon_gacha");
            AddButtonIcon(canvas.Find("BtnHangar"), "icon_hangar");
            AddButtonIcon(canvas.Find("BtnQuest"), "icon_quest");
            AddButtonIcon(canvas.Find("BtnMilestone"), "icon_milestone");
            AddButtonIcon(canvas.Find("BtnLog"), "icon_log");

            AddCurrencyIcon(GetText(canvas, "TopBar/CreditText"), LoadSprite("icon_credit"), UiIcons.Gold);
            AddCurrencyIcon(GetText(canvas, "TopBar/CoreText"), LoadSprite("icon_core"), UiIcons.Cyan);

            // 일일 퀘스트·마일스톤 보상 글자("코어 +200")
            for (int i = 0; i < 4; i++)
                AddCurrencyIcon(GetText(canvas, $"DailyQuestPopup/Box/Row{i}/RewardText"), LoadSprite("icon_core"), UiIcons.Cyan);
            AddCurrencyIcon(GetText(canvas, "MilestonePopup/Box/RewardText"), LoadSprite("icon_core"), UiIcons.Cyan);
        }

        private static void ApplyHangar(Scene scene)
        {
            var canvas = FindByName(scene, "Canvas");
            if (canvas == null) return;

            AddCurrencyIcon(GetText(canvas, "TopBar/CreditText"), LoadSprite("icon_credit"), UiIcons.Gold);
            AddCurrencyIcon(GetText(canvas, "TopBar/CoreText"), LoadSprite("icon_core"), UiIcons.Cyan);

            // 상세 패널: 조각·크레딧 줄과 드론 이름(그림은 코드가 드론 종류에 맞게 바꾼다)
            AddPrefixIcon(GetText(canvas, "DetailPanel/ShardsText"), LoadSprite("icon_shard"), UiIcons.Purple, 38f, 10f);
            AddPrefixIcon(GetText(canvas, "DetailPanel/CostText"), LoadSprite("icon_credit"), UiIcons.Gold, 38f, 10f);
            AddPrefixIcon(GetText(canvas, "DetailPanel/NameText"), LoadSprite("icon_drone_melee"), UiIcons.DroneColor(SurvivalDrone.Drones.DroneType.Melee), 68f, 14f);

            // 드론 목록 줄 견본: 왼쪽 끝에 아이콘 자리를 만들고 글자를 오른쪽으로 조금 민다.
            var template = canvas.Find("ListPanel/RowTemplate");
            if (template != null)
            {
                var existing = template.Find("Icon");
                if (existing == null)
                {
                    var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(template, false);
                    var rect = go.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(64f, 64f);
                    rect.anchoredPosition = new Vector2(52f, 0f);

                    // 이름·장착 표시 글자는 아이콘만큼 오른쪽으로 민다. (아이콘을 처음 만들 때 한 번만)
                    foreach (var childName in new[] { "NameText", "TagText" })
                    {
                        var child = template.Find(childName) as RectTransform;
                        if (child == null) continue;
                        var min = child.offsetMin;
                        min.x += 84f;
                        child.offsetMin = min;
                    }
                    existing = go.transform;
                }

                var image = existing.GetComponent<Image>();
                image.sprite = LoadSprite("icon_drone_melee");
                image.color = UiIcons.DroneColor(SurvivalDrone.Drones.DroneType.Melee);
                image.preserveAspect = true;
                image.raycastTarget = false;
                EditorUtility.SetDirty(template.gameObject);
            }
        }

        private static void ApplyGacha(Scene scene)
        {
            var canvas = FindByName(scene, "Canvas");
            if (canvas == null) return;

            foreach (var path in new[] { "TopBar/CoreText", "GachaSelectPanel/CoreText", "LockOnPanel/CoreText" })
                AddCurrencyIcon(GetText(canvas, path), LoadSprite("icon_core"), UiIcons.Cyan);

            // 뽑기 버튼 가격 줄("N 코어")
            foreach (var buttonName in new[] { "BtnPullSingle", "BtnPullTen" })
            {
                var button = canvas.Find(buttonName);
                if (button == null) continue;
                foreach (var text in button.GetComponentsInChildren<Text>(true))
                {
                    if (!text.text.Contains("코어")) continue;
                    // 열 군데 중 파란 버튼(채워진 버튼)에서는 어두운 아이콘이 읽기 쉽다.
                    var fill = button.GetComponent<Image>();
                    float luminance = fill != null ? fill.color.r * 0.299f + fill.color.g * 0.587f + fill.color.b * 0.114f : 0f;
                    AddPrefixIcon(text, LoadSprite("icon_core"), luminance > 0.5f ? UiIcons.Dark : UiIcons.Cyan, Mathf.Round(text.fontSize * TextIconScale), 8f);
                }
            }

            // 뽑기 결과 카드 견본: 드론 이름 왼쪽 종류 아이콘 (그림은 코드가 정한다)
            var nameText = GetText(canvas, "ResultPanel/CardTemplate/Face/DroneNameText");
            AddPrefixIcon(nameText, LoadSprite("icon_drone_melee"), UiIcons.DroneColor(SurvivalDrone.Drones.DroneType.Melee), 44f, 8f);
        }

        private static void ApplyInGame(Scene scene)
        {
            var panel = FindByName(scene, "LevelUpPanel");
            if (panel == null) return;

            var icons = new List<Image>();
            for (int i = 0; i < 3; i++)
            {
                var title = GetText(panel, $"Option{i}/Title");
                var icon = AddPrefixIcon(title, LoadSprite("icon_drone_melee"), UiIcons.DroneColor(SurvivalDrone.Drones.DroneType.Melee), 34f, 8f);
                icons.Add(icon);
            }

            // LevelUpUI의 아이콘 칸에 세 개를 연결한다.
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var comp in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (comp == null || comp.GetType().Name != "LevelUpUI") continue;
                    var so = new SerializedObject(comp);
                    var prop = so.FindProperty("optionIcons");
                    if (prop == null) continue;
                    prop.arraySize = icons.Count;
                    for (int k = 0; k < icons.Count; k++) prop.GetArrayElementAtIndex(k).objectReferenceValue = icons[k];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(comp);
                }
            }
        }
    }
}
