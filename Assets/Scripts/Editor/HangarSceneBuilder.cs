using SurvivalDrone.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static SurvivalDrone.EditorTools.SceneUIKit;

namespace SurvivalDrone.EditorTools
{
    // 격납고 씬(Assets/Scenes/Hangar.unity)을 코드로 만들어주는 에디터 전용 도구.
    // 메뉴 SurvivalDrone → Build → Create Hangar Scene 을 누르면 격납고 씬을 처음부터 다시 만들어 저장한다.
    // (로비·뽑기 씬 빌더와 같은 방식. 다시 실행하면 씬이 덮어써지니 손으로 고쳤다면 먼저 커밋해두자.)
    //
    // 화면 배치 (1920x1080 기준, 가운데가 기준점):
    //   위: 뒤로 버튼 / 제목 / 재화
    //   왼쪽 패널: 드론 목록 (런타임에 견본 줄을 복제해서 5줄을 만든다)
    //   오른쪽 패널: 고른 드론의 상세 (이름, 등급·레벨, 전투력, 성능 배율, 조각 진행바, 강화 버튼, 조각 교환 버튼)
    //   아래: 내 전투력 + 출격 장착 슬롯 2개
    public static class HangarSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Hangar.unity";

        [MenuItem("SurvivalDrone/Build/Create Hangar Scene")]
        public static void Build()
        {
            var root = CreateCanvasScene(out var scene);
            if (root == null) return;

            // ---- 상단 바 ----
            var topBar = NewRect("TopBar", root);
            Stretch(topBar);
            var creditText = NewText("CreditText", topBar, "크레딧  0", 34, LightText, SansFont, TextAnchor.MiddleRight);
            Place(creditText.rectTransform, TopRight, TopRight, TopRight, new Vector2(-380f, -30f), new Vector2(320f, 60f));
            var coreText = NewText("CoreText", topBar, "코어  0", 34, Cyan, SansFont, TextAnchor.MiddleRight);
            Place(coreText.rectTransform, TopRight, TopRight, TopRight, new Vector2(-40f, -30f), new Vector2(320f, 60f));
            var title = NewText("TitleText", topBar, "격납고", 44, LightText, MonoFont, TextAnchor.MiddleCenter);
            Place(title.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -28f), new Vector2(600f, 64f));
            var back = NewButton("BtnBack", root, "<  뒤로", 30, ButtonColor, LightText);
            Place(back.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(40f, -30f), new Vector2(220f, 64f));

            // ---- 왼쪽: 드론 목록 ----
            var listPanel = NewImage("ListPanel", root, PanelColor);
            Place(listPanel.rectTransform, Center, Center, Center, new Vector2(-590f, 30f), new Vector2(620f, 580f));
            AddOutline(listPanel.gameObject, OutlineColor);

