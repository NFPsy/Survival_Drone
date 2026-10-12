using UnityEngine;

namespace SurvivalDrone.Enemies
{
    // 적 로봇의 "걷는 모습"을 코드로 흉내 내는 스크립트. (뼈대·애니메이션 없이 모델 전체를 살짝 움직인다)
    //
    // 왜 필요한가:
    //  적 모델은 팔다리가 움직이지 않는 정지 자세 모델이라, 플레이어는 걷는데 적은 미끄러져 오는 것처럼 보인다.
    //  그래서 모델이 걸음에 맞춰 위아래로 통통 튀고, 좌우로 기우뚱하고, 달릴 때는 앞으로 기울게 만든다.
    //
    // 어떻게 동작하는가:
    //  - 이 스크립트는 적 프리팹의 "Model" 자식 오브젝트에 붙인다. 이동·충돌·피해는 루트(EnemyAI)가 맡고,
    //    여기서는 눈에 보이는 모델의 위치와 기울기만 바꾼다. 그래서 게임 수치와 판정은 전혀 달라지지 않는다.
    //  - 얼마나 빨리 걷는지는 루트가 프레임마다 실제로 움직인 거리로 구한다. 건물에 막히거나 플레이어에 붙어
    //    멈추면 걸음도 멈추고 숨 쉬듯 가만히 있게 된다.
    //  - 적마다 걸음 박자의 시작 위치를 무작위로 줘서, 떼로 몰려와도 모두가 같은 박자로 움직이지 않는다.
    //  - 시간이 멈추면(일시정지) deltaTime이 0이라 움직임도 같이 멈춘다.
    public class EnemyWalkMotion : MonoBehaviour
    {
        [Header("걸음 박자")]
        // 최고 속도로 걸을 때 1초에 몇 걸음을 딛는지. 클수록 총총 걷고, 작을수록 느리고 무겁게 걷는다.
        [SerializeField] private float stepsPerSecond = 2.5f;

        // "최고 속도"로 볼 이동 속도(미터/초). 보통 그 적의 moveSpeed와 같게 둔다.
        // 이 값의 비율만큼 걸음이 빨라지고 움직임의 크기가 커진다.
        [SerializeField] private float referenceSpeed = 4.6f;

        [Header("움직임 크기")]
        // 걸음마다 위로 튀어 오르는 높이(루트 기준 단위, 적 크기에 비례해 보인다).
        [SerializeField] private float bobHeight = 0.08f;

        // 걸을 때 좌우로 기우뚱하는 각도(도).
        [SerializeField] private float rollDegrees = 6f;

        // 최고 속도일 때 앞으로 기울어지는 각도(도). 빠른 적일수록 크게 준다.
        [SerializeField] private float leanDegrees = 6f;

        // 멈춰 있을 때 숨 쉬듯 위아래로 흔들리는 크기. 0이면 멈춘 적은 완전히 정지한다.
        [SerializeField] private float idleBreathHeight = 0.015f;

        // 이동 속도 변화를 부드럽게 따라가는 정도. 클수록 빨리 따라간다. (속도가 순간적으로 튀어도 움직임이 덜컥대지 않게)
        [SerializeField] private float speedSmoothing = 10f;

        // 넉백처럼 순간적으로 밀릴 때 달리는 것처럼 보이지 않도록, 움직임에 쓰는 속도 비율의 상한.
        private const float MaxSpeedRatio = 1.2f;

        // 프리팹에 설정된 원래 위치·회전(모델 방향을 맞춰 둔 값). 여기에 흔들림을 더하고 뺀다.
        private Vector3 basePosition;
        private Quaternion baseRotation;

        // 이 모델의 부모(= 이동하는 적 루트). 걸은 거리를 여기서 잰다.
        private Transform root;
        private Vector3 lastRootPosition;

        // 걸음 박자(라디안). 2π가 두 걸음(한 쌍)이다.
        private float stridePhase;
        private float breathPhase;

        // 부드럽게 다듬은 현재 이동 속도 비율(0 = 정지, 1 = 최고 속도).
        private float smoothedRatio;

        private void Awake()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            root = transform.parent;

            // 모든 적이 같은 박자로 움직이지 않도록 시작 위치를 무작위로 준다.
            stridePhase = Random.value * Mathf.PI * 2f;
            breathPhase = Random.value * Mathf.PI * 2f;
        }

        private void OnEnable()
        {
            // 활성화되자마자 첫 프레임에 "스폰 위치까지 순간이동한 것"을 이동으로 착각하지 않도록 현재 위치를 기억해 둔다.
            if (root != null) lastRootPosition = root.position;
            smoothedRatio = 0f;
        }

        // 적 루트의 이동(EnemyAI.Update)이 끝난 뒤에 모델을 움직이려고 LateUpdate에서 처리한다.
        private void LateUpdate()
        {
            if (root == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return; // 일시정지 중에는 그대로 둔다.

            // 1) 이번 프레임에 수평으로 얼마나 움직였는지로 속도를 구한다.
            Vector3 delta = root.position - lastRootPosition;
            lastRootPosition = root.position;
            delta.y = 0f;
            float speed = delta.magnitude / dt;

            float targetRatio = referenceSpeed > 0.01f ? Mathf.Min(speed / referenceSpeed, MaxSpeedRatio) : 0f;
            smoothedRatio = Mathf.Lerp(smoothedRatio, targetRatio, 1f - Mathf.Exp(-speedSmoothing * dt));

            // 2) 걸음 박자를 속도에 비례해 앞으로 돌린다. (느리게 걸으면 박자도 느려진다)
            stridePhase += dt * stepsPerSecond * Mathf.PI * smoothedRatio;
            breathPhase += dt * 1.6f;

            // 3) 박자에서 위아래·좌우 흔들림을 계산한다.
            //    |sin|은 한 쌍의 걸음 동안 두 번 튀어 오르고(발을 딛는 순간 가장 낮음),
            //    cos은 발을 딛는 순간 가장 크게 그쪽으로 기울어진다.
            float bounce = Mathf.Abs(Mathf.Sin(stridePhase)) * bobHeight * smoothedRatio;
            float breath = Mathf.Sin(breathPhase) * idleBreathHeight * (1f - Mathf.Clamp01(smoothedRatio * 2f));
            float roll = Mathf.Cos(stridePhase) * rollDegrees * smoothedRatio;
            float pitch = leanDegrees * smoothedRatio;

            // 4) 원래 자세에 흔들림을 얹는다. 기울기는 부모(루트) 기준 축으로 돌리므로
            //    모델 파일의 방향(270도 눕힘)과 상관없이 "앞으로 숙이기 / 옆으로 기울기"가 된다.
            transform.localPosition = basePosition + Vector3.up * (bounce + breath);
            transform.localRotation = Quaternion.Euler(pitch, 0f, roll) * baseRotation;
        }
    }
}
