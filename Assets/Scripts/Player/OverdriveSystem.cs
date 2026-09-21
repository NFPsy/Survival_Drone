using System;
using UnityEngine;
using UnityEngine.InputSystem;
using SurvivalDrone.Core;

namespace SurvivalDrone.Player
{
    // "오버드라이브" — 이 게임의 유일한 액티브 스킬.
    //
    // 왜 만들었나:
    // 원래 이 게임은 플레이어가 "이동"만 하고 공격은 드론이 전부 자동으로 했다.
    // 그래서 한 판 10분 동안 플레이어가 내리는 판단이 "어디로 움직일까"밖에 없었고,
    // 순간순간의 선택이 없어서 밋밋했다. 이 시스템은 거기에 "지금 지를까, 참을까"라는
    // 판단을 하나 더해주기 위한 것이다.
    //
    // 어떻게 동작하나:
    //  1) 적을 처치할 때마다 게이지가 찬다 (엘리트 로봇은 훨씬 많이 채워준다).
    //  2) 게이지가 100%가 되면 스페이스바로 발동할 수 있다.
    //  3) 발동하면 일정 시간 동안 모든 공격 드론의 공격 속도가 크게 올라가고, 이동도 빨라진다.
    //  4) 대신 그 시간 동안에는 플레이어가 "받는 피해도 2배"가 된다.  ← 이게 핵심
    //
    // 4번 때문에 "적이 많이 몰려 있을 때"(=게이지가 빨리 차고 이득이 큰 순간)가
    // 동시에 "가장 위험한 순간"이 되어서, 발동 타이밍 자체가 하나의 선택이 된다.
    public class OverdriveSystem : MonoBehaviour
    {
        // ── 인스펙터에서 조절 가능한 수치들 ──

        // 게이지를 가득 채우는 데 필요한 양. (게이지는 0부터 이 값까지 찬다)
        [SerializeField] private float maxGauge = 100f;

        // 일반 적을 한 마리 잡을 때마다 차는 게이지 양.
        // 4로 두면 일반 적 25마리를 잡아야 한 번 쓸 수 있다.
        [SerializeField] private float gaugePerKill = 4f;

        // 엘리트 로봇을 잡았을 때 차는 게이지 양 (일반 적의 약 6배).
        // "엘리트를 먼저 노리면 오버드라이브를 더 자주 쓸 수 있다"는 유인을 만들기 위함.
        [SerializeField] private float gaugePerEliteKill = 25f;

        // 발동했을 때 효과가 지속되는 시간(초).
        [SerializeField] private float duration = 5f;

        // 발동 중 드론들의 공격 속도가 몇 배가 되는지.
        [SerializeField] private float attackSpeedMultiplier = 2.5f;

        // 발동 중 플레이어가 "받는" 피해가 몇 배가 되는지. 이게 위험 부담(리스크)이다.
        [SerializeField] private float damageTakenMultiplier = 2f;

        // 발동 중 이동 속도가 몇 배가 되는지 (1.25 = 25% 빨라짐).
        // 피해를 2배로 받는 동안 피할 수단도 조금은 줘야 공정하기 때문에 같이 올려준다.
        [SerializeField] private float moveSpeedMultiplier = 1.25f;

        // 게이지가 가득 찼을 때 한 번 재생할 효과음("이제 쓸 수 있다"는 신호).
        [SerializeField] private AudioClip readySound;

        // 발동하는 순간 재생할 효과음.
        [SerializeField] private AudioClip activateSound;

        // 효과가 끝날 때 재생할 효과음.
        [SerializeField] private AudioClip endSound;

        // ── 다른 스크립트들이 읽어가는 값들 ──

        // 드론 스크립트들이 "지금 공격 속도를 몇 배로 해야 하나"를 물어보는 값.
        // static(정적)으로 만든 이유: 드론이 5종류나 되는데 각자 이 매니저를 인스펙터로
        // 연결해주는 건 번거롭고 실수하기 쉬워서, 어디서든 바로 읽을 수 있게 했다.
        public static float AttackSpeedMultiplier { get; private set; } = 1f;

        // PlayerController가 읽어가는 이동 속도 배율.
        public static float MoveSpeedMultiplier { get; private set; } = 1f;

        // 현재 게이지 양 (0 ~ maxGauge).
        public float Gauge { get; private set; }

        // 게이지를 0~1 비율로 변환한 값 (HUD 게이지 바가 이 값을 쓴다).
        public float GaugeRatio => maxGauge > 0f ? Gauge / maxGauge : 0f;

        // 지금 발동할 수 있는 상태인지 (게이지가 가득 찼고, 아직 발동 중이 아님).
        public bool IsReady => Gauge >= maxGauge && !IsActive;

        // 지금 오버드라이브 효과가 진행 중인지.
        public bool IsActive { get; private set; }

        // 게이지가 바뀔 때마다 알려주는 이벤트. (0~1 비율, 지금 발동 중인지)
        public event Action<float, bool> OnGaugeChanged;

        // 발동한 순간 알려주는 이벤트 (화면 연출 등이 구독).
        public event Action OnOverdriveStarted;

        // 효과가 끝난 순간 알려주는 이벤트.
        public event Action OnOverdriveEnded;

        // ── 내부에서만 쓰는 값들 ──