            var rowContainer = NewRect("RowContainer", listPanel.transform);
            Stretch(rowContainer);
            var layout = rowContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 20, 20);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // 줄 견본: 꺼 둔 채로 두고, 런타임에 드론 종류 수만큼 복제한다.
            var rowTemplate = NewImage("RowTemplate", listPanel.transform, ButtonColor);
            Place(rowTemplate.rectTransform, Center, Center, Center, Vector2.zero, new Vector2(588f, 100f));
            AddOutline(rowTemplate.gameObject, OutlineColor);
            rowTemplate.gameObject.AddComponent<Button>().targetGraphic = rowTemplate;
            var rowName = NewText("NameText", rowTemplate.transform, "근접 드론", 36, LightText, SansFont, TextAnchor.MiddleLeft);
            Place(rowName.rectTransform, Center, Center, Center, new Vector2(-110f, 14f), new Vector2(340f, 48f));
            var rowTag = NewText("TagText", rowTemplate.transform, "", 24, Cyan, SansFont, TextAnchor.MiddleLeft);
            Place(rowTag.rectTransform, Center, Center, Center, new Vector2(-110f, -24f), new Vector2(340f, 34f));
            var rowInfo = NewText("InfoText", rowTemplate.transform, "N   Lv1", 38, LightText, MonoFont, TextAnchor.MiddleRight);
            Place(rowInfo.rectTransform, Center, Center, Center, new Vector2(170f, 0f), new Vector2(220f, 60f));
            rowTemplate.gameObject.SetActive(false);

            // ---- 오른쪽: 상세 ----
            var detail = NewImage("DetailPanel", root, PanelColor);
            Place(detail.rectTransform, Center, Center, Center, new Vector2(330f, 30f), new Vector2(1140f, 580f));
            AddOutline(detail.gameObject, OutlineColor);

            var nameText = NewText("NameText", detail.transform, "근접 드론", 62, LightText, SansFont, TextAnchor.MiddleCenter);
            Place(nameText.rectTransform, Center, Center, Center, new Vector2(0f, 230f), new Vector2(900f, 80f));
            var rarityLevel = NewText("RarityLevelText", detail.transform, "SR   Lv3 / 5", 42, LightText, MonoFont, TextAnchor.MiddleCenter);
            Place(rarityLevel.rectTransform, Center, Center, Center, new Vector2(0f, 165f), new Vector2(900f, 56f));
            var power = NewText("PowerText", detail.transform, "전투력", 36, LightText, SansFont, TextAnchor.MiddleCenter);
            Place(power.rectTransform, Center, Center, Center, new Vector2(0f, 105f), new Vector2(1000f, 50f));
            var multiplier = NewText("MultiplierText", detail.transform, "성능", 28, MutedText, SansFont, TextAnchor.MiddleCenter);
            Place(multiplier.rectTransform, Center, Center, Center, new Vector2(0f, 60f), new Vector2(1000f, 40f));

            var shards = NewText("ShardsText", detail.transform, "조각", 32, LightText, SansFont, TextAnchor.MiddleCenter);
            Place(shards.rectTransform, Center, Center, Center, new Vector2(0f, -5f), new Vector2(900f, 44f));
            var shardBar = NewImage("ShardBar", detail.transform, ButtonColor);
            Place(shardBar.rectTransform, Center, Center, Center, new Vector2(0f, -45f), new Vector2(760f, 18f));
            AddOutline(shardBar.gameObject, OutlineColor);
            var shardFill = NewImage("Fill", shardBar.transform, Cyan);
            shardFill.rectTransform.anchorMin = Vector2.zero;
            shardFill.rectTransform.anchorMax = new Vector2(0f, 1f); // 가로 끝 위치(anchorMax.x)를 실행 중에 바꿔서 진행바를 채운다
            shardFill.rectTransform.offsetMin = Vector2.zero;
            shardFill.rectTransform.offsetMax = Vector2.zero;

            var cost = NewText("CostText", detail.transform, "크레딧", 32, LightText, SansFont, TextAnchor.MiddleCenter);
            Place(cost.rectTransform, Center, Center, Center, new Vector2(0f, -95f), new Vector2(900f, 44f));
            var upgrade = NewButton("BtnUpgrade", detail.transform, "강화", 42, Cyan, DarkText);
            Place(upgrade.rectTransform, Center, Center, Center, new Vector2(-250f, -170f), new Vector2(420f, 88f));
            // 강화 버튼 오른쪽: 크레딧으로 조각을 사는 버튼 (글자는 HangarUI가 받는 조각·크레딧·오늘 횟수로 채운다)
            var exchange = NewButton("BtnExchange", detail.transform, "조각 +10 교환\n크레딧 500  (오늘 0/3)", 28, ButtonColor, LightText);
            Place(exchange.rectTransform, Center, Center, Center, new Vector2(250f, -170f), new Vector2(460f, 88f));
            var status = NewText("StatusText", detail.transform, "", 28, Cyan, SansFont, TextAnchor.MiddleCenter);
            Place(status.rectTransform, Center, Center, Center, new Vector2(0f, -240f), new Vector2(1080f, 40f));

            // ---- 아래: 내 전투력 + 출격 장착 슬롯 ----
            var totalPower = NewText("TotalPowerText", root, "내 전투력  0", 36, Cyan, SansFont, TextAnchor.MiddleCenter);
            Place(totalPower.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 180f), new Vector2(800f, 50f));
            var slot1 = NewButton("BtnSlot1", root, "슬롯 1\n비어 있음", 30, ButtonColor, LightText);
            Place(slot1.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(-260f, 50f), new Vector2(480f, 110f));
            var slot2 = NewButton("BtnSlot2", root, "슬롯 2\n비어 있음", 30, ButtonColor, LightText);
            Place(slot2.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(260f, 50f), new Vector2(480f, 110f));

            // ---- 격납고 컨트롤러 ----
            var hangarUI = root.gameObject.AddComponent<HangarUI>();
            SetClickSound(hangarUI);

            SaveAndRegister(scene, ScenePath);
            Debug.Log($"[Inventory] 격납고 씬을 만들어 저장했습니다: {ScenePath} ({System.DateTime.Now:HH:mm:ss})");
        }
    }
}
