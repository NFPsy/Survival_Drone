using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Enemies;
using SurvivalDrone.Player;
using SurvivalDrone.UI;

namespace SurvivalDrone.Tutorial
{
    // 튜토리얼 씬(Tutorial.unity)을 처음부터 끝까지 진행하는 스크립트.
    // 이 씬은 InGame 씬을 복사해서 만들었고(HangarSceneBuilder처럼 TutorialSceneBuilder가 만든다), 일반 판과 달리 이 스크립트가 적과 보스를 직접 불러낸다.
    //
    // 세 단계를 순서대로 진행한다. (오른쪽 아래 "건너뛰기"로 단계를 넘길 수 있다)
    //   1) 이동        : WASD로 하늘색 원 3개를 차례로 밟고, 드론이 알아서 공격하는 것을 적 3마리로 확인한다.
    //   2) 오버드라이브 : 게이지를 처음부터 가득 채워 주고, 적이 몰려오는 동안 스페이스바로 발동해 보게 한다.
    //   3) 보스 패턴    : 약해지지 않는(죽지 않는) 보스가 내려찍기·돌진을 쓴다. 붉은 예고를 보고 각각 연속 2번 피하면 통과.
    //                    이 단계에서는 맞아도 피해가 없다. (맞으면 "다시 도전" 문구만 뜬다)
    //
    // 저장 데이터는 건드리지 않는다. (GameManager.IsTutorial이 켜져 있어 판 기록·보상·마일스톤이 모두 건너뛰어진다)
    // 화면 요소(안내 상자·건너뛰기·완료 창)는 TutorialSceneBuilder가 이 오브젝트의 자식으로 만들어 두며, 이름으로 찾아 연결한다.
    public class TutorialController : MonoBehaviour
    {
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private AudioClip clickSound;

        // ── 연습 규칙에 쓰는 값들 ──
        private const int CheckpointCount = 3;           // 이동 단계: 밟을 원 개수
        private const float CheckpointStep = 9f;         // 원과 원 사이 거리(m)
        private const float CheckpointRadius = 2f;       // 원 반지름(m) = 도착으로 인정하는 거리
        private const int MoveEnemyCount = 3;            // 이동 단계 마지막에 처치해 볼 적 수
        private const int OverdriveEnemyCount = 12;      // 오버드라이브 단계에서 먼저 몰려오는 적 수
        private const int OverdriveBonusEnemyCount = 10; // 발동한 순간 가까이에 추가로 나오는 적 수
        private const int DodgesNeeded = 2;              // 보스 패턴을 연속으로 피해야 하는 횟수(패턴 종류마다)
        private const float BossArenaRadius = 22f;
        private const float BossMapLimit = 49f;
        private const float BossSpawnDistance = 12f;
        private const float BossHealthMultiplier = 500f; // 사실상 안 죽게 (드론이 계속 때려도 튜토리얼이 끝나기 전에는 안 쓰러진다)

        private Text _stepText;
        private Text _messageText;
        private Text _hintText;
        private Text _progressText;
        private GameObject _completePanel;

        private Transform _player;
        private Health _playerHealth;
        private EnemySpawner _spawner;
        private OverdriveSystem _overdrive;

        private readonly List<EnemyAI> _enemies = new List<EnemyAI>();
        private bool _skip;
        private bool _noDamage;

        private void Awake()
        {
            _stepText = FindText("Guide/StepText");
            _messageText = FindText("Guide/MessageText");
            _hintText = FindText("Guide/HintText");
            _progressText = FindText("Guide/ProgressText");

            WireButton("BtnSkip", () => { PlayClick(); _skip = true; });
            WireButton("CompletePanel/Box/BtnMainMenu", () => { PlayClick(); SceneManager.LoadScene(mainMenuSceneName); });
            WireButton("CompletePanel/Box/BtnRetry", () => { PlayClick(); SceneManager.LoadScene(SceneManager.GetActiveScene().name); });

            var complete = transform.Find("CompletePanel");
            _completePanel = complete != null ? complete.gameObject : null;
            if (_completePanel != null) _completePanel.SetActive(false);
        }

