using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;

namespace SurvivalDrone.UI
{
    // 체력이 위험할 만큼 낮아지면 화면 가장자리가 붉게 맥동(깜빡)하며 경고해주는 스크립트.
    // 지금까지는 체력바/숫자만 보고 판단해야 했는데, 이 연출이 있으면
    // 화면을 안 보고 있어도 "지금 위험하다"는 걸 즉각적으로 느낄 수 있다.
    public class LowHealthWarning : MonoBehaviour
    {
        // 체력 변화를 감지하기 위한 플레이어의 Health 컴포넌트.
        [SerializeField] private Health playerHealth;

        // 화면 전체를 덮는 빨간 오버레이 이미지.
        [SerializeField] private Image overlay;

        // 체력 비율이 이 값(0~1) 이하로 떨어지면 경고를 시작한다. 0.3 = 30%.
        [SerializeField] private float warningThreshold = 0.3f;

        // 맥동(깜빡임) 속도. 값이 클수록 더 빨리 깜빡인다.
        [SerializeField] private float pulseSpeed = 4f;

        // 가장 진할 때의 최대 투명도(알파). 너무 진하면 화면이 안 보이니 적당히 낮게.
        [SerializeField] private float maxAlpha = 0.35f;

        // 지금 경고 상태인지 여부 (체력 비율이 threshold 이하인 동안 true).
        private bool isWarning;

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.OnHealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnHealthChanged -= HandleHealthChanged;
        }

        private void Start()
        {
            SetAlpha(0f);
        }

        // 체력이 바뀔 때마다 호출되어, 지금 경고 상태로 들어가야 하는지 판단한다.
        private void HandleHealthChanged(float current, float max)
        {
            float ratio = max > 0f ? current / max : 0f;
            isWarning = ratio > 0f && ratio <= warningThreshold;

            // 경고 상태가 아니게 되면(체력을 회복했거나 죽었으면) 오버레이를 즉시 끈다.
            if (!isWarning) SetAlpha(0f);
        }

        private void Update()
        {
            if (!isWarning || overlay == null) return;

            // 사인 함수를 이용해 0~1 사이를 부드럽게 오가는 값을 만들어 "맥동"하는 느낌을 준다.
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            SetAlpha(pulse * maxAlpha);
        }

        private void SetAlpha(float alpha)
        {
            if (overlay == null) return;
            Color c = overlay.color;
            c.a = alpha;
            overlay.color = c;
        }
    }
}
