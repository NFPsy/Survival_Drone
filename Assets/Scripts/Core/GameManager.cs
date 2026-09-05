using System;
using UnityEngine;

namespace SurvivalDrone.Core
{
    // 한 판(매치)의 상태를 나타내는 3가지 경우.
    // Playing = 진행 중, Won = 승리(시간을 다 버팀), Lost = 패배(플레이어 사망)
    public enum MatchState { Playing, Won, Lost }

    // 게임 전체의 진행 상황(타이머, 승패)을 관리하는 매니저.
    // 씬에 하나만 존재해야 하는 "싱글턴(Singleton)" 패턴으로 만들었다.
    public class GameManager : MonoBehaviour
    {
        // 어디서든 GameManager.Instance로 이 스크립트에 접근할 수 있게 해주는 정적 변수.
        public static GameManager Instance { get; private set; }

        // 한 판의 길이(초). 기획서 기준 8~10분이라 기본값을 600초(10분)로 설정.
        [SerializeField] private float matchDuration = 600f;

        public float MatchDuration => matchDuration;

        // 게임이 시작된 뒤 흐른 시간(초). Update()에서 매 프레임 누적된다.
        public float ElapsedTime { get; private set; }

        // 남은 시간 = 전체 시간 - 흐른 시간. 0보다 작아지지 않도록 Mathf.Max로 보정.
        public float TimeRemaining => Mathf.Max(0f, matchDuration - ElapsedTime);

        // 현재 매치 상태. 기본값은 진행 중(Playing).
        public MatchState State { get; private set; } = MatchState.Playing;

        // 상태가 바뀔 때(승리/패배) 다른 스크립트(UI 등)에게 알려주는 이벤트.
        public event Action<MatchState> OnStateChanged;

        private void Awake()
        {
            // 씬에서 가장 먼저 생성될 때 자기 자신을 Instance에 등록.
            Instance = this;
        }

        private void OnEnable()
        {
            // 플레이어가 죽었다는 신호(GameEvents.OnPlayerDied)를 구독해서
            // HandlePlayerDied 함수가 자동으로 호출되도록 연결한다.
            GameEvents.OnPlayerDied += HandlePlayerDied;
        }

        private void OnDisable()
        {
            // 오브젝트가 사라질 때는 구독을 반드시 해제해야 메모리 누수가 없다.
            GameEvents.OnPlayerDied -= HandlePlayerDied;
        }

        private void Update()
        {
            // 게임이 이미 끝났으면(승리/패배) 더 이상 타이머를 진행하지 않는다.
            if (State != MatchState.Playing) return;

            // 매 프레임 지난 시간(Time.deltaTime)만큼 누적.
            ElapsedTime += Time.deltaTime;

            // 시간이 다 되면 승리 처리.
            if (ElapsedTime >= matchDuration)
            {
                Win();
            }
        }

        // 플레이어 사망 신호를 받았을 때 실행되는 함수.
        private void HandlePlayerDied()
        {
            if (State != MatchState.Playing) return;
            State = MatchState.Lost;
            // 시간을 멈춰서 게임 오브젝트들의 움직임/스폰 등을 모두 정지시킨다.
            Time.timeScale = 0f;
            // 나중에 콘솔에서 "몇 초 만에 죽었는지" 복기할 수 있도록 기록해둔다.
            Debug.Log($"[Match] 패배 - 경과 시간 {ElapsedTime:F0}초");
            OnStateChanged?.Invoke(State);
        }

        // 시간을 다 버텨서 승리했을 때 실행되는 함수.
        private void Win()
        {
            if (State != MatchState.Playing) return;
            State = MatchState.Won;
            Time.timeScale = 0f;
            // 승리 시점은 항상 매치 길이(600초) 근처라 F0로 찍으면 콘솔의 "중복 묶기"에 걸려
            // 이전 승리 기록과 같은 줄로 합쳐진다. 소수점까지 찍어서 매번 다른 문구가 되게 한다.
            Debug.Log($"[Match] 승리 - 경과 시간 {ElapsedTime:F2}초");
            GameEvents.RaiseMatchWon();
            OnStateChanged?.Invoke(State);
        }
    }
}