        private void Start()
        {
            var playerController = FindFirstObjectByType<PlayerController>();
            _player = playerController != null ? playerController.transform : null;
            _playerHealth = _player != null ? _player.GetComponent<Health>() : null;
            _spawner = FindFirstObjectByType<EnemySpawner>();
            _overdrive = FindFirstObjectByType<OverdriveSystem>();
            if (_playerHealth != null) _playerHealth.CannotDie = true;   // 연습 중에는 죽지 않는다 (아래 Update가 체력도 채워 준다)

            if (_player == null || _spawner == null || _overdrive == null)
            {
                Debug.LogWarning("[Tutorial] 플레이어·EnemySpawner·OverdriveSystem 중 못 찾은 것이 있어 튜토리얼을 시작하지 못했습니다.");
                return;
            }
            StartCoroutine(RunTutorial());
        }

        // 연습 중에는 죽지 않게 한다. 체력이 40% 밑으로 내려가면 가득 채워 준다.
        // 보스 단계에서는 오버드라이브가 받는 피해 배율을 1로 되돌려 놓아도 다시 0으로 고정한다.
        private void Update()
        {
            if (_playerHealth == null) return;
            if (_noDamage) _playerHealth.DamageTakenMultiplier = 0f;
            if (!_playerHealth.IsDead && _playerHealth.CurrentHealth < _playerHealth.MaxHealth * 0.4f)
                _playerHealth.Heal(_playerHealth.MaxHealth);
        }

        private IEnumerator RunTutorial()
        {
            yield return Delay(1f);

            yield return RunMoveStep();
            yield return RunOverdriveStep();
            yield return RunBossStep();

            ShowComplete();
        }

        // ───────────────────────── 1단계: 이동 ─────────────────────────

        private IEnumerator RunMoveStep()
        {
            _skip = false;
            SetGuide("튜토리얼 1/3  ·  이동", "WASD 키로 움직이세요", "하늘색 원 안으로 들어가 보세요", $"0 / {CheckpointCount}");

            TutorialMarker marker = null;
            try
            {
                var points = PickCheckpoints(CheckpointCount);
                for (int i = 0; i < points.Count && !_skip; i++)
                {
                    marker = TutorialMarker.Create(points[i], CheckpointRadius);
                    var target = marker;
                    yield return WaitUntilDone(() => FlatDistance(_player.position, target.transform.position) <= target.Radius);
                    Destroy(marker.gameObject);
                    marker = null;
                    SetProgress($"{i + 1} / {CheckpointCount}");
                }

                if (!_skip)
                {
                    SetGuide(null, "공격은 드론이 알아서 합니다", "다가오는 적 3마리를 쓰러뜨려 보세요", "");
                    for (int i = 0; i < MoveEnemyCount; i++) SpawnEnemy(9f);
                    yield return WaitUntilDone(AllEnemiesGone);
                }

                SetGuide(null, "좋아요! 이동은 이게 전부예요", "", "");
                yield return Delay(1.8f);
            }
            finally
            {
                if (marker != null) Destroy(marker.gameObject);
                ClearEnemies();
            }
        }

        // 가야 할 자리 count개를 정한다. 지금 서 있는 곳에서 출발해 돌아가며(삼각형 모양) 이어지고, 건물 안이나 맵 밖은 피한다.
        private List<Vector3> PickCheckpoints(int count)
        {
            var points = new List<Vector3>();
            Vector3 current = _player.position;
            float angle = 30f;
            for (int i = 0; i < count; i++)
            {
                Vector3 candidate = current;
                for (int tries = 0; tries < 18; tries++)
                {
                    candidate = current + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * CheckpointStep;
                    if (Mathf.Abs(candidate.x) < 42f && Mathf.Abs(candidate.z) < 42f && !IsBlocked(candidate)) break;
                    angle += 20f;   // 막혔으면 방향을 조금씩 돌려 본다
                }
                points.Add(candidate);
                current = candidate;
                angle += 120f;
            }
            return points;
        }

