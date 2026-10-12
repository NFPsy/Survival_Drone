using UnityEngine;

namespace SurvivalDrone.Enemies
{
    // 뼈대(리깅)가 있는 적 모델의 동작(대기 / 걷기)을 이동 상태에 맞춰 바꿔 주는 스크립트.
    // 지금은 보스(와 보스 모델을 쓰는 미니 보스)에 쓴다. 주인공의 PlayerAnimator와 같은 방식이다.
    //
    // 어떻게 동작하는가:
    //  - 적의 이동은 EnemyAI(루트 오브젝트)가 좌표를 직접 옮겨서 한다. 이 스크립트는 움직이지 않고,
    //    "지금 얼마나 빨리 움직이는지"만 Animator(동작을 고르는 장치)에게 알려준다.
    //  - 속도는 루트가 프레임마다 실제로 움직인 거리로 잰다. 그래서 건물에 막히거나 플레이어에 붙어 멈추면
    //    자동으로 대기 동작으로 바뀐다.
    //  - 걷기 동작의 재생 속도(WalkRate)도 같이 맞춘다. 동작 안에서 땅에 닿은 발이 뒤로 움직이는 속도와
    //    실제 이동 속도가 어긋나면 발이 땅 위에서 미끄러져 보이기 때문이다.
    //      재생 속도 = 실제 이동 속도 ÷ (동작 속 발 속도 × 모델의 현재 월드 크기)
    //    미니 보스처럼 모델이 작아지면 발 속도도 작아지므로 크기를 곱해서 계산한다.
    //  - 시간이 멈추면(일시정지, 레벨업 선택 화면) Animator도 같이 멈추므로 따로 처리할 것이 없다.
    //
    // 이 스크립트는 적 프리팹의 "Model"(Animator가 있는 오브젝트)에 붙인다.
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimator : MonoBehaviour
    {
        // Animator 안에서 값을 받는 파라미터 이름. 컨트롤러 파일(BossAnimator.controller)의 이름과 같아야 한다.
        private const string SpeedParameter = "Speed";
        private const string WalkRateParameter = "WalkRate";

        // 땅에 닿은 발이 동작 안에서 1초에 뒤로 움직이는 거리(모델 크기를 1로 봤을 때, 미터/초).
        // 보스 걷기 동작의 발 위치를 재서 구한 값(약 0.44)이다. 걷는 모습이 땅에서 미끄러지면 이 값을 조절한다.
        [SerializeField] private float footSpeedAtUnitScale = 0.44f;

        // 걷기 동작 재생 속도의 허용 범위. (너무 느리거나 빨라서 어색해지는 것을 막는다)
        [SerializeField] private float minWalkRate = 0.3f;
        [SerializeField] private float maxWalkRate = 1.6f;

        // 이동 속도 변화를 부드럽게 따라가는 정도. 클수록 빨리 따라간다.
        [SerializeField] private float speedSmoothing = 10f;

        // 넉백처럼 순간적으로 밀릴 때 걷는 속도가 비정상적으로 커 보이지 않도록 속도의 상한(미터/초).
        [SerializeField] private float maxSpeed = 6f;

        private static readonly int SpeedHash = Animator.StringToHash(SpeedParameter);
        private static readonly int WalkRateHash = Animator.StringToHash(WalkRateParameter);

        private Animator animator;

        // 이 모델의 부모(= 이동하는 적 루트). 걸은 거리를 여기서 잰다.
        private Transform root;
        private Vector3 lastRootPosition;
        private float smoothedSpeed;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            root = transform.parent;
        }

        private void OnEnable()
        {
            // 활성화되자마자 "스폰 위치까지 순간이동한 것"을 이동으로 착각하지 않도록 현재 위치를 기억해 둔다.
            if (root != null) lastRootPosition = root.position;
            smoothedSpeed = 0f;
        }

        // 적 루트의 이동(EnemyAI.Update)이 끝난 뒤에 속도를 재려고 LateUpdate에서 처리한다.
        private void LateUpdate()
        {
            if (root == null || animator == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return; // 일시정지 중에는 그대로 둔다.

            // 이번 프레임에 수평으로 얼마나 움직였는지로 속도를 구한다.
            Vector3 delta = root.position - lastRootPosition;
            lastRootPosition = root.position;
            delta.y = 0f;
            float speed = Mathf.Min(delta.magnitude / dt, maxSpeed);
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-speedSmoothing * dt));

            // 걷기 재생 속도: 실제 이동 속도 ÷ (동작 속 발 속도 × 현재 월드 크기)
            float footSpeed = footSpeedAtUnitScale * transform.lossyScale.y;
            float walkRate = footSpeed > 0.0001f ? Mathf.Clamp(smoothedSpeed / footSpeed, minWalkRate, maxWalkRate) : 1f;

            animator.SetFloat(SpeedHash, smoothedSpeed);
            animator.SetFloat(WalkRateHash, walkRate);
        }
    }
}
