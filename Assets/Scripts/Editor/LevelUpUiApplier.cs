using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.LevelUp;
using SurvivalDrone.UI;

namespace SurvivalDrone.EditorTools
{
    // 레벨업 선택 화면(InGame 씬의 LevelUpPanel)을 새 디자인으로 바꿔 주는 에디터 도구. 여러 번 실행해도 안전하다.
    //
    // 새 카드 구성 (카드 한 장 320x440, 위에서 아래로):
    //   Tag(종류 라벨) + KeyHint(키 안내) → BigIcon(큰 아이콘) → Title(이름) → Desc(설명) → Divider(가는 선) → Change(무엇이 바뀌는지)
    // 화면 위쪽에는 HeaderText("레벨 업!")와 SubHeaderText(안내 글자)가 생긴다.
    // 카드에는 마우스를 올리면 밝아지는 LevelUpCardHover가 붙고, 기존 제목 옆 작은 아이콘(TextIconPrefix)은 큰 아이콘으로 대체된다.
    //
    // 메뉴: SurvivalDrone/UI/Apply Level Up Redesign
    public static class LevelUpUiApplier
    {
        private const string ScenePath = "Assets/Scenes/InGame.unity";

        private static readonly Color Light = new Color(0.890f, 0.961f, 0.961f);
        private static readonly Color Muted = new Color(0.588f, 0.706f, 0.714f);
        private static readonly Color Cyan = new Color(0.310f, 0.847f, 0.910f);

        [MenuItem("SurvivalDrone/UI/Apply Level Up Redesign")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[LevelUpUiApplier] 플레이 모드 중에는 쓸 수 없습니다.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Transform panel = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "LevelUpPanel") panel = t;
            if (panel == null)
            {
                Debug.LogWarning("[LevelUpUiApplier] LevelUpPanel을 찾지 못했습니다.");
                EditorSceneManager.CloseScene(scene, true);
                return;
            }

            var font = panel.Find("Option0/Title").GetComponent<Text>().font;

            // 화면 위쪽 제목
            var header = EnsureText(panel, "HeaderText", font, 64, Cyan, TextAnchor.MiddleCenter, new Vector2(0f, 310f), new Vector2(1000f, 90f), "레벨 업!");
            var subHeader = EnsureText(panel, "SubHeaderText", font, 26, Muted, TextAnchor.MiddleCenter, new Vector2(0f, 245f), new Vector2(1000f, 40f), "하나를 골라 주세요");

            var tags = new Text[3];
            var changes = new Text[3];
            var keys = new Text[3];
            var bigIcons = new Image[3];
            float[] xs = { -370f, 0f, 370f };

            for (int i = 0; i < 3; i++)
            {
                var card = panel.Find("Option" + i);
                if (card == null) continue;

                var rect = card.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(xs[i], -20f);
                rect.sizeDelta = new Vector2(320f, 440f);

                // 마우스를 올렸을 때 모양이 겹쳐 변하지 않도록 버튼의 기본 색 전환은 끄고, 대신 LevelUpCardHover를 붙인다.
                var button = card.GetComponent<Button>();
                if (button != null) button.transition = Selectable.Transition.None;
                if (card.GetComponent<LevelUpCardHover>() == null) card.gameObject.AddComponent<LevelUpCardHover>();

                // 예전 제목 옆 작은 아이콘 방식은 큰 아이콘으로 대체한다.
                var title = card.Find("Title");
                var oldPrefix = title.GetComponent<TextIconPrefix>();
                if (oldPrefix != null) Object.DestroyImmediate(oldPrefix);
                var oldIcon = title.Find("Icon");
                if (oldIcon != null) Object.DestroyImmediate(oldIcon.gameObject);

                SetRect(title, new Vector2(0f, 30f), new Vector2(290f, 50f));
                var titleText = title.GetComponent<Text>();
                titleText.fontSize = 34;
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
                titleText.raycastTarget = false;

                var desc = card.Find("Desc");
                SetRect(desc, new Vector2(0f, -60f), new Vector2(280f, 110f));
                var descText = desc.GetComponent<Text>();
                descText.fontSize = 22;
                descText.alignment = TextAnchor.UpperCenter;
                descText.horizontalOverflow = HorizontalWrapMode.Wrap;
                descText.verticalOverflow = VerticalWrapMode.Overflow;
                descText.lineSpacing = 1.1f;
                descText.raycastTarget = false;

                tags[i] = EnsureText(card, "Tag", font, 22, Light, TextAnchor.MiddleCenter, new Vector2(0f, 196f), new Vector2(200f, 30f), "NEW");
                keys[i] = EnsureText(card, "KeyHint", font, 20, Muted, TextAnchor.MiddleRight, new Vector2(118f, 196f), new Vector2(60f, 30f), "[" + (i + 1) + "]");
                changes[i] = EnsureText(card, "Change", font, 32, Cyan, TextAnchor.MiddleCenter, new Vector2(0f, -175f), new Vector2(290f, 50f), "Lv 1 → 2");

                var iconTransform = card.Find("BigIcon");
                if (iconTransform == null)
                {
                    var go = new GameObject("BigIcon", typeof(RectTransform));
                    go.transform.SetParent(card, false);
                    iconTransform = go.transform;
                    go.AddComponent<Image>();
                }
                SetRect(iconTransform, new Vector2(0f, 110f), new Vector2(100f, 100f));
                bigIcons[i] = iconTransform.GetComponent<Image>();
                bigIcons[i].raycastTarget = false;
                bigIcons[i].preserveAspect = true;

                var divider = card.Find("Divider");
                if (divider == null)
                {
                    var go = new GameObject("Divider", typeof(RectTransform));
                    go.transform.SetParent(card, false);
                    divider = go.transform;
                    go.AddComponent<Image>();
                }
                SetRect(divider, new Vector2(0f, -132f), new Vector2(250f, 2f));
                var dividerImage = divider.GetComponent<Image>();
                dividerImage.color = new Color(0.31f, 0.42f, 0.52f, 0.5f);
                dividerImage.raycastTarget = false;
            }

            // LevelUpUI에 새 칸들을 연결한다.
            LevelUpUI ui = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<LevelUpUI>(true);
                if (found != null) ui = found;
            }
            if (ui != null)
            {
                var so = new SerializedObject(ui);
                SetArray(so, "optionTags", tags);
                SetArray(so, "optionChanges", changes);
                SetArray(so, "optionKeyHints", keys);
                SetArray(so, "optionIcons", bigIcons);
                so.FindProperty("headerText").objectReferenceValue = header;
                so.FindProperty("subHeaderText").objectReferenceValue = subHeader;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ui);
            }
            else Debug.LogWarning("[LevelUpUiApplier] LevelUpUI를 찾지 못해 칸을 연결하지 못했습니다.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[LevelUpUiApplier] 레벨업 선택 화면을 새 디자인으로 바꿨습니다.");
        }

        private static void SetRect(Transform t, Vector2 pos, Vector2 size)
        {
            var rect = t.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private static Text EnsureText(Transform parent, string name, Font font, int size, Color color, TextAnchor align, Vector2 pos, Vector2 sizeDelta, string content)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                go.AddComponent<Text>();
            }
            SetRect(go.transform, pos, sizeDelta);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static void SetArray<T>(SerializedObject so, string property, T[] items) where T : Object
        {
            var prop = so.FindProperty(property);
            prop.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }
}