        private static bool IsBlocked(Vector3 point)
        {
            var obstacles = EnemyObstacle.All;
            for (int i = 0; i < obstacles.Count; i++)
            {
                Vector3 probe = point;
                if (obstacles[i].PushOut(ref probe, 2.5f)) return true;
            }
            return false;
        }

        // ───────────────────────── 2단계: 오버드라이브 ─────────────────────────

        private IEnumerator RunOverdriveStep()
        {
            _skip = false;
            SetGuide("튜토리얼 2/3  ·  오버드라이브", "적이 몰려옵니다!", "게이지가 가득 찼어요. 스페이스바를 눌러 오버드라이브를 쓰세요", "");

            try
            {
                _overdrive.AddGauge(1000f);   // 처음부터 가득 채워 준다 (최대치를 넘지는 않는다)
                StartCoroutine(SpawnWave(OverdriveEnemyCount, 10f, 4f));

                yield return WaitUntilDone(() => _overdrive.IsActive);
                if (!_skip)
                {
                    SetGuide(null, "오버드라이브 발동!", "5초 동안 공격 2.5배 · 이동도 빨라져요  (대신 받는 피해가 2배)", "적을 쓸어버리세요");
                    for (int i = 0; i < OverdriveBonusEnemyCount; i++) SpawnEnemy(7f);
                    yield return WaitUntilDone(() => !_overdrive.IsActive);
                }

                SetGuide(null, "잘했어요!", "게이지는 적을 쓰러뜨릴 때마다 차요. 적이 몰린 위기의 순간에 쓰세요", "");
                yield return Delay(4f);
            }
            finally
            {
                ClearEnemies();
            }
        }

        // 적 count마리를 seconds초에 걸쳐 조금씩 내보낸다. (한꺼번에 나오면 갑자기 겹쳐 보여서)
        private IEnumerator SpawnWave(int count, float radius, float seconds)
        {
            float interval = seconds / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                SpawnEnemy(radius);
                yield return new WaitForSeconds(interval);
            }
        }

        // ───────────────────────── 3단계: 보스 패턴 피하기 ─────────────────────────

        private int _slamStreak;
        private int _dashStreak;

        private IEnumerator RunBossStep()
        {
            _skip = false;
            _slamStreak = 0;
            _dashStreak = 0;
            _noDamage = true;
            SetGuide("튜토리얼 3/3  ·  보스 패턴 피하기", "붉은 표시가 다 차기 전에 벗어나세요!",
                "내려찍기는 붉은 원 밖으로, 돌진은 붉은 띠 옆으로 피하세요. 맞아도 피해는 없어요",
                BuildBossProgress());

            GameObject ring = null;
            EnemyAI boss = null;
            BossBrain brain = null;
            try
            {
                BossArena.Begin(_player.position, BossArenaRadius, BossMapLimit);
                ring = BossFight.BuildRing(BossArena.Center, BossArena.Radius);

                boss = _spawner.SpawnFinalBoss(PickBossPosition(_player.position), BossHealthMultiplier);
                if (boss != null)
                {
                    brain = boss.gameObject.AddComponent<BossBrain>();
                    // 시작 시각을 아주 먼 미래로 줘서 "오래 끌면 분노"가 튜토리얼 중에 켜지지 않게 한다.
                    brain.Init(boss, _player, Time.time + 100000f);
                    brain.PatternResolved += HandlePatternResolved;
                }

                yield return WaitUntilDone(() => _slamStreak >= DodgesNeeded && _dashStreak >= DodgesNeeded);
            }
            finally
            {
                if (brain != null) brain.PatternResolved -= HandlePatternResolved;
                if (boss != null) Destroy(boss.gameObject);
                BossArena.End();
                if (ring != null) Destroy(ring);
                _noDamage = false;
                if (_playerHealth != null) _playerHealth.DamageTakenMultiplier = 1f;
            }

            if (_slamStreak >= DodgesNeeded && _dashStreak >= DodgesNeeded) HUDNotice.Instance?.Show("보스 패턴을 모두 피했어요!", 2f);
            yield return Delay(1.5f);
        }

