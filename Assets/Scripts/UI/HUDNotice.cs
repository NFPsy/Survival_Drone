using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalDrone.UI
{
    // 화면 위쪽에 잠깐 떴다 사라지는 알림 문구. "미니 보스 출현!", "마일스톤 달성! 코어 +100" 같은 안내에 쓴다.
    // 어디서든 HUDNotice.Instance?.Show("문구")로 부를 수 있다. (씬에 하나만 둔다)
    public class HUDNotice : MonoBehaviour
    {
        public static HUDNotice Instance { get; private set; }

        // 문구를 보여줄 텍스트. 평소엔 꺼져 있다가 Show()가 켜 준다.
        [SerializeField] private Text label;

        // 문구가 화면에 머무는 시간(초)의 기본값.
        [SerializeField] private float defaultSeconds = 2.5f;

        private Coroutine _hideRoutine;

        private void Awake()
        {
            Instance = this;
            if (label != null) label.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 문구를 보여주고 잠시 뒤 자동으로 숨긴다. 이미 다른 문구가 떠 있으면 새 문구로 바꾼다.
        public void Show(string message, float seconds = 0f)
        {
            if (label == null) return;

            label.text = message;
            label.gameObject.SetActive(true);

            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(HideAfter(seconds > 0f ? seconds : defaultSeconds));
        }

        // 레벨업 선택 화면처럼 시간이 멈춰(timeScale 0) 있어도 사라지도록 "실제 시간"으로 센다.
        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            label.gameObject.SetActive(false);
            _hideRoutine = null;
        }
    }
}
