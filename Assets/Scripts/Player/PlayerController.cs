using UnityEngine;
using UnityEngine.InputSystem;
using SurvivalDrone.Core;

namespace SurvivalDrone.Player
{
    // 플레이어를 이동시키는 스크립트. 기획서에 따라 "공격은 드론이 자동으로 하고,
    // 플레이어는 이동만 담당"하기 때문에 여기에는 이동 관련 코드만 있다.
    //
    // [RequireComponent]는 이 스크립트가 붙은 오브젝트에 CharacterController가
    // 반드시 있어야 한다는 뜻이다. 없으면 Unity가 자동으로 추가해준다.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        // 이동속도 등의 능력치를 참조하기 위한 PlayerStats 연결.
        [SerializeField] private PlayerStats stats;

        // 캐릭터가 이동 방향으로 회전하는 속도(초당 각도).
        [SerializeField] private float rotationSpeed = 720f;

        // 중력 가속도. 캐릭터가 바닥에 붙어있도록 아래로 당기는 힘.
        [SerializeField] private float gravity = -9.81f;

        // 실제 이동/충돌 처리를 담당하는 유니티 기본 컴포넌트.
        private CharacterController controller;

        // 새 Input System에서 "이동" 입력을 읽어오는 액션(WASD, 게임패드 스틱 등에 연결됨).
        private InputAction moveAction;

        // 중력에 의한 수직(위아래) 속도를 따로 저장해두는 변수.
        private Vector3 verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            // 인스펙터에서 stats를 직접 연결하지 않았다면 같은 오브젝트에서 자동으로 찾는다.
            if (stats == null) stats = GetComponent<PlayerStats>();

            // PlayerInput 컴포넌트에서 "Move"라는 이름의 액션을 찾아 저장.
            // InputSystem_Actions.inputactions 파일에 미리 정의되어 있는 액션이다.
            var playerInput = GetComponent<PlayerInput>();
            moveAction = playerInput != null ? playerInput.actions["Move"] : null;

            // 플레이어 체력이 0이 되어 죽으면(OnDeath) 게임 전체에 "플레이어 사망" 신호를 보낸다.
            var health = GetComponent<Health>();
            if (health != null) health.OnDeath += () => GameEvents.RaisePlayerDied();
        }

        private void Update()
        {
            // 입력값(x=좌우, y=앞뒤)을 -1~1 범위의 Vector2로 읽어온다. 입력이 없으면 (0,0).
            Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

            // 2D 입력(x, y)을 3D 이동 방향(x, 0, z)으로 변환. 위아래(y)는 이동에 사용하지 않음.
            Vector3 move = new Vector3(input.x, 0f, input.y);

            // 대각선으로 이동할 때 속도가 더 빨라지지 않도록 길이를 1로 제한(정규화).
            if (move.sqrMagnitude > 1f) move.Normalize();

            // PlayerStats에서 최종 이동속도를 가져온다. 없으면 기본값 5 사용.
            float speed = stats != null ? stats.MoveSpeed : 5f;
            Vector3 motion = move * speed;

            // 땅에 닿아있으면 살짝 아래로 눌러주는 값(-0.5)을 줘서 계단 등에서 떨어지지 않게 함.
            // 공중에 있으면 중력을 계속 누적시켜 점점 빠르게 떨어지도록 함.
            if (controller.isGrounded)
            {
                verticalVelocity.y = -0.5f;
            }
            else
            {
                verticalVelocity.y += gravity * Time.deltaTime;
            }

            // 실제로 캐릭터를 이동시키는 부분. 프레임 시간(Time.deltaTime)을 곱해
            // 프레임 속도(FPS)와 상관없이 항상 같은 속도로 움직이게 한다.
            controller.Move((motion + verticalVelocity) * Time.deltaTime);

            // 입력이 있을 때만 이동 방향을 바라보도록 부드럽게 회전시킨다.
            if (move.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }
}
