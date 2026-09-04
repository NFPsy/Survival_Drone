using System.Collections;
using UnityEngine;

namespace SurvivalDrone.Core
{
    // 피해를 입었을 때 잠깐 색이 번쩍이는 연출을 담당하는 컴포넌트.
    // 지금까지는 적이나 플레이어가 맞아도 눈에 보이는 반응이 전혀 없어서
    // "지금 공격이 맞고 있는지" 알기 어려웠는데, 이 컴포넌트를 붙이면
    // Health의 OnDamaged 이벤트가 울릴 때마다 잠깐 색이 바뀌었다가 원래대로 돌아온다.
    //
    // [RequireComponent(typeof(Health))]는 이 스크립트가 붙은 오브젝트에
    // Health(체력) 컴포넌트가 반드시 있어야 한다는 뜻.
    [RequireComponent(typeof(Health))]
    public class DamageFlash : MonoBehaviour
    {
        // 색을 바꿀 대상 렌더러. 비워두면 자기 자신이나 자식에서 자동으로 찾는다.
        [SerializeField] private Renderer targetRenderer;

        // 맞았을 때 잠깐 바뀔 색. 기본값은 흰색(가장 눈에 잘 띄는 "맞았다" 느낌).
        [SerializeField] private Color flashColor = Color.white;

        // 색이 번쩍이는 시간(초). 너무 길면 눈에 거슬리고, 너무 짧으면 안 보이므로 적당히 짧게.
        [SerializeField] private float flashDuration = 0.12f;

        private Health health;

        // 원래 머티리얼 색상 (번쩍인 뒤 이 색으로 되돌아간다).
        private Color originalColor;

        // 지금 진행 중인 번쩍임 코루틴 (중복 실행 방지를 위해 저장해둔다).
        private Coroutine flashRoutine;

        // MaterialPropertyBlock을 쓰면 머티리얼 에셋 자체를 복제하지 않고도
        // 오브젝트별로 다른 색을 잠깐 보여줄 수 있어서, 적이 아무리 많아도 메모리 낭비가 없다.
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            health = GetComponent<Health>();
            propertyBlock = new MaterialPropertyBlock();

            // 인스펙터에서 렌더러를 지정하지 않았다면 자기 자신이나 자식에서 자동으로 찾는다.
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();

            // 나중에 되돌아갈 수 있도록 원래 색을 기억해둔다.
            if (targetRenderer != null) originalColor = targetRenderer.sharedMaterial.color;
        }

        private void OnEnable()
        {
            // 피해를 입을 때마다 HandleDamaged가 자동으로 호출되도록 연결.
            health.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            health.OnDamaged -= HandleDamaged;
        }

        private void HandleDamaged(float amount)
        {
            if (targetRenderer == null) return;

            // 연속으로 맞아서 번쩍임이 겹치면 이전 것을 취소하고 새로 시작한다.
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        // 색을 flashColor로 바꿨다가, 잠깐 기다린 뒤 원래 색으로 되돌리는 코루틴.
        private IEnumerator FlashRoutine()
        {
            SetColor(flashColor);
            yield return new WaitForSeconds(flashDuration);
            SetColor(originalColor);
            flashRoutine = null;
        }

        // MaterialPropertyBlock을 이용해 렌더러의 색상만 바꾸는 함수.
        private void SetColor(Color color)
        {
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
