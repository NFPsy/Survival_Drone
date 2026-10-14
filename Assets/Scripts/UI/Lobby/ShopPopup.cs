using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 가상 상점 화면 (로비 위에 뜨는 팝업). OBT 2단계 기능이라 ShopSettings 스위치가 켜졌을 때만 로비가 만든다.
    //
    // 실제 결제는 구현하지 않는다. 목적은 "어떤 상품에 관심을 보이는지"를 데이터로 보는 것이라서:
    //  - 상점 화면 맨 위에는 항상 "데모 상점 — 실제 결제는 이루어지지 않습니다"가 보이고,
    //  - 상품 카드를 누르면 결제 대신 "데모입니다" 안내 창이 뜨며,
    //  - 상점을 연 것(shop_open), 상품을 누른 것(shop_click), 안내 창을 확인한 것(shop_demo_popup)이 테스트 로그에 남는다.
    //
    // 화면은 씬 파일에 미리 만들어 두지 않고 코드로 만든다. 스위치가 꺼져 있는 동안은 로비 씬을 전혀 건드리지 않기 위해서다.
    // 색·글꼴은 다른 로비 화면(마일스톤·일일 퀘스트 창)과 같은 규칙을 따른다.
    public class ShopPopup : MonoBehaviour
    {
        // 로비 화면 전체에서 쓰는 색 (SceneUIKit과 같은 값)
        private static readonly Color PanelColor = new Color(0.063f, 0.078f, 0.110f, 0.97f);
        private static readonly Color ButtonColor = new Color(0.086f, 0.106f, 0.149f, 1f);
        private static readonly Color Cyan = new Color(0.310f, 0.847f, 0.910f, 1f);
        private static readonly Color OutlineColor = new Color(0.300f, 0.360f, 0.460f, 0.9f);
        private static readonly Color LightText = new Color(0.890f, 0.961f, 0.961f, 1f);
        private static readonly Color MutedText = new Color(0.588f, 0.706f, 0.714f, 1f);
        private static readonly Color DarkText = new Color(0.043f, 0.055f, 0.078f, 1f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color DemoColor = new Color(1f, 0.62f, 0.2f, 1f);   // 주황: "데모" 표시

        // 구매 버튼을 눌렀을 때 보여줄 안내 문구 (노션 "상점 설계" 4절 초안).
        private const string DemoMessage = "이 상점은 데모입니다. 실제 결제는 이루어지지 않으며, 카드나 계정 정보도 받지 않습니다. 어떤 상품에 관심이 있었는지는 가격 설계에 참고합니다.";

        private AudioClip _clickSound;
        private GameObject _demoPopup;
        private Text _demoProductText;
        private ShopProduct _lastProduct;

        // 상점 화면 전체를 만들어 돌려준다. 만들어진 직후에는 꺼져 있고 Open()으로 연다.
        //  parent = 로비 캔버스, font = 로비의 다른 글자와 같은 글꼴, products = 상품 목록
        public static ShopPopup Create(Transform parent, Font font, ShopProduct[] products, AudioClip clickSound)
        {
            var rootImage = NewImage("ShopPopup", parent, DimColor);
            Stretch(rootImage.rectTransform);
            var popup = rootImage.gameObject.AddComponent<ShopPopup>();
            popup._clickSound = clickSound;

            // 상자
            var box = NewImage("Box", rootImage.transform, PanelColor);
            Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1780f, 980f));
            AddOutline(box.gameObject, OutlineColor);
            box.gameObject.AddComponent<UIPopupAnimator>();

            var title = NewText(box.transform, "TitleText", "상점", 46, LightText, TextAnchor.MiddleCenter, font);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(800f, 66f));

            // "데모" 안내는 상점을 열어 둔 동안 항상 보인다.
            var banner = NewText(box.transform, "DemoBannerText", "데모 상점 — 실제 결제는 이루어지지 않습니다", 30, DemoColor, TextAnchor.MiddleCenter, font);
            Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(1200f, 44f));

            // 상품 카드: 한 줄에 5장씩 격자로 놓는다.
            var container = new GameObject("ProductGrid", typeof(RectTransform));
            container.transform.SetParent(box.transform, false);
            var containerRect = container.GetComponent<RectTransform>();
            Place(containerRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(1700f, 700f));
            var grid = container.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(320f, 330f);
            grid.spacing = new Vector2(24f, 24f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperCenter;

            for (int i = 0; i < products.Length; i++)
            {
                if (products[i] == null) continue;
                popup.BuildCard(container.transform, products[i], font);
            }

            var close = NewButton(box.transform, "BtnClose", "닫기", 32, ButtonColor, LightText, font);
            Place(close.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(300f, 76f));
            close.GetComponent<Button>().onClick.AddListener(popup.Close);

            popup.BuildDemoPopup(rootImage.transform, font);

            popup.transform.SetAsLastSibling();   // 다른 로비 창보다 위에 그려지게 한다
            popup.gameObject.SetActive(false);
            return popup;
        }

        // 로비 오른쪽 아래에 놓는 "상점" 버튼을 만든다. 모양은 다른 로비 버튼과 같다.
        public static Button CreateLobbyButton(Transform parent, Font font, string label, Vector2 anchoredPosition, Vector2 size)
        {
            var image = NewButton(parent, "BtnShop", label, 30, ButtonColor, LightText, font);
            Place(image.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), anchoredPosition, size);
            return image.GetComponent<Button>();
        }

        // ---------------- 열기/닫기 ----------------

        public void Open()
        {
            gameObject.SetActive(true);
            _demoPopup.SetActive(false);
            transform.SetAsLastSibling();

            // 메인 메뉴를 거치지 않고 로비만 실행한 경우(저장 데이터 없음)에는 기록을 남기지 않는다.
            if (CurrencyManager.Instance != null)
            {
                PlayLog.RecordShopOpen(SaveManager.Data, CurrencyManager.Instance.Core);
                SaveManager.Save();
            }
        }

        private void Close()
        {
            AudioManager.Instance?.PlaySfx(_clickSound);
            gameObject.SetActive(false);
        }

        // ---------------- 카드 / 안내 창 ----------------

        private void BuildCard(Transform parent, ShopProduct product, Font font)
        {
            var card = NewImage("Card_" + product.displayName, parent, ButtonColor);
            AddOutline(card.gameObject, OutlineColor);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.onClick.AddListener(() => OnProductClicked(product));

            var category = NewText(card.transform, "CategoryText", product.category, 24, Cyan, TextAnchor.MiddleCenter, font);
            Place(category.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(296f, 34f));

            var name = NewText(card.transform, "NameText", product.displayName, 38, LightText, TextAnchor.MiddleCenter, font);
            Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(296f, 56f));

            var contents = NewText(card.transform, "ContentsText", product.contents, 24, MutedText, TextAnchor.UpperCenter, font);
            contents.horizontalOverflow = HorizontalWrapMode.Wrap;
            contents.verticalOverflow = VerticalWrapMode.Truncate;
            Place(contents.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(284f, 84f));

            string priceLabel = product.priceWon > 0 ? $"{product.priceWon:N0}원" : "무료";
            var price = NewText(card.transform, "PriceText", priceLabel, 44, Cyan, TextAnchor.MiddleCenter, font);
            Place(price.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 78f), new Vector2(296f, 60f));

            var buy = NewImage("BuyBar", card.transform, Cyan);
            Place(buy.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(264f, 48f));
            buy.raycastTarget = false;
            var buyText = NewText(buy.transform, "Text", "구매 (데모)", 28, DarkText, TextAnchor.MiddleCenter, font);
            Stretch(buyText.rectTransform);
        }

        // 구매 버튼이 눌렸을 때: 클릭을 기록하고, 결제 대신 데모 안내 창을 띄운다.
        private void OnProductClicked(ShopProduct product)
        {
            AudioManager.Instance?.PlaySfx(_clickSound);
            _lastProduct = product;

            if (CurrencyManager.Instance != null)
            {
                PlayLog.RecordShopClick(SaveManager.Data, product.displayName, product.priceWon, CurrencyManager.Instance.Core);
                SaveManager.Save();
            }

            string price = product.priceWon > 0 ? $"{product.priceWon:N0}원" : "무료";
            _demoProductText.text = $"선택한 상품: {product.displayName} · {price}";
            _demoPopup.SetActive(true);
        }

        private void BuildDemoPopup(Transform parent, Font font)
        {
            var dim = NewImage("DemoPopup", parent, DimColor);
            Stretch(dim.rectTransform);
            _demoPopup = dim.gameObject;

            var box = NewImage("Box", dim.transform, PanelColor);
            Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 560f));
            AddOutline(box.gameObject, OutlineColor);
            box.gameObject.AddComponent<UIPopupAnimator>();

            var title = NewText(box.transform, "TitleText", "데모 상점 안내", 42, DemoColor, TextAnchor.MiddleCenter, font);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(800f, 64f));

            _demoProductText = NewText(box.transform, "ProductText", "", 34, Cyan, TextAnchor.MiddleCenter, font);
            Place(_demoProductText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(900f, 50f));

            var message = NewText(box.transform, "MessageText", DemoMessage, 30, LightText, TextAnchor.MiddleCenter, font);
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            message.verticalOverflow = VerticalWrapMode.Truncate;
            Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(860f, 200f));

            var ok = NewButton(box.transform, "BtnOk", "확인", 32, Cyan, DarkText, font);
            Place(ok.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(300f, 76f));
            ok.GetComponent<Button>().onClick.AddListener(CloseDemoPopup);

            _demoPopup.SetActive(false);
        }

        // 안내 창을 확인(닫음)했을 때: 기록을 남긴다.
        private void CloseDemoPopup()
        {
            AudioManager.Instance?.PlaySfx(_clickSound);
            _demoPopup.SetActive(false);

            if (CurrencyManager.Instance != null && _lastProduct != null)
            {
                PlayLog.RecordShopPopupClosed(SaveManager.Data, _lastProduct.displayName);
                SaveManager.Save();
            }
        }

        // ---------------- 화면 조각 만들기 도우미 (런타임용 간단 버전) ----------------

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text NewText(Transform parent, string name, string value, int size, Color color, TextAnchor anchor, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            return text;
        }

        private static Image NewButton(Transform parent, string name, string label, int size, Color fill, Color textColor, Font font)
        {
            var image = NewImage(name, parent, fill);
            image.gameObject.AddComponent<Button>().targetGraphic = image;
            AddOutline(image.gameObject, fill == Cyan ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f) : OutlineColor);
            var text = NewText(image.transform, "Text", label, size, textColor, TextAnchor.MiddleCenter, font);
            Stretch(text.rectTransform);
            return image;
        }

        private static void AddOutline(GameObject go, Color color)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, 1f);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
