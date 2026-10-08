using System;
using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 테스트(CBT) 로그 창. 테스터가 자신의 플레이 기록을 개발자에게 보낼 수 있게 해준다.
    //   - 미리보기: 요약(판 수, 클리어율, 뽑기 횟수 등)과 최근 기록 몇 줄
    //   - 복사: 전체 기록을 클립보드에 복사 → 디스코드·메일 등에 붙여넣어 보낸다
    //   - 파일로 저장: 전체 기록을 텍스트 파일로 내려받는다 → 파일을 보낸다
    //
    // 구조: 이 스크립트가 붙은 팝업 뿌리 아래에 Box/PreviewText, Box/StatusText, Box/BtnCopy, Box/BtnSave, Box/BtnClose 가 있어야 한다.
    // (LobbySceneBuilder가 만든다). 못 찾아도 경고만 남기고 나머지는 계속 동작한다.
    public class TestLogPopup : MonoBehaviour
    {
        [SerializeField] private AudioClip clickSound;

        private static readonly Color CyanColor = new Color(0.310f, 0.847f, 0.910f);
        private static readonly Color ShortColor = new Color(1f, 0.35f, 0.35f);

        private Text _previewText;
        private Text _statusText;

        private void Awake()
        {
            _previewText = FindText("Box/PreviewText");
            _statusText = FindText("Box/StatusText");
            WireButton("Box/BtnCopy", Copy);
            WireButton("Box/BtnSave", Save);
            WireButton("Box/BtnClose", Close);
        }

        // 창이 열릴 때마다 최신 기록으로 미리보기를 다시 채운다.
        private void OnEnable()
        {
            SetStatus("", CyanColor);
            if (_previewText != null)
            {
                // 저장 슬롯을 쓰는 중이면 지금 슬롯 번호를 알려 주고, 내보내기에는 모든 슬롯의 기록이 합쳐져 들어간다.
                string slotNote = SaveManager.SlotsEnabled && SaveManager.CurrentSlot > 0
                    ? $"[지금 슬롯 {SaveManager.CurrentSlot}] 복사·파일 저장에는 모든 슬롯의 기록이 함께 들어갑니다\n\n"
                    : "";
                _previewText.text = slotNote + PlayLog.BuildPreviewText(SaveManager.Data);
            }
        }

        public void Open() => gameObject.SetActive(true);

        private void Close()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            gameObject.SetActive(false);
        }

        private void Copy()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            BrowserBridge.CopyToClipboard(BuildExport());
            // 웹에서는 브라우저가 복사를 막았는지 게임이 알 수 없으므로, 안 되면 파일 저장을 쓰도록 함께 안내한다.
            SetStatus("복사했습니다. 붙여넣기가 안 되면 '파일로 저장'을 눌러주세요.", CyanColor);
        }

        private void Save()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            string fileName = $"drone-commander-log-{SaveManager.Data.testerId}-{DateTime.Now:MMdd-HHmm}.txt";
            string result = BrowserBridge.DownloadTextFile(fileName, BuildExport());
            if (result == null) SetStatus("파일 저장에 실패했습니다. '복사'를 눌러주세요.", ShortColor);
            else SetStatus($"파일로 저장했습니다: {fileName}  ({result})", CyanColor);
        }

        private static string BuildExport()
        {
            // 슬롯을 쓰는 중이면 모든 슬롯의 기록을 한 글로 합쳐서 내보낸다. (슬롯마다 구역이 나뉜다)
            if (SaveManager.SlotsEnabled && SaveManager.CurrentSlot > 0)
                return PlayLog.BuildMultiSlotExportText(SaveManager.PeekAllSlots(), Application.version, Application.platform.ToString());
            return PlayLog.BuildExportText(SaveManager.Data, Application.version, Application.platform.ToString());
        }

        private void SetStatus(string message, Color color)
        {
            if (_statusText == null) return;
            _statusText.text = message;
            _statusText.color = color;
        }

        private Text FindText(string path)
        {
            var child = transform.Find(path);
            var text = child != null ? child.GetComponent<Text>() : null;
            if (text == null) Debug.LogWarning($"[Save] 테스트 로그 창에서 '{path}' 글자 칸을 찾지 못했습니다.");
            return text;
        }

        private void WireButton(string path, UnityEngine.Events.UnityAction action)
        {
            var child = transform.Find(path);
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogWarning($"[Save] 테스트 로그 창에서 '{path}' 버튼을 찾지 못했습니다.");
                return;
            }
            button.onClick.AddListener(action);
        }
    }
}
