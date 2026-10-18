using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 저장 코드 창 (로비 위에 뜨는 팝업). 지금 슬롯의 저장을 글자 코드로 내보내거나, 코드를 붙여 넣어서 불러온다.
    //
    // 왜 있나: 웹 게임의 저장은 브라우저 안(게임 주소별)에만 있어서, 게임을 새로 올리면 테스터의 저장이 사라질 수 있다.
    // 업데이트 전에 코드를 복사해 두었다가 새 버전에서 붙여 넣으면 이어서 할 수 있다. (서버·로그인 없음, 규칙은 SaveTransfer 참고)
    //
    // 화면 흐름:
    //  - "코드 복사하기": 코드를 클립보드에 복사한다. 웹에서 복사가 막힐 때를 대비해 "파일로 저장"도 있다.
    //  - "코드 불러오기": 붙여넣기 입력 창이 뜨고, 확인하면 코드를 검사해서 문제가 없으면
    //    "불러올 저장 / 지금 저장" 요약을 보여 주며 "지금 슬롯의 저장이 사라집니다"를 확인받은 뒤 덮어쓴다.
    //    덮어쓴 뒤에는 로비를 다시 불러서 모든 화면이 새 저장으로 바뀌게 한다.
    //
    // 화면은 씬 파일에 만들어 두지 않고 코드로 만든다. (로비 씬 파일을 건드리지 않으려고. ShopPopup과 같은 방식)
    public class SaveTransferPopup : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.063f, 0.078f, 0.110f, 0.97f);
        private static readonly Color ButtonColor = new Color(0.086f, 0.106f, 0.149f, 1f);
        private static readonly Color Cyan = new Color(0.310f, 0.847f, 0.910f, 1f);
        private static readonly Color OutlineColor = new Color(0.300f, 0.360f, 0.460f, 0.9f);
        private static readonly Color LightText = new Color(0.890f, 0.961f, 0.961f, 1f);
        private static readonly Color MutedText = new Color(0.588f, 0.706f, 0.714f, 1f);
        private static readonly Color DarkText = new Color(0.043f, 0.055f, 0.078f, 1f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color WarnColor = new Color(1f, 0.62f, 0.2f, 1f);
        private static readonly Color ErrorColor = new Color(1f, 0.35f, 0.35f, 1f);

        private const string Explain =
            "지금 슬롯의 진행 상황(코어·드론·스테이지)을 글자 코드로 바꿔 줍니다.\n" +
            "게임이 업데이트되어 저장이 사라질 때를 대비해 복사해 두었다가, 새 버전에서 \"코드 불러오기\"로 이어서 하세요.\n" +
            "테스트 기록은 코드에 들어가지 않으니 \"테스트 로그\"로 따로 보내 주세요.";

        private AudioClip _clickSound;
        private Text _statusText;
        private GameObject _confirmPanel;
        private Text _confirmText;
        private SaveData _pendingImport;
        private bool _waitingForPaste;

        // 창 전체를 만들어 돌려준다. 만들어진 직후에는 꺼져 있고 Open()으로 연다.
        public static SaveTransferPopup Create(Transform parent, Font font, AudioClip clickSound)
        {
            var rootImage = NewImage("SaveTransferPopup", parent, DimColor);
            Stretch(rootImage.rectTransform);
            var popup = rootImage.gameObject.AddComponent<SaveTransferPopup>();
            popup._clickSound = clickSound;

            var box = NewImage("Box", rootImage.transform, PanelColor);
            Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 760f));
            AddOutline(box.gameObject, OutlineColor);
            box.gameObject.AddComponent<UIPopupAnimator>();

            var title = NewText(box.transform, "TitleText", "저장 코드", 46, LightText, TextAnchor.MiddleCenter, font);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(800f, 66f));

            var explain = NewText(box.transform, "ExplainText", Explain, 29, MutedText, TextAnchor.UpperCenter, font);
            explain.horizontalOverflow = HorizontalWrapMode.Wrap;
            explain.verticalOverflow = VerticalWrapMode.Truncate;
            Place(explain.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1080f, 190f));

            var copy = NewButton(box.transform, "BtnCopy", "코드 복사하기", 32, Cyan, DarkText, font);
            Place(copy.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-330f, -30f), new Vector2(320f, 84f));
            copy.GetComponent<Button>().onClick.AddListener(popup.OnCopy);

            var save = NewButton(box.transform, "BtnSaveFile", "파일로 저장", 32, ButtonColor, LightText, font);
            Place(save.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(320f, 84f));
            save.GetComponent<Button>().onClick.AddListener(popup.OnSaveFile);

            var import = NewButton(box.transform, "BtnImport", "코드 불러오기", 32, ButtonColor, LightText, font);
            Place(import.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(330f, -30f), new Vector2(320f, 84f));
            import.GetComponent<Button>().onClick.AddListener(popup.OnImport);

            popup._statusText = NewText(box.transform, "StatusText", "", 28, Cyan, TextAnchor.MiddleCenter, font);
            popup._statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            popup._statusText.verticalOverflow = VerticalWrapMode.Truncate;
            Place(popup._statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1100f, 170f));

            var close = NewButton(box.transform, "BtnClose", "닫기", 32, ButtonColor, LightText, font);
            Place(close.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(300f, 76f));
            close.GetComponent<Button>().onClick.AddListener(popup.Close);

            popup.BuildConfirmPanel(rootImage.transform, font);

            popup.transform.SetAsLastSibling();
            popup.gameObject.SetActive(false);
            return popup;
        }

        // 로비 왼쪽 아래에 놓는 "저장 코드" 버튼 (테스트 로그 버튼과 같은 모양·크기).
        public static Button CreateLobbyButton(Transform parent, Font font, Vector2 anchoredPosition, Vector2 size)
        {
            var image = NewButton(parent, "BtnSaveTransfer", "저장 코드", 30, ButtonColor, LightText, font);
            Place(image.rectTransform, Vector2.zero, Vector2.zero, anchoredPosition, size);
            return image.GetComponent<Button>();
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _confirmPanel.SetActive(false);
            _waitingForPaste = false;
            _pendingImport = null;
            SetStatus(SaveManager.HasSelectedSlot ? "" : "저장 슬롯을 먼저 고른 뒤(메인 메뉴 → 게임 시작) 이용해 주세요.", WarnColor);
        }

        private void Close()
        {
            Click();
            gameObject.SetActive(false);
        }

        // ---------------- 내보내기 ----------------

        private void OnCopy()
        {
            Click();
            if (!CanUse()) return;

            string code = SaveTransfer.Encode(SaveManager.Data);
            BrowserBridge.CopyToClipboard(code);
            RecordExport();
            // 웹에서는 브라우저가 복사를 막았는지 게임이 알 수 없으므로, 안 되면 파일 저장을 쓰도록 함께 안내한다.
            SetStatus($"코드를 복사했습니다 ({code.Length:N0}자). 메모장 등에 붙여 넣어 보관하세요.\n붙여넣기가 안 되면 \"파일로 저장\"을 눌러 주세요.", Cyan);
        }

        private void OnSaveFile()
        {
            Click();
            if (!CanUse()) return;

            string code = SaveTransfer.Encode(SaveManager.Data);
            string fileName = $"survival-d-save-slot{Mathf.Max(1, SaveManager.CurrentSlot)}.txt";
            string result = BrowserBridge.DownloadTextFile(fileName, code);
            if (result == null)
            {
                SetStatus("파일 저장에 실패했습니다. \"코드 복사하기\"를 눌러 주세요.", ErrorColor);
                return;
            }
            RecordExport();
            SetStatus($"파일로 저장했습니다: {fileName}\n(파일을 열어서 안의 코드를 통째로 복사해 두면 됩니다)", Cyan);
        }

        private static void RecordExport()
        {
            if (CurrencyManager.Instance == null) return;
            PlayLog.RecordSaveExport(SaveManager.Data, CurrencyManager.Instance.Core);
            SaveManager.Save();
        }

        // ---------------- 불러오기 ----------------

        private void OnImport()
        {
            Click();
            if (!CanUse()) return;

            _waitingForPaste = true;
            SetStatus("붙여넣기 창에 저장 코드를 붙여 넣고 \"확인\"을 눌러 주세요.", MutedText);
            BrowserBridge.BeginPaste("저장 코드를 아래 칸에 붙여 넣고(Ctrl+V) \"확인\"을 눌러 주세요.\n\"SDSAVE1:\"로 시작하는 코드를 통째로 붙여 넣어야 합니다.");
        }

        // 붙여넣기 입력 창의 결과를 매 프레임 확인한다. (웹의 입력 창은 게임 밖 화면이라 결과를 기다려야 한다)
        private void Update()
        {
            if (!_waitingForPaste) return;

            int state = BrowserBridge.PollPaste(out string text);
            if (state == 0) return;

            _waitingForPaste = false;
            if (state == 2)
            {
                SetStatus("불러오기를 취소했습니다.", MutedText);
                return;
            }

            var result = SaveTransfer.TryDecode(text, out SaveData data);
            if (result != SaveTransfer.DecodeResult.Ok)
            {
                SetStatus(SaveTransfer.Describe(result), ErrorColor);
                return;
            }

            _pendingImport = data;
            _confirmText.text = "이 저장을 불러올까요?\n\n" +
                                $"불러올 저장:  {SaveTransfer.Summarize(data)}\n" +
                                $"지금 저장:  {SaveTransfer.Summarize(SaveManager.Data)}\n\n" +
                                "지금 슬롯의 저장은 사라지고 불러온 저장으로 바뀝니다.";
            _confirmPanel.SetActive(true);
            SetStatus("", Cyan);
        }

        private void OnConfirmImport()
        {
            Click();
            if (_pendingImport == null)
            {
                _confirmPanel.SetActive(false);
                return;
            }

            int coreBefore = CurrencyManager.Instance != null ? CurrencyManager.Instance.Core : SaveManager.Data.core;
            var data = _pendingImport;
            _pendingImport = null;
            _confirmPanel.SetActive(false);

            if (!SaveManager.ReplaceCurrentData(data))
            {
                SetStatus("불러오지 못했습니다. 저장 슬롯을 먼저 고른 뒤 이용해 주세요.", ErrorColor);
                return;
            }

            PlayLog.RecordSaveImport(SaveManager.Data, coreBefore, SaveManager.Data.core);
            SaveManager.Save();

            // 모든 화면(재화·드론·스테이지 표시)이 새 저장으로 바뀌도록 로비를 다시 불러온다.
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void OnCancelImport()
        {
            Click();
            _pendingImport = null;
            _confirmPanel.SetActive(false);
            SetStatus("불러오기를 취소했습니다.", MutedText);
        }

        private void BuildConfirmPanel(Transform parent, Font font)
        {
            var dim = NewImage("ConfirmPanel", parent, DimColor);
            Stretch(dim.rectTransform);
            _confirmPanel = dim.gameObject;

            var box = NewImage("Box", dim.transform, PanelColor);
            Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 560f));
            AddOutline(box.gameObject, OutlineColor);
            box.gameObject.AddComponent<UIPopupAnimator>();

            var title = NewText(box.transform, "TitleText", "저장 불러오기 확인", 42, WarnColor, TextAnchor.MiddleCenter, font);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(900f, 64f));

            _confirmText = NewText(box.transform, "MessageText", "", 30, LightText, TextAnchor.MiddleCenter, font);
            _confirmText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _confirmText.verticalOverflow = VerticalWrapMode.Truncate;
            Place(_confirmText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1000f, 320f));

            var ok = NewButton(box.transform, "BtnOverwrite", "덮어쓰기", 32, Cyan, DarkText, font);
            Place(ok.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170f, 40f), new Vector2(300f, 76f));
            ok.GetComponent<Button>().onClick.AddListener(OnConfirmImport);

            var cancel = NewButton(box.transform, "BtnCancel", "취소", 32, ButtonColor, LightText, font);
            Place(cancel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(170f, 40f), new Vector2(300f, 76f));
            cancel.GetComponent<Button>().onClick.AddListener(OnCancelImport);

            _confirmPanel.SetActive(false);
        }

        // ---------------- 도우미 ----------------

        // 저장할 곳(슬롯)이 정해져 있어야 내보내기·불러오기를 쓸 수 있다.
        private bool CanUse()
        {
            if (SaveManager.HasSelectedSlot) return true;
            SetStatus("저장 슬롯을 먼저 고른 뒤(메인 메뉴 → 게임 시작) 이용해 주세요.", WarnColor);
            return false;
        }

        private void Click() => AudioManager.Instance?.PlaySfx(_clickSound);

        private void SetStatus(string message, Color color)
        {
            if (_statusText == null) return;
            _statusText.text = message;
            _statusText.color = color;
        }

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
