using UnityEngine;

namespace SurvivalDrone.Player
{
    // 플레이어 모델의 애니메이션(대기 / 달리기)을 이동 상태에 맞춰 바꿔주는 스크립트.
    //
    // 어떻게 동작하는가:
    //  이동 자체는 PlayerController(CharacterController)가 하고, 이 스크립트는 "지금 얼마나 빨리 움직이는지"만
    //  Animator에게 알려준다. Animator 안의 상태 그림(PlayerAnimator.controller)이 그 값을 보고
    //  멈춰 있으면 "대기", 움직이면 "달리기" 동작으로 바꾼다.
    //  (애니메이션이 캐릭터를 직접 움직이지 않는다. 동작은 제자리에서 달리는 모양이고 실제 이동은 코드가 맡는다)
    //
    // 시간이 멈추면(일시정지, 레벨업 선택 화면) Animator도 같이 멈추므로 따로 처리할 것이 없다.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerAnimator : MonoBehaviour
    {
        // 동작을 재생할 Animator. 비워두면 자식 오브젝트(모델)에서 자동으로 찾는다.
        [SerializeField] private Animator animator;

        // Animator 안에서 "수평 이동 속도"를 받는 값(파라미터)의 이름. 컨트롤러 파일의 이름과 같아야 한다.
        private const string SpeedParameter = "Speed";

        private static readonly int SpeedHash = Animator.StringToHash(SpeedParameter);

        private CharacterController controller;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            // 인스펙터에서 직접 연결하지 않았다면 자식 모델에서 Animator를 찾는다.
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("[PlayerAnimator] Animator를 찾지 못했습니다. 플레이어 모델에 Animator가 있는지 확인해주세요.");
                enabled = false;
            }
        }

        private void Update()
        {
            // 위아래(중력) 속도는 빼고 땅 위에서 움직이는 속도만 알려준다.
            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            animator.SetFloat(SpeedHash, velocity.magnitude);
        }
    }
}
