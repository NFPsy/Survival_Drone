using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurvivalDrone.LevelUp
{
    // 레벨업 선택 카드에 마우스를 올리면 카드가 살짝 커지고 테두리가 시안색으로 밝아지게 하는 스크립트.
    // 카드(선택지 버튼) 오브젝트에 붙인다. 카드에 Outline(테두리) 컴포넌트가 없으면 크기만 변한다.
    //
    // 주의: 선택 화면이 뜰 때는 Time.timeScale이 0(게임 일시정지)이라서,
    // 시간이 멈춰도 흐르는 Time.unscaledDeltaTime으로 움직여야 애니메이션이 재생된다.
    public class LevelUpCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // 마우스를 올렸을 때의 크기 배율.
        [SerializeField] private float hoverScale = 1.05f;

        // 크기가 바뀌는 속도(클수록 빠르게 따라간다).
        [SerializeField] private float speed = 14f;

        // 마우스를 올렸을 때의 테두리 색.
        [SerializeField] private Color hoverOutlineColor = new Color(0.31f, 0.847f, 0.91f, 1f);

        private Outline _outline;
        private Color _baseOutlineColor;
        private Vector2 _baseOutlineDistance;
        private bool _hover;

        private void Awake()
        {
            _outline = GetComponent<Outline>();
            if (_outline != null)
            {
                _baseOutlineColor = _outline.effectColor;
                _baseOutlineDistance = _outline.effectDistance;
            }
        }

        // 카드가 다시 켜질 때(새 선택 화면)는 항상 평소 모습으로 시작한다.
        private void OnEnable()
        {
            _hover = false;
            transform.localScale = Vector3.one;
            ApplyOutline(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hover = true;
            ApplyOutline(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            ApplyOutline(false);
        }

        private void Update()
        {
            float target = _hover ? hoverScale : 1f;
            float current = transform.localScale.x;
            float next = Mathf.MoveTowards(current, target, speed * (hoverScale - 1f) * Time.unscaledDeltaTime);
            transform.localScale = new Vector3(next, next, 1f);
        }

        private void ApplyOutline(bool hover)
        {
            if (_outline == null) return;
            _outline.effectColor = hover ? hoverOutlineColor : _baseOutlineColor;
            _outline.effectDistance = hover ? _baseOutlineDistance * 2f : _baseOutlineDistance;
        }
    }
}
