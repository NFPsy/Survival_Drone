using SurvivalDrone.Meta;
using SurvivalDrone.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static SurvivalDrone.EditorTools.SceneUIKit;

namespace SurvivalDrone.EditorTools
{
    // 뽑기 씬(Assets/Scenes/Gacha.unity)을 코드로 만들어주는 에디터 전용 도구.
    // 메뉴 SurvivalDrone → Build → Create Gacha Scene 을 누르면 뽑기 씬을 처음부터 다시 만들어 저장한다.
    // (로비 씬 빌더와 같은 방식. 다시 실행하면 씬이 덮어써지니 손으로 고쳤다면 먼저 커밋해두자.)
    //
    // 화면 배치 (1920x1080 기준, 가운데가 기준점):
    //   위: 뒤로 버튼 / 코어 잔액
    //   배너 → 등급 확률 요약(+ 확률 보기) → 천장 게이지 → 아래: 1회 / 10연 버튼
    //   팝업 2개(확률 공개, 코어 부족)와 뽑기 결과 화면은 평소엔 꺼져 있다.
    public static class GachaSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Gacha.unity";

        [MenuItem("SurvivalDrone/Build/Create Gacha Scene")]
        public static void Build()
        {
            var root = CreateCanvasScene(out var scene);
            if (root == null) return;

            // ---- 상단 바: 뒤로 / 코어 ----
            var topBar = NewRect("TopBar", root);
            Stretch(topBar);
            var coreText = NewText("CoreText", topBar, "코어  0", 36, Cyan, SansFont, TextAnchor.MiddleRight);
            Place(coreText.rectTransform, TopRight, TopRight, TopRight, new Vector2(-40f, -30f), new Vector2(420f, 60f));
            var back = NewButton("BtnBack", root, "<  뒤로", 30, ButtonColor, LightText);
            Place(back.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(40f, -30f), new Vector2(220f, 64f));

            // ---- 배너: 무엇을 뽑는 화면인지 ----
            var banner = NewImage("Banner", root, PanelColor);
            Place(banner.rectTransform, Center, Center, Center, new Vector2(0f, 230f), new Vector2(1000f, 200f));
            AddOutline(banner.gameObject, OutlineColor);
            var bannerTitle = NewText("TitleText", banner.transform, "드론 설계도 뽑기", 60, LightText, SansFont, TextAnchor.MiddleCenter);
            Place(bannerTitle.rectTransform, Center, Center, Center, new Vector2(0f, 30f), new Vector2(900f, 90f));
            var bannerSub = NewText("SubtitleText", banner.transform, "근접 · 저격 · 수집 · 폭발 · 회복 드론 설계도", 30, MutedText, SansFont, TextAnchor.MiddleCenter);
            Place(bannerSub.rectTransform, Center, Center, Center, new Vector2(0f, -45f), new Vector2(900f, 50f));

            // ---- 등급 확률 요약 (숫자는 실행할 때 GachaTable에서 읽어 채운다) ----
            var ratesPanel = NewImage("RatesPanel", root, PanelColor);
            Place(ratesPanel.rectTransform, Center, Center, Center, new Vector2(0f, 40f), new Vector2(1000f, 170f));
            AddOutline(ratesPanel.gameObject, OutlineColor);
            var ratesText = NewText("RatesText", ratesPanel.transform, "SSR", 30, LightText, SansFont, TextAnchor.MiddleLeft);
            ratesText.supportRichText = true;
            ratesText.lineSpacing = 0.95f;
            Place(ratesText.rectTransform, Center, Center, Center, new Vector2(-250f, 0f), new Vector2(400f, 150f));
            var ratesButton = NewButton("BtnRates", ratesPanel.transform, "확률 보기", 30, ButtonColor, LightText);
            Place(ratesButton.rectTransform, Center, Center, Center, new Vector2(300f, 0f), new Vector2(300f, 70f));

            // ---- 천장 게이지 ----
            var pityText = NewText("PityText", root, "SSR 확정까지    누적 0 / 70", 32, RarityColors.Gold, SansFont, TextAnchor.MiddleCenter);
            Place(pityText.rectTransform, Center, Center, Center, new Vector2(0f, -80f), new Vector2(1000f, 44f));
            var pityBar = NewImage("PityBar", root, ButtonColor);
            Place(pityBar.rectTransform, Center, Center, Center, new Vector2(0f, -122f), new Vector2(1000f, 18f));
            AddOutline(pityBar.gameObject, OutlineColor);
            var pityFill = NewImage("Fill", pityBar.transform, RarityColors.Gold);
            pityFill.rectTransform.anchorMin = Vector2.zero;
            pityFill.rectTransform.anchorMax = new Vector2(0f, 1f); // 가로 끝 위치(anchorMax.x)를 실행 중에 바꿔서 게이지를 채운다
            pityFill.rectTransform.offsetMin = Vector2.zero;
            pityFill.rectTransform.offsetMax = Vector2.zero;

            // ---- 뽑기 버튼 ----
            // 1회 뽑기는 보조 버튼(어두운 색), 10연은 할인이 있어 권하는 주요 버튼(시안색)으로 구분한다.
            var single = NewButton("BtnPullSingle", root, "", 40, ButtonColor, LightText);
            Place(single.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(-260f, 60f), new Vector2(440f, 120f));
            FillPullButton(single.transform, "1회 뽑기", LightText, MutedText);

            var ten = NewButton("BtnPullTen", root, "", 40, Cyan, DarkText);
            Place(ten.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(260f, 60f), new Vector2(440f, 120f));
            FillPullButton(ten.transform, "10연 뽑기", DarkText, DarkText);
            var badge = NewImage("BadgeBack", ten.transform, RarityColors.Gold);
            badge.raycastTarget = false; // 뱃지가 버튼 클릭을 가로채지 않게 한다
            Place(badge.rectTransform, TopRight, TopRight, Center, new Vector2(-90f, 0f), new Vector2(150f, 40f));
            var badgeText = NewText("DiscountBadge", badge.transform, "10% 할인", 24, DarkText, SansFont, TextAnchor.MiddleCenter);
            Stretch(badgeText.rectTransform);

            // ---- 팝업 (평소엔 꺼져 있음) ----
            var probBox = NewPopup("ProbabilityPopup", root, new Vector2(900f, 760f), out var probRoot);
            var probTitle = NewText("TitleText", probBox, "뽑기 확률 정보", 44, LightText, MonoFont, TextAnchor.MiddleCenter);
            Place(probTitle.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -40f), new Vector2(800f, 70f));
            var probRates = NewText("RatesText", probBox, "SSR", 44, LightText, SansFont, TextAnchor.MiddleLeft);
            probRates.supportRichText = true;
            Place(probRates.rectTransform, Center, Center, Center, new Vector2(0f, 140f), new Vector2(360f, 260f));
            var probRules = NewText("RulesText", probBox, "규칙", 30, MutedText, SansFont, TextAnchor.UpperLeft);
            probRules.lineSpacing = 1.3f;
            Place(probRules.rectTransform, Center, Center, Center, new Vector2(0f, -120f), new Vector2(760f, 160f));
            var probClose = NewButton("BtnClose", probBox, "닫기", 32, ButtonColor, LightText);
            Place(probClose.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 40f), new Vector2(280f, 72f));
            var probabilityPopup = probRoot.gameObject.AddComponent<ProbabilityPopup>();
            probRoot.gameObject.SetActive(false);

            var lackBox = NewPopup("InsufficientPopup", root, new Vector2(640f, 300f), out var lackRoot);
            var lackMessage = NewText("MessageText", lackBox, "코어가 부족합니다", 40, LightText, SansFont, TextAnchor.MiddleCenter);
            Place(lackMessage.rectTransform, Center, Center, Center, new Vector2(0f, 40f), new Vector2(560f, 80f));
            var lackOk = NewButton("BtnOk", lackBox, "확인", 32, Cyan, DarkText);
            Place(lackOk.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, 36f), new Vector2(240f, 68f));
            lackRoot.gameObject.SetActive(false);

            // ---- 뽑기 결과 화면 (뽑기가 성공하면 화면 전체를 덮으며 나타난다) ----
            var resultPanel = NewImage("ResultPanel", root, new Color(0.03f, 0.04f, 0.06f, 1f)); // 완전 불투명: 뒤의 뽑기 화면이 비쳐 보이지 않게
            Stretch(resultPanel.rectTransform);
            var resultTitle = NewText("TitleText", resultPanel.transform, "뽑기 결과", 44, LightText, MonoFont, TextAnchor.MiddleCenter);
            Place(resultTitle.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -30f), new Vector2(800f, 70f));

            var resultContainer = NewRect("CardContainer", resultPanel.transform);
            Place(resultContainer, Center, Center, Center, new Vector2(0f, 5f), new Vector2(1500f, 600f));
            var grid = resultContainer.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(270f, 280f);
            grid.spacing = new Vector2(24f, 24f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

            // 카드 견본: 꺼 둔 채로 두고, 뽑기 결과가 나올 때마다 장 수만큼 복제한다.
            var cardTemplate = NewImage("CardTemplate", resultPanel.transform, ButtonColor);
            Place(cardTemplate.rectTransform, Center, Center, Center, Vector2.zero, new Vector2(270f, 280f));
            AddOutline(cardTemplate.gameObject, OutlineColor);
            var cardBack = NewText("Back", cardTemplate.transform, "?", 110, MutedText, MonoFont, TextAnchor.MiddleCenter);
            Stretch(cardBack.rectTransform);
            var cardFace = NewRect("Face", cardTemplate.transform);
            Stretch(cardFace);
            var rarityText = NewText("RarityText", cardFace, "SSR", 76, RarityColors.Gold, MonoFont, TextAnchor.MiddleCenter);
            Place(rarityText.rectTransform, Center, Center, Center, new Vector2(0f, 60f), new Vector2(250f, 90f));
            var droneNameText = NewText("DroneNameText", cardFace, "근접 드론", 38, LightText, SansFont, TextAnchor.MiddleCenter);
            Place(droneNameText.rectTransform, Center, Center, Center, new Vector2(0f, -20f), new Vector2(250f, 50f));
            var tagText = NewText("TagText", cardFace, "NEW", 28, Cyan, SansFont, TextAnchor.MiddleCenter);
            Place(tagText.rectTransform, Center, Center, Center, new Vector2(0f, -85f), new Vector2(250f, 44f));
            cardTemplate.gameObject.SetActive(false);

            var skip = NewButton("BtnSkip", resultPanel.transform, "스킵  >>", 30, ButtonColor, LightText);
            Place(skip.rectTransform, TopRight, TopRight, TopRight, new Vector2(-40f, -30f), new Vector2(220f, 64f));

            var confirm = NewButton("BtnConfirm", resultPanel.transform, "확인", 38, ButtonColor, LightText);
            Place(confirm.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(-230f, 40f), new Vector2(380f, 96f));
            var again = NewButton("BtnAgain", resultPanel.transform, "", 40, Cyan, DarkText);
            Place(again.rectTransform, BottomCenter, BottomCenter, BottomCenter, new Vector2(230f, 40f), new Vector2(380f, 96f));
            var againDefaultText = again.transform.Find("Text");
            if (againDefaultText != null) Object.DestroyImmediate(againDefaultText.gameObject);
            var againLabel = NewText("LabelText", again.transform, "10연 다시 뽑기", 34, DarkText, SansFont, TextAnchor.MiddleCenter);
            Place(againLabel.rectTransform, Center, Center, Center, new Vector2(0f, 16f), new Vector2(360f, 44f));
            var againPrice = NewText("PriceText", again.transform, "0 코어", 28, DarkText, SansFont, TextAnchor.MiddleCenter);
            Place(againPrice.rectTransform, Center, Center, Center, new Vector2(0f, -22f), new Vector2(360f, 40f));

            // SSR이 공개되는 순간 화면 전체를 금색으로 번쩍이게 하는 얇은 막 (평소엔 완전 투명, 클릭도 막지 않는다)
            var flash = NewImage("Flash", resultPanel.transform, new Color(RarityColors.Gold.r, RarityColors.Gold.g, RarityColors.Gold.b, 0f));
            Stretch(flash.rectTransform);
            flash.raycastTarget = false;

            var resultUI = resultPanel.gameObject.AddComponent<GachaResultUI>();
            resultPanel.gameObject.SetActive(false);

            // ---- 뽑기 화면 컨트롤러 ----
            var gachaUI = root.gameObject.AddComponent<GachaUI>();
            SetClickSound(gachaUI);
            SetClickSound(probabilityPopup);
            SetClickSound(resultUI);

            SaveAndRegister(scene, ScenePath);
            Debug.Log($"[Gacha] 뽑기 씬을 만들어 저장했습니다: {ScenePath} ({System.DateTime.Now:HH:mm:ss})");
        }

        // 뽑기 버튼 안에 "이름"과 "가격" 두 줄을 넣는다. 가격 글자 색은 코어가 모자랄 때 실행 중에 빨갛게 바뀐다.
        private static void FillPullButton(Transform button, string label, Color labelColor, Color priceColor)
        {
            // NewButton이 기본으로 만든 가운데 글자는 쓰지 않고 지운다.
            var defaultText = button.Find("Text");
            if (defaultText != null) Object.DestroyImmediate(defaultText.gameObject);

            var labelText = NewText("LabelText", button, label, 38, labelColor, SansFont, TextAnchor.MiddleCenter);
            Place(labelText.rectTransform, Center, Center, Center, new Vector2(0f, 20f), new Vector2(420f, 50f));
            var priceText = NewText("PriceText", button, "0 코어", 30, priceColor, SansFont, TextAnchor.MiddleCenter);
            Place(priceText.rectTransform, Center, Center, Center, new Vector2(0f, -28f), new Vector2(420f, 44f));
        }
    }
}
