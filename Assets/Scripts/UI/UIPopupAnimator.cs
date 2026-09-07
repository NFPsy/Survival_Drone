using System.Collections;
using UnityEngine;

namespace SurvivalDrone.UI
{
    // UI 패널이 SetActive(true)로 켜질 때마다, 뚝 튀어나오는 대신
    // 살짝 작았다가 커지면서 + 서서히 선명해지는 연출을 자동으로 넣어주는 컴포넌트.
    // 레벨업 선택 화면, 결과 화면(승리/패배)처럼 "짠!" 하고 나타나야 하는 패널에 붙인다.
    //
    // 주의: 이 패널들은 뜰 때 Time.timeScale이 0(게임 일시정지)인 경우가 많다.
    // 그래서 일반적인 Time.deltaTime 대신 시간이 멈춰도 흐르는
    // Time.unscaledDeltaTime을 사용해야 애니메이션이 실제로 재생된다.
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPopupAnimator : MonoBehaviour
    {
        // 연출이 걸리는 시간(초). 실시간 기준이라 일시정지 중에도 정상적으로 흐른다.
        [SerializeField] private float duration = 0.18f;

        // 시작할 때의 크기 배율 (1보다 작게 시작해서 원래 크기로 커지는 느낌을 준다).
        [SerializeField] private float startScale = 0.85f;

        private CanvasGroup canvasGroup;
        private Coroutine playingRoutine;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        // 이 오브젝트가 SetActive(true)로 켜질 때마다 자동으로 호출된다.
        private void OnEnable()
        {
            if (playingRoutine != null) StopCoroutine(playingRoutine);
            playingRoutine = StartCoroutine(PlayPopup());
        }

        private IEnumerator PlayPopup()
        {
            float elapsed = 0f;
            transform.localScale = Vector3.one * startScale;
            canvasGroup.alpha = 0f;

            while (elapsed < duration)
            {
                // Time.deltaTime이 아니라 Time.unscaledDeltaTime을 써야
                // Time.timeScale = 0(일시정지) 상태에서도 애니메이션이 진행된다.
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // easeOutBack 느낌으로 살짝 튕기듯 커지게 만드는 간단한 완화 곡선.
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                transform.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, eased);
                canvasGroup.alpha = eased;

                yield return null;
            }

            transform.localScale = Vector3.one;
            canvasGroup.alpha = 1f;
            playingRoutine = null;
        }
    }
}
