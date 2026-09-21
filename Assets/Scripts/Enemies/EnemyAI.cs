using System;
using System.Collections.Generic;
using UnityEngine;
using SurvivalDrone.Core;
using SurvivalDrone.Pickups;

namespace SurvivalDrone.Enemies
{
    // 적 하나하나의 행동(플레이어 추적, 부딪히면 피해주기, 죽으면 XP 드랍)을 담당하는 스크립트.
    //
    // [RequireComponent(typeof(Health))]는 이 스크립트가 붙은 오브젝트에
    // Health(체력) 컴포넌트가 반드시 있어야 한다는 뜻. 없으면 자동으로 추가됨.
    [RequireComponent(typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        // 부딪힌 뒤 다시 피해를 줄 수 있을 때까지의 간격(초). 매 프레임 부딪혔다고 계속 때리지 않기 위함.
        [SerializeField] private float contactDamageInterval = 1f;

        // 이 거리 안에 플레이어가 들어오면 "부딪혔다"고 판정하는 거리.
        [SerializeField] private float contactRange = 1f;

        // 현재 살아있는 모든 적을 담아두는 목록. static이라서 모든 EnemyAI가 공유한다.
        // 저격 드론이 "가장 먼 적"을 찾을 때 씬 전체를 뒤지지 않고 이 목록만 보면 되므로 훨씬 빠르다.
        public static readonly List<EnemyAI> ActiveEnemies = new List<EnemyAI>();

        // 이 적이 어떤 종류인지에 대한 데이터(체력, 속도, 피해량 등).
        private EnemyDefinition definition;

        // 같은 오브젝트에 붙어있는 Health 컴포넌트.
        private Health health;

        // 쫓아갈 대상(플레이어)의 Transform.
        private Transform target;

        // 죽었을 때 생성할 XP 오브의 프리팹.
        private GameObject xpOrbPrefab;

        // 다음 접촉 피해까지 남은 시간.
        private float contactTimer;

        // 이 적이 죽었을 때 스포너(EnemySpawner)에게 알려주기 위한 콜백 함수.
        private Action<EnemyAI> onDeathCallback;

        // 난이도 배율이 적용된 실제 접촉 피해량. definition.contactDamage에 매번 배율을
        // 곱하지 않도록 Initialize 시점에 한 번만 계산해서 저장해둔다.
        private float scaledContactDamage;

        // ── 엘리트 로봇 관련 ──
        // 엘리트는 가끔 섞여 나오는 "강화판" 적이다. 체력이 훨씬 많고 크고 색이 다르지만,
        // 잡으면 XP를 많이 주고 오버드라이브 게이지를 왕창 채워준다.
        // 있는 이유: 모든 적이 똑같으면 "아무나 잡으면 된다"가 되는데,
        // 엘리트가 있으면 "저놈부터 잡을까?"라는 우선순위 판단이 생겨서 전투가 덜 단조로워진다.

        // 이 적이 엘리트인지 여부. EnemySpawner가 스폰 직후 MakeElite()를 불러서 정해준다.
        private bool isElite;

        // 엘리트일 때 체력이 몇 배가 되는지.
        [SerializeField] private float eliteHealthMultiplier = 3f;

        // 엘리트일 때 주는 XP가 몇 배가 되는지.
        [SerializeField] private float eliteXpMultiplier = 3f;

        // 엘리트일 때 겉보기 크기가 몇 배가 되는지 (한눈에 구분되도록).
        [SerializeField] private float eliteScaleMultiplier = 1.35f;

        // 엘리트 표시용 색상 (시안색 — 게임 전체의 강조색과 맞춤).
        [SerializeField] private Color eliteTint = new Color(0.31f, 0.847f, 0.91f);

        // 외부에서 이 적이 엘리트인지 확인할 수 있게 해주는 프로퍼티.
        public bool IsElite => isElite;

        // 외부에서 이 적의 종류 데이터를 읽을 수 있게 해주는 프로퍼티.
        public EnemyDefinition Definition => definition;

        // 적이 생성(스폰)된 직후, EnemySpawner가 호출해서 필요한 정보를 채워주는 초기화 함수.
        // 프리팹 자체에는 어떤 데이터를 쓸지 미리 정해져 있지 않기 때문에, 스폰될 때마다 주입해준다.
        public void Initialize(EnemyDefinition def, Transform playerTarget, GameObject orbPrefab, Action<EnemyAI> onDeath)
        {
            definition = def;
            target = playerTarget;
            xpOrbPrefab = orbPrefab;
            onDeathCallback = onDeath;

            // 난이도(쉬움/보통/어려움)에 따라 체력/접촉 피해량을 낮춰준다. 어려움은 1배(원래 밸런스 그대로).
            float difficultyScale = GameDifficulty.EnemyStatMultiplier;
            float scaledMaxHealth = def.maxHealth * difficultyScale;
            scaledContactDamage = def.contactDamage * difficultyScale;

            // 엘리트라면 체력만 크게 올려준다.
            // (접촉 피해량은 일부러 안 올렸다 — 엘리트를 "더 아픈 적"이 아니라
            //  "잡는 데 오래 걸리지만 보상이 큰 적"으로 만들기 위함. 그래야 플레이어가
            //  피하지 않고 노리게 된다.)
            if (isElite) scaledMaxHealth *= eliteHealthMultiplier;

            health = GetComponent<Health>();
            // 난이도가 반영된 체력으로 설정하고, 가득 채운 상태로 시작.
            health.SetMaxHealth(scaledMaxHealth, scaledMaxHealth);
            // 체력이 0이 되면 HandleDeath 함수가 자동으로 호출되도록 연결.
            health.OnDeath += HandleDeath;
        }

        // 이 적을 "엘리트"로 만드는 함수.
        // 반드시 Initialize()보다 먼저 호출해야 한다 — Initialize에서 체력을 정할 때
        // 엘리트 여부를 보고 배율을 곱하기 때문이다. (EnemySpawner가 순서를 지켜서 호출한다)
        public void MakeElite()
        {
            isElite = true;

            // 한눈에 알아볼 수 있도록 크기를 키운다.
            transform.localScale *= eliteScaleMultiplier;

            // 색을 시안색 계열로 물들여서 일반 적과 구분되게 한다.
            // MaterialPropertyBlock을 쓰는 이유: 머티리얼 원본을 복제하지 않고 이 오브젝트만
            // 색을 바꿀 수 있어서, 엘리트가 여러 마리 나와도 메모리 낭비가 없다.
            // (피격 시 색 번쩍임을 담당하는 DamageFlash도 같은 방식을 쓴다)
            var renderers = GetComponentsInChildren<Renderer>();
            var block = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", eliteTint);
                r.SetPropertyBlock(block);
            }

            // 피격 시 색이 번쩍였다가 되돌아갈 "원래 색"도 엘리트 색으로 바꿔준다.
            // 이걸 안 해주면 한 대 맞는 순간 원래 흰색으로 되돌아가서 엘리트 표시가 사라진다.
            var flash = GetComponent<Core.DamageFlash>();
            if (flash != null) flash.SetBaseColor(eliteTint);
        }

        // 오브젝트가 활성화될 때 살아있는 적 목록에 자신을 추가.
        private void OnEnable()
        {
            ActiveEnemies.Add(this);
        }

        // 오브젝트가 비활성화되거나 파괴될 때 목록에서 자신을 제거.
        private void OnDisable()
        {
            ActiveEnemies.Remove(this);
        }

        private void Update()
        {
            // 아직 초기화되지 않았으면(Initialize가 호출되기 전) 아무것도 하지 않는다.
            if (target == null || definition == null) return;

            // 플레이어 방향 벡터를 구한다. y값은 무시해서 위아래가 아니라 바닥 기준 수평 방향만 계산.
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            // 아주 가깝지 않다면 플레이어 방향으로 이동하고, 그 방향을 바라보도록 회전.
            if (distance > 0.05f)
            {
                Vector3 dir = toTarget / distance;
                transform.position += dir * definition.moveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }

            // 접촉 피해 쿨타임을 줄여나간다.
            contactTimer -= Time.deltaTime;

            // 플레이어와 충분히 가깝고, 쿨타임이 다 됐으면 피해를 준다.
            if (distance <= contactRange && contactTimer <= 0f)
            {
                var playerHealth = target.GetComponent<Health>();
                playerHealth?.TakeDamage(scaledContactDamage);
                contactTimer = contactDamageInterval;
            }
        }

        // 드론에게 공격받았을 때 호출되는 함수. 드론들이 이 함수를 통해서만 피해를 줄 수 있다.
        public void ApplyDamage(float amount)
        {
            health.TakeDamage(amount);
        }

        // 체력이 0이 되어 죽었을 때 호출되는 함수.
        private void HandleDeath()
        {
            // 게임 전체에 "적이 죽었다"는 신호를 보낸다.
            // 엘리트였는지도 함께 알려줘서, 오버드라이브 게이지를 얼마나 채울지 판단하게 한다.
            GameEvents.RaiseEnemyKilled(transform.position, isElite);

            // 죽은 위치에 XP 오브를 하나 만들고, 이 적의 xpReward 값을 지급하도록 설정.
            if (xpOrbPrefab != null)
            {
                var orbObj = Instantiate(xpOrbPrefab, transform.position, Quaternion.identity);
                var orb = orbObj.GetComponent<XPOrb>();
                if (orb != null && definition != null)
                {
                    // 엘리트는 XP도 몇 배로 준다 (잡는 데 오래 걸린 만큼 보상).
                    float reward = definition.xpReward * (isElite ? eliteXpMultiplier : 1f);
                    orb.SetValue(reward);
                }
            }

            // 스포너에게 "나 죽었어, 살아있는 목록에서 빼줘"라고 알림.
            onDeathCallback?.Invoke(this);

            // 이 적 오브젝트를 씬에서 완전히 제거.
            Destroy(gameObject);
        }
    }
}
