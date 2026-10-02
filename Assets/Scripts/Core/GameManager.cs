using System;
using UnityEngine;
using SurvivalDrone.Meta;

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

        // 적이 나오는 시간(초) = 한 판의 "기본 길이". 기본값 600초(10분).
        // 이 시간이 지나면 적이 더 나오지 않고, 남아 있는 적을 모두 처치해야 승리한다(EnemySpawner가 확인).
        [SerializeField] private float matchDuration = 600f;

        // 승리했을 때 재생할 효과음. 사운드 파일이 아직 없다면 비워둬도 안전하다.
        [SerializeField] private AudioClip victorySound;

        // 패배했을 때 재생할 효과음.
        [SerializeField] private AudioClip defeatSound;

        // 플레이 중 계속 재생할 배경음악.
        [SerializeField] private AudioClip inGameMusic;

        public float MatchDuration => matchDuration;

        // 판을 시작한 실제 시각(Time.realtimeSinceStartup 기준). 실제 소요 시간을 재기 위한 기준점이다.
        private float _matchStartRealtime;

        // 이번 판에 실제로 걸린 시간(초). 게임 시간(ElapsedTime)과 달리 일시정지·레벨업 선택 화면에서 멈춘 시간도 포함한다.
        // 판이 끝나는 순간에 정해지고, 그 전에는 0이다. 테스트(CBT) 기록에 남겨서 "한 판이 실제로 몇 분인지" 판단하는 근거로 쓴다.
        public float RealElapsedSeconds { get; private set; }

        // 이번 판이 끝났을 때 받은 보상. 결과 화면이 "획득 보상" 패널에 보여준다. (판이 끝나기 전에는 0)
        public int RewardCore { get; private set; }
        public int RewardCredit { get; private set; }

        // 게임이 시작된 뒤 흐른 시간(초). Update()에서 매 프레임 누적된다.
        public float ElapsedTime { get; private set; }

        // 적이 나오는 남은 시간 = 전체 시간 - 흐른 시간. 0보다 작아지지 않도록 Mathf.Max로 보정.
        public float TimeRemaining => Mathf.Max(0f, matchDuration - ElapsedTime);

        // 적이 나오는 시간이 끝났는지. true가 된 뒤에는 새 적이 나오지 않고, 남은 적을 모두 잡으면 승리한다.
        public bool SpawningEnded => ElapsedTime >= matchDuration;

        // 지금 살아 있는 적의 수. EnemySpawner가 매 프레임 알려준다(화면 위쪽 "남은 적" 표시용).
        public int RemainingEnemies { get; private set; }

        public void SetRemainingEnemies(int count)
        {
            RemainingEnemies = count;
        }

        // 적이 나오는 시간이 끝난 뒤 마지막 적까지 모두 처치했을 때 EnemySpawner가 부른다. 승리 처리를 시작한다.
        public void ReportAllEnemiesDefeated()
        {
            Win();
        }

        // 현재 매치 상태. 기본값은 진행 중(Playing).
        public MatchState State { get; private set; } = MatchState.Playing;

        // 상태가 바뀔 때(승리/패배) 다른 스크립트(UI 등)에게 알려주는 이벤트.
        public event Action<MatchState> OnStateChanged;

        private void Awake()
        {
            // 씬에서 가장 먼저 생성될 때 자기 자신을 Instance에 등록.
            Instance = this;
            _matchStartRealtime = Time.realtimeSinceStartup;
        }

        private void Start()
        {
            // AudioManager.Instance는 AudioManager 자신의 Awake()에서 등록되는데,
            // 어느 오브젝트의 Awake()가 먼저 실행될지는 보장되지 않는다(실행 순서 미지정 시).
            // 반면 Start()는 씬의 모든 Awake()가 다 끝난 뒤에만 호출되므로,
            // 여기서 불러야 AudioManager.Instance가 확실히 준비되어 있다.
            AudioManager.Instance?.PlayMusic(inGameMusic);
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
            // 시간이 다 돼도 바로 승리하지 않는다: 적이 나오는 시간이 끝난 뒤 남은 적을 모두 잡았을 때
            // EnemySpawner가 ReportAllEnemiesDefeated()를 불러 승리 처리한다.
            ElapsedTime += Time.deltaTime;

            CheckTimeMilestones();
        }

        // 이번 판에서 이미 확인한(도달 처리한) 시간 마일스톤. 3분·6분 순서이고, 한 판에 한 번만 확인하기 위한 표시다.
        private readonly bool[] _milestoneReached = new bool[MatchMilestones.Seconds.Length];

        // 3분·6분에 도달했는지 확인하고, 계정에서 처음 도달한 것이면 "달성"으로 기록한다.
        // 코어는 여기서 주지 않는다. 로비의 마일스톤 창에서 "획득"을 눌러야 받는다.
        private void CheckTimeMilestones()
        {
            for (int i = 0; i < MatchMilestones.Seconds.Length; i++)
            {
                if (_milestoneReached[i] || ElapsedTime < MatchMilestones.Seconds[i]) continue;
                _milestoneReached[i] = true;
                RecordDailyQuestDone(i);
                MarkMilestoneDone(i, $"{Mathf.RoundToInt(MatchMilestones.Seconds[i] / 60f)}분 달성");
            }
        }

        // 일일 퀘스트를 달성 처리하고 저장한다. 퀘스트 번호(0: 3분, 1: 6분, 2: 클리어)는 마일스톤 번호와 같다.
        // 코어는 로비의 일일 퀘스트 창에서 "받기"를 눌러야 지급된다.
        // 로비를 거치지 않고 InGame만 실행한 경우(CurrencyManager 없음)에는 사용자의 저장 파일을 건드리지 않도록 건너뛴다.
        private void RecordDailyQuestDone(int questIndex)
        {
            if (CurrencyManager.Instance == null) return;

            DailyQuests.MarkDone(SaveManager.Data, questIndex);
            SaveManager.Save();
        }

        // 마일스톤 하나를 "달성"으로 기록하고, 처음 달성한 것이면 화면에 안내 문구를 띄운다. 마일스톤은 스테이지마다 따로라서
        // 지금 플레이 중인 스테이지의 기록에 적는다. 이미 달성했거나 받은 마일스톤이면 조용히 넘어간다.
        // (InGame만 실행한 경우도 저장 파일을 건드리지 않도록 건너뜀)
        private void MarkMilestoneDone(int index, string title)
        {
            if (CurrencyManager.Instance == null) return;

            int stageIndex = StageProgress.Instance != null ? StageProgress.Instance.SelectedIndex : 0;
            var data = SaveManager.Data;
            if (!MatchMilestones.MarkDone(data, stageIndex, index)) return;

            SaveManager.Save();
            SurvivalDrone.UI.HUDNotice.Instance?.Show($"스테이지 {stageIndex + 1} 마일스톤 {title}!  로비에서 코어 +{MatchMilestones.GetCore(stageIndex, index)}를 받을 수 있어요");
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
            RealElapsedSeconds = Time.realtimeSinceStartup - _matchStartRealtime;
            StageProgress.Instance?.RecordMatchResult(false, ElapsedTime);
            GrantReward(false);
            AudioManager.Instance?.PlaySfx(defeatSound);
            OnStateChanged?.Invoke(State);
        }

        // 시간을 다 버텨서 승리했을 때 실행되는 함수.
        private void Win()
        {
            if (State != MatchState.Playing) return;
            State = MatchState.Won;
            Time.timeScale = 0f;
            // 승리 시점은 항상 비슷한 시간(적이 나오는 시간 직후)이라 F0로 찍으면 콘솔의 "중복 묶기"에 걸려
            // 이전 승리 기록과 같은 줄로 합쳐진다. 소수점까지 찍어서 매번 다른 문구가 되게 한다.
            Debug.Log($"[Match] 승리 - 경과 시간 {ElapsedTime:F2}초");
            RealElapsedSeconds = Time.realtimeSinceStartup - _matchStartRealtime;
            StageProgress.Instance?.RecordMatchResult(true, ElapsedTime);
            // 계정에서 처음 클리어했다면 "처음 클리어" 마일스톤을 달성으로 기록한다. (코어는 로비에서 받는다)
            MarkMilestoneDone(MatchMilestones.ClearIndex, "처음 클리어");
            RecordDailyQuestDone(MatchMilestones.ClearIndex);
            GrantReward(true);
            AudioManager.Instance?.PlaySfx(victorySound);
            GameEvents.RaiseMatchWon();
            OnStateChanged?.Invoke(State);
        }

        // 판이 끝났을 때 재화 보상을 지급한다. 클리어면 스테이지별 클리어 보상, 실패면 버틴 시간에 비례한 보상(1분 미만이면 없음).
        // 결과 화면(OnStateChanged를 받는 쪽)이 지급된 양을 읽을 수 있도록, 이벤트를 보내기 전에 호출해야 한다.
        // CurrencyManager가 없으면(InGame 씬만 단독으로 실행한 경우) 보상 없이 넘어간다.
        private void GrantReward(bool cleared)
        {
            if (CurrencyManager.Instance == null)
            {
                Debug.LogWarning("[Currency] CurrencyManager가 없어 판 보상을 지급하지 못했습니다. 메인 메뉴 씬부터 시작했는지 확인해주세요.");
                return;
            }

            // 스테이지 번호는 1부터 시작한다. 로비를 거치지 않고 InGame만 실행한 경우(StageProgress 없음)에는 스테이지 1로 본다.
            int stageNumber = StageProgress.Instance != null ? StageProgress.Instance.SelectedIndex + 1 : 1;
            CurrencyManager.Instance.GrantMatchReward(cleared, stageNumber, ElapsedTime, matchDuration, out int core, out int credit);
            RewardCore = core;
            RewardCredit = credit;
        }
    }
}