        // 보스 패턴 하나가 끝날 때마다 불린다. 맞으면 그 종류의 연속 기록이 0으로 돌아간다.
        private void HandlePatternResolved(bool isSlam, bool hitPlayer)
        {
            string label = isSlam ? "내려찍기" : "돌진";
            int streak = hitPlayer ? 0 : (isSlam ? _slamStreak : _dashStreak) + 1;
            if (isSlam) _slamStreak = streak; else _dashStreak = streak;

            HUDNotice.Instance?.Show(hitPlayer ? $"{label}에 맞았어요!  (피해는 없어요) 다시 도전" : $"{label}을(를) 피했어요!", 2f);
            SetProgress(BuildBossProgress());
        }

        private string BuildBossProgress()
        {
            return $"내려찍기 피하기 {Mathf.Min(_slamStreak, DodgesNeeded)} / {DodgesNeeded}      돌진 피하기 {Mathf.Min(_dashStreak, DodgesNeeded)} / {DodgesNeeded}";
        }

        // 보스가 나타날 자리: 경기장 중심 쪽으로 플레이어에서 BossSpawnDistance만큼. 중심 근처에 서 있으면 앞쪽(+z).
        private static Vector3 PickBossPosition(Vector3 playerPosition)
        {
            Vector3 toCenter = BossArena.Center - playerPosition;
            toCenter.y = 0f;
            Vector3 direction = toCenter.magnitude > 1f ? toCenter.normalized : Vector3.forward;
            return BossArena.Clamp(playerPosition + direction * BossSpawnDistance, 2.5f);
        }

        // ───────────────────────── 끝 ─────────────────────────

        private void ShowComplete()
        {
            SetGuide("튜토리얼 완료", "", "", "");
            if (_completePanel != null) _completePanel.SetActive(true);
        }

        // ───────────────────────── 도우미 ─────────────────────────

        // 조건이 참이 되거나 "건너뛰기"가 눌릴 때까지 기다린다.
        private IEnumerator WaitUntilDone(Func<bool> done)
        {
            while (!_skip && !done()) yield return null;
        }

        // 건너뛰기를 누르면 바로 끝나는 대기.
        private IEnumerator Delay(float seconds)
        {
            float t = 0f;
            while (t < seconds && !_skip)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        private void SpawnEnemy(float radius)
        {
            var enemy = _spawner.SpawnTutorialEnemy(radius);
            if (enemy != null) _enemies.Add(enemy);
        }

        private bool AllEnemiesGone()
        {
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i] != null) return false;
            return true;
        }

        // 남은 적을 모두 없앤다. (Vanish는 "죽음" 신호를 보내지 않아서 오버드라이브 게이지가 차지 않는다)
        private void ClearEnemies()
        {
            foreach (var enemy in _enemies)
                if (enemy != null) enemy.Vanish();
            _enemies.Clear();
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // 안내 상자의 글자를 바꾼다. null을 주면 그 줄은 그대로 둔다.
        private void SetGuide(string step, string message, string hint, string progress)
        {
            if (step != null && _stepText != null) _stepText.text = step;
            if (_messageText != null) _messageText.text = message ?? "";
            if (_hintText != null) _hintText.text = hint ?? "";
            if (_progressText != null) _progressText.text = progress ?? "";
        }

        private void SetProgress(string progress)
        {
            if (_progressText != null) _progressText.text = progress;
        }

        private void PlayClick() => AudioManager.Instance?.PlaySfx(clickSound);

        private Text FindText(string path)
        {
            var child = transform.Find(path);
            var text = child != null ? child.GetComponent<Text>() : null;
            if (text == null) Debug.LogWarning($"[Tutorial] '{path}' 글자 칸을 찾지 못했습니다.");
            return text;
        }

        private void WireButton(string path, UnityEngine.Events.UnityAction action)
        {
            var child = transform.Find(path);
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogWarning($"[Tutorial] '{path}' 버튼을 찾지 못했습니다.");
                return;
            }
            button.onClick.AddListener(action);
        }
    }
}
