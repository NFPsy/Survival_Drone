using System.Collections.Generic;
using UnityEngine;
using SurvivalDrone.Core;

namespace SurvivalDrone.Enemies
{
    // "적 데이터(EnemyDefinition) + 그 적의 프리팹"을 한 쌍으로 묶어두는 작은 데이터 상자.
    // [System.Serializable]을 붙여야 인스펙터의 리스트(List)에서 편집할 수 있다.
    [System.Serializable]
    public class EnemyEntry
    {
        public EnemyDefinition definition;
        public GameObject prefab;
    }

    // 시간이 지날수록 점점 더 많고 강한 적을 스폰(생성)하는 스크립트.
    // 기획서 6장의 "시간대별 난이도 곡선" 표를 그대로 코드로 옮긴 것이다.
    public class EnemySpawner : MonoBehaviour
    {
        // 적을 스폰할 기준이 되는 플레이어의 Transform.
        [SerializeField] private Transform player;

        // 적이 죽을 때 생성할 XP 오브 프리팹 (EnemyAI에게 전달해줌).
        [SerializeField] private GameObject xpOrbPrefab;

        // 일반 적 5종 중 보스를 제외한 나머지(약한/튼튼한/빠른/강한) 목록.
        [SerializeField] private List<EnemyEntry> enemyEntries = new List<EnemyEntry>();

        // 보스는 별도로 관리 (9분에 딱 한 번만 등장하므로 목록이 아니라 단일 항목).
        [SerializeField] private EnemyEntry bossEntry;

        // 플레이어를 중심으로 이 거리(반지름)에 있는 원 위에서 적을 스폰한다 (화면 밖에서 나타나도록).
        [SerializeField] private float spawnRadius = 16f;

        // ── 아래는 기획서 6장 "시간대별 난이도 곡선" 표를 코드 값으로 옮긴 부분 ──
        [Header("난이도 곡선 (기획서 6장)")]

        // 중반 구간이 시작되는 시점(초). 120초 = 2분. (초반 10분 난이도 조정: 3분 -> 2분으로 앞당김)
        [SerializeField] private float midPhaseStart = 120f;

        // 후반 구간이 시작되는 시점(초). 360초 = 6분.
        [SerializeField] private float latePhaseStart = 360f;

        // 보스가 등장하는 시점(초). 540초 = 9분.
        [SerializeField] private float bossSpawnTime = 540f;

        // 각 구간에서 "1초에 몇 마리씩" 새로 나오는지의 범위 (최소~최대).
        [SerializeField] private Vector2 earlySpawnPerSecond = new Vector2(1f, 2f);
        [SerializeField] private Vector2 midSpawnPerSecond = new Vector2(3f, 4f);
        [SerializeField] private Vector2 lateSpawnPerSecond = new Vector2(5f, 6f);

        // 각 구간에서 화면(맵)에 동시에 존재할 수 있는 최대 적 수.
        [SerializeField] private int earlyMaxAlive = 15;
        [SerializeField] private int midMaxAlive = 30;
        [SerializeField] private int lateMaxAlive = 48;

        // 현재 살아있는 적들을 직접 추적하는 목록 (최대 마릿수 제한을 확인하기 위함).
        private readonly List<EnemyAI> alive = new List<EnemyAI>();

        // "다음 적을 스폰하기까지 얼마나 시간이 쌓였는지"를 누적하는 타이머.
        private float spawnTimer;

        // 보스를 이미 스폰했는지 여부 (한 판에 한 번만 나오게 하기 위한 플래그).
        private bool bossSpawned;