        // 남은 지속 시간(초). 발동 중에만 0보다 크다.
        private float remainingTime;

        // 같은 오브젝트에 붙어 있는 체력 컴포넌트 (받는 피해 배율을 바꾸기 위해 필요).
        private Health health;

        // 게이지가 가득 찼다는 소리를 이미 냈는지 (매 프레임 반복해서 울리지 않도록).
        private bool readySoundPlayed;

        private void Awake()
        {
            health = GetComponent<Health>();

            // static 값은 씬을 다시 불러와도 이전 판의 값이 그대로 남아 있을 수 있다.
            // (예: 오버드라이브 발동 중에 재시작하면 공격 속도 2.5배가 계속 유지되는 버그)
            // 그래서 시작할 때 항상 원래 값(1배)으로 되돌려준다.
            AttackSpeedMultiplier = 1f;
            MoveSpeedMultiplier = 1f;
        }

        private void OnEnable()
        {
            // 적이 죽을 때마다 HandleEnemyKilled가 호출되도록 연결.
            GameEvents.OnEnemyKilled += HandleEnemyKilled;
        }

        private void OnDisable()
        {
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;

            // 이 오브젝트가 사라질 때도(씬 전환/재시작 등) 배율을 원래대로 되돌려준다.
            AttackSpeedMultiplier = 1f;
            MoveSpeedMultiplier = 1f;
        }

        // 적이 죽었을 때 호출되어 게이지를 채우는 함수.
        // wasElite가 true면 엘리트 로봇이었다는 뜻이라 훨씬 많이 채워준다.
        private void HandleEnemyKilled(Vector3 position, bool wasElite)
        {
            AddGauge(wasElite ? gaugePerEliteKill : gaugePerKill);
        }

        // 게이지를 원하는 만큼 채우는 함수 (다른 스크립트에서도 쓸 수 있게 public).
        public void AddGauge(float amount)
        {
            if (amount <= 0f) return;

            // 발동 중에는 게이지를 채우지 않는다.
            // (안 그러면 오버드라이브로 적을 쓸어담는 동안 게이지가 다시 꽉 차서 무한히 이어진다)
            if (IsActive) return;

            // 최대치를 넘지 않도록 Mathf.Min으로 제한.
            Gauge = Mathf.Min(maxGauge, Gauge + amount);

            // 방금 막 가득 찼다면 "준비됐다"는 소리를 딱 한 번만 울려준다.
            if (Gauge >= maxGauge && !readySoundPlayed)
            {
                readySoundPlayed = true;
                AudioManager.Instance?.PlaySfx(readySound);
            }

            OnGaugeChanged?.Invoke(GaugeRatio, IsActive);
        }

        private void Update()
        {
            // 게임이 끝난 뒤(승리/패배 화면)에는 발동할 수 없게 막는다.
            bool isPlaying = GameManager.Instance == null || GameManager.Instance.State == MatchState.Playing;

            // 스페이스바를 이번 프레임에 눌렀는지 확인.
            bool pressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

            // 쓸 수 있는 상태에서 스페이스바를 누르면 발동.
            if (pressed && IsReady && isPlaying)
            {
                Activate();
            }

            // 발동 중이라면 남은 시간을 줄여나가고, 다 되면 효과를 끝낸다.
            if (IsActive)
            {
                remainingTime -= Time.deltaTime;

                // 발동 중에는 게이지 바가 "남은 시간"을 보여주도록 비율을 갱신한다.
                // (게이지가 줄어드는 모습 = 효과가 끝나가는 모습)
                Gauge = maxGauge * Mathf.Clamp01(remainingTime / duration);
                OnGaugeChanged?.Invoke(GaugeRatio, true);

                if (remainingTime <= 0f) Deactivate();
            }
        }

        // 오버드라이브를 실제로 발동시키는 함수.
        private void Activate()
        {
            IsActive = true;
            remainingTime = duration;
            readySoundPlayed = false;

            // 드론들과 플레이어 이동이 이 값을 읽어서 빨라진다.
            AttackSpeedMultiplier = attackSpeedMultiplier;
            MoveSpeedMultiplier = moveSpeedMultiplier;

            // 받는 피해를 늘린다 — 이게 오버드라이브의 대가(리스크)다.
            if (health != null) health.DamageTakenMultiplier = damageTakenMultiplier;

            AudioManager.Instance?.PlaySfx(activateSound);
            Debug.Log($"[Overdrive] 발동 - 경과 시간 {(GameManager.Instance != null ? GameManager.Instance.ElapsedTime : 0f):F1}초");

            OnOverdriveStarted?.Invoke();
            OnGaugeChanged?.Invoke(GaugeRatio, true);
        }

        // 지속 시간이 끝나 효과를 되돌리는 함수.
        private void Deactivate()
        {
            IsActive = false;
            remainingTime = 0f;
            Gauge = 0f;

            // 모든 배율을 원래대로 복구.
            AttackSpeedMultiplier = 1f;
            MoveSpeedMultiplier = 1f;
            if (health != null) health.DamageTakenMultiplier = 1f;

            AudioManager.Instance?.PlaySfx(endSound);

            OnOverdriveEnded?.Invoke();
            OnGaugeChanged?.Invoke(GaugeRatio, false);
        }
    }
}
