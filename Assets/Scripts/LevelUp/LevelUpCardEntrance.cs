using System.Collections;
using UnityEngine;

namespace SurvivalDrone.LevelUp
{
    // 레벨업 선택 화면에서 카드(와 제목 글자)가 "차례로 올라오며" 나타나게 하는 연출 스크립트.
    // 카드마다, 그리고 화면 위쪽 제목 글자에 붙인다. 붙은 오브젝트가 켜질 때마다(= 선택 화면이 뜰 때마다) 처음부터 다시 재생한다.
    //
    // 움직임: 정해 둔 시간(delay)만큼 기다렸다가, 아래에서 위로 slideFrom만큼 올라오며 + 서서히 선명해지고 + 조금 커진다.
    //         끝에서 살짝 튕기듯 멈추고, 카드는 도착하는 순간 종류 색 장식(LevelUpCardAccent)이 한 번 반짝인다.
    //
    // 주의: 선택 화면이 뜰 때는 Time.timeScale이 0(게임 일시정지)이라서, 시간이 멈춰도 흐르는 "실제 시간"으로 움직인다.
    // 카드 크기는 마우스 올림 효과(LevelUpCardHover)도 바꾸므로, 연출 중에는 그쪽을 잠깐 쉬게 한다.
    public class LevelUpCardEntrance : MonoBehaviour
    {
        // 켜진 뒤 움직이기 시작하기까지 기다리는 시간(초).
        [SerializeField] private float delay = 0f;

        // 올라오는 데 걸리는 시간(초).
        [SerializeField] private float duration = 0.22f;

        // 처음 위치: 원래 자리에서 이만큼 떨어진 곳(픽셀)에서 시작한다. 카드는 (0, -40) = 40px 아래.
        [SerializeField] private Vector2 slideFrom = new Vector2(0f, -40f);

        // 처음 크기 배율. 1이 될 때까지 커진다.
        [SerializeField] private float startScale = 0.9f;

        // 끝에서 살짝 튕기는 정도 (0이면 튕김 없음).
        [SerializeField] private float overshoot = 1.2f;

        // 한 프레임에 흐르는 시간의 상한(초). 선택 화면을 처음 켜는 프레임은 화면을 만드느라 오래 걸려서(특히 웹)
        // 그 시간이 그대로 더해지면 연출이 시작하자마자 끝나 버린다. 그래서 한 번에 1/30초까지만 센다.
        private const float MaxStep = 1f / 30f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private LevelUpCardHover _hover;
        private LevelUpCardAccent _accent;
        private Vector2 _basePosition;
        private bool _baseKnown;
        private float _elapsed;
        private bool _finished = true;

        // 이 카드를 지금 골라도 되는지. 거의 다 올라온 뒤(진행 60% 이후)부터 true.
        // (아직 안 올라온 카드를 키보드나 마우스로 고르는 것을 막되, 모든 카드가 다 올라올 때까지 기다리게 하지는 않는다)
        public bool CanChoose => _finished || _elapsed >= delay + duration * 0.6f;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _hover = GetComponent<LevelUpCardHover>();
            _accent = GetComponent<LevelUpCardAccent>();
        }

        // 켜질 때마다 처음부터. 원래 자리는 "처음 한 번"만 기억한다(연출 도중 다시 켜져도 위치가 밀리지 않도록).
        private void OnEnable()
        {
            if (!_baseKnown)
            {
                _basePosition = _rect.anchoredPosition;
                _baseKnown = true;
            }
            _elapsed = 0f;
            _finished = false;
            Apply(0f);
            StartCoroutine(Play());
        }

        // 도중에 꺼지면(선택을 마침) 원래 모습으로 돌려 놓는다. 다음에 켜질 때 OnEnable이 다시 시작한다.
        private void OnDisable()
        {
            if (!_baseKnown) return;
            Restore();
        }

        private IEnumerator Play()
        {
            if (_hover != null) _hover.Suspended = true;

            while (_elapsed < delay + duration)
            {
                _elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
                float t = Mathf.Clamp01((_elapsed - delay) / duration);
                Apply(t);
                yield return null;
            }

            Restore();
            _finished = true;
            if (_accent != null) _accent.Flash(); // 도착하는 순간 한 번 반짝
        }

        // 진행도 t(0~1)에 맞게 위치·투명도·크기를 정한다.
        private void Apply(float t)
        {
            float eased = EaseOutBack(t);
            _rect.anchoredPosition = _basePosition + slideFrom * (1f - eased);
            _group.alpha = Mathf.Clamp01(t * 1.6f); // 위치보다 조금 빨리 선명해진다
            float scale = Mathf.LerpUnclamped(startScale, 1f, eased);
            _rect.localScale = new Vector3(scale, scale, 1f);
        }

        // 원래 자리·투명도·크기로 되돌리고 마우스 올림 효과를 다시 켠다.
        private void Restore()
        {
            _rect.anchoredPosition = _basePosition;
            _rect.localScale = Vector3.one;
            if (_group != null) _group.alpha = 1f;
            if (_hover != null) _hover.Suspended = false;
        }

        // 끝에서 살짝 넘쳤다 돌아오는 완화 곡선(easeOutBack).
        private float EaseOutBack(float t)
        {
            float c1 = overshoot;
            float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