        private void Update()
        {
            // GameManager가 없거나 게임이 진행 중이 아니면(승리/패배 상태) 스폰을 멈춘다.
            if (GameManager.Instance == null || GameManager.Instance.State != MatchState.Playing) return;

            // 게임이 시작된 뒤 흐른 시간을 가져온다.
            float elapsed = GameManager.Instance.ElapsedTime;

            // 아직 보스가 안 나왔고, 보스 등장 시점이 되었다면 보스를 한 번 스폰하고 이번 프레임은 종료.
            if (!bossSpawned && elapsed >= bossSpawnTime)
            {
                bossSpawned = true;
                SpawnEnemy(bossEntry);
                return;
            }

            // 현재 시간(elapsed)이 어느 구간에 속하는지에 따라 스폰 속도(rateRange)와
            // 최대 마릿수(maxAlive)를 결정한다. 뒤에서부터 검사하는 이유는
            // "가장 늦은 구간"부터 확인해야 조건이 겹치지 않기 때문.
            Vector2 rateRange;
            int maxAlive;
            if (elapsed >= latePhaseStart)
            {
                rateRange = lateSpawnPerSecond;
                maxAlive = lateMaxAlive;
            }
            else if (elapsed >= midPhaseStart)
            {
                rateRange = midSpawnPerSecond;
                maxAlive = midMaxAlive;
            }
            else
            {
                rateRange = earlySpawnPerSecond;
                maxAlive = earlyMaxAlive;
            }

            // 이미 죽어서 파괴된(null이 된) 적들을 목록에서 정리.
            alive.RemoveAll(e => e == null);

            // 이미 최대 마릿수에 도달했으면 더 이상 스폰하지 않는다.
            if (alive.Count >= maxAlive) return;

            // 이번 구간의 스폰 속도 범위 안에서 무작위로 "초당 스폰 수"를 정한다.
            float spawnsPerSecond = Random.Range(rateRange.x, rateRange.y);

            // 시간이 흐른 만큼(deltaTime) 스폰 속도를 곱해서 타이머에 누적.
            // 예: 초당 2마리면 0.5초마다 타이머가 1이 되어 한 마리씩 스폰됨.
            spawnTimer += Time.deltaTime * spawnsPerSecond;

            // 타이머가 1을 넘을 때마다 적을 한 마리씩 스폰 (한 프레임에 여러 마리가 밀릴 수도 있음).
            while (spawnTimer >= 1f && alive.Count < maxAlive)
            {
                spawnTimer -= 1f;
                SpawnEnemy(PickEntry(elapsed));
            }
        }

        // 현재 시간(elapsed) 기준으로 등장 가능한 적 종류 중 하나를 무작위로 고르는 함수.
        private EnemyEntry PickEntry(float elapsed)
        {
            var candidates = new List<EnemyEntry>();
            foreach (var entry in enemyEntries)
            {
                // 이 적의 "등장 시점(unlockTime)"이 아직 안 지났으면 후보에서 제외.
                if (entry.definition != null && elapsed >= entry.definition.unlockTime)
                {
                    candidates.Add(entry);
                }
            }

            // 혹시 후보가 하나도 없으면(설정 오류 방지용) 목록의 첫 번째를 사용.
            if (candidates.Count == 0)
            {
                return enemyEntries.Count > 0 ? enemyEntries[0] : null;
            }

            return candidates[Random.Range(0, candidates.Count)];
        }

        // 실제로 적 하나를 생성(Instantiate)하는 함수.
        private void SpawnEnemy(EnemyEntry entry)
        {
            if (entry == null || entry.prefab == null || player == null) return;

            // 플레이어를 중심으로 무작위 방향(원 둘레)을 하나 고른 뒤, spawnRadius만큼 떨어진 위치를 계산.
            // 이렇게 하면 적이 항상 플레이어 주변 "화면 밖"에서 나타나는 것처럼 보인다.
            Vector2 dir2 = Random.insideUnitCircle.normalized;
            Vector3 spawnPos = player.position + new Vector3(dir2.x, 0f, dir2.y) * spawnRadius;

            // 프리팹으로 실제 게임오브젝트를 생성.
            var obj = Instantiate(entry.prefab, spawnPos, Quaternion.identity);

            // 생성된 오브젝트의 EnemyAI 스크립트를 찾아서 필요한 정보를 넘겨주고 초기화.
            var ai = obj.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.Initialize(entry.definition, player, xpOrbPrefab, HandleEnemyDeath);
                alive.Add(ai);
            }
        }

        // 적이 죽었을 때 EnemyAI가 호출해주는 콜백 함수. 살아있는 목록에서 제거한다.
        private void HandleEnemyDeath(EnemyAI enemy)
        {
            alive.Remove(enemy);
        }
    }
}
