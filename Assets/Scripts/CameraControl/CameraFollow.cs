using UnityEngine;

namespace SurvivalDrone.CameraControl
{
    // 카메라가 플레이어를 계속 따라다니게 만드는 스크립트.
    // 기획서의 "쿼터뷰(비스듬히 위에서 내려다보는 시점)"를 구현하기 위해
    // 플레이어 위치에서 일정한 거리(offset)만큼 떨어진 곳을 계속 유지한다.
    public class CameraFollow : MonoBehaviour
    {
        // 따라다닐 대상(플레이어). 인스펙터에서 연결하거나 SetTarget으로 지정.
        [SerializeField] private Transform target;

        // 대상으로부터 얼마나 떨어져서(위쪽, 뒤쪽) 카메라를 위치시킬지.
        // (0, 10, -7) = 위로 10, 뒤로 7만큼 떨어진 위치 -> 비스듬히 내려다보는 구도가 됨.
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -7f);

        // 카메라가 얼마나 부드럽게(느리게) 따라올지. 값이 작을수록 빠르게 반응함.
        [SerializeField] private float smoothTime = 0.15f;

        // Vector3.SmoothDamp 함수가 내부적으로 사용하는 현재 속도 값(직접 사용하지 않아도 됨).
        private Vector3 velocity;

        // 외부(예: 게임 시작 시 초기화 코드)에서 따라다닐 대상을 바꾸고 싶을 때 사용.
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        // LateUpdate는 모든 오브젝트의 Update()가 끝난 뒤 마지막에 호출된다.
        // 플레이어가 먼저 움직인 다음에 카메라가 따라가야 자연스럽기 때문에 여기서 처리한다.
        private void LateUpdate()
        {
            if (target == null) return;

            // 목표 위치 = 플레이어 위치 + 오프셋(떨어진 거리).
            Vector3 desired = target.position + offset;

            // 목표 위치로 부드럽게 이동(순간이동이 아니라 서서히 따라가도록).
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

            // 카메라가 항상 플레이어(살짝 위쪽)를 바라보도록 회전시킨다.
            transform.LookAt(target.position + Vector3.up * 1.2f);
        }
    }
}
