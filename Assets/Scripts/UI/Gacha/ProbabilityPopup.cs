using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 확률 공개 팝업. 등급별 확률표와 천장·초기화·10연 규칙을 보여준다.
    // 표시하는 숫자와 문장은 전부 GachaTable에서 읽어 만든다 (GachaRateFormatter) — "표기 = 실제" 원칙.
    //
    // 구조: 이 스크립트가 붙은 팝업 뿌리 아래에 Box/RatesText, Box/RulesText, Box/BtnClose 가 있어야 한다.
    public class ProbabilityPopup : MonoBehaviour
    {
        [SerializeField] private AudioClip clickSound;

        private Text _ratesText;
        private Text _rulesText;

        private void Awake()
        {
            _ratesText = FindText("Box/RatesText");
            _rulesText = FindText("Box/RulesText");

            var close = transform.Find("Box/BtnClose");
            var button = close != null ? close.GetComponent<Button>() : null;
            if (button != null) button.onClick.AddListener(Close);
            else Debug.LogWarning("[Gacha] 확률 팝업에서 'Box/BtnClose' 버튼을 찾지 못했습니다.");
        }

        // 팝업이 열릴 때마다 최신 GachaTable 값으로 다시 채운다.
        private void OnEnable()
        {
            var table = GachaController.Instance != null ? GachaController.Instance.Table : null;
            if (table == null)
            {
                Debug.LogWarning("[Gacha] 확률 팝업을 채울 GachaTable을 찾지 못했습니다.");
                return;
            }

            if (_ratesText != null) _ratesText.text = GachaRateFormatter.BuildRatesRichText(table);
            if (_rulesText != null) _rulesText.text = GachaRateFormatter.BuildRulesText(table);
        }

        public void Open() => gameObject.SetActive(true);

        private void Close()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            gameObject.SetActive(false);
        }

        private Text FindText(string path)
        {
            var child = transform.Find(path);
            var text = child != null ? child.GetComponent<Text>() : null;
            if (text == null) Debug.LogWarning($"[Gacha] 확률 팝업에서 '{path}' 글자 칸을 찾지 못했습니다.");
            return text;
        }
    }
}
