using System;
using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 뽑기의 "전체 흐름"을 이어주는 매니저. 화면(UI)은 이 클래스의 PullSingle / PullTen만 부르면 된다.
    //
    // 한 번의 뽑기는 이 순서로 처리된다:
    //   1) 코어를 먼저 차감한다 (모자라면 여기서 끝: 차감도 뽑기도 없이 "부족" 알림만 발생)
    //   2) GachaSystem으로 등급·드론 종류를 뽑는다 (천장 처리 포함)
    //   3) 결과를 DroneInventory에 반영한다 (NEW / 승급 / 중복 조각)
    //   4) 천장 카운트를 저장 데이터에 기록하고 파일로 저장한다
    //
    // 각 부품이 하는 일: GachaSystem = 확률 굴리기, CurrencyManager = 코어, DroneInventory = 보유 목록.
    // 이 클래스는 그 셋을 올바른 순서로 호출만 한다.
    //
    // CurrencyManager와 같은 방식: 메인 메뉴 씬에 한 번 놓아두면 DontDestroyOnLoad로 씬이 바뀌어도 유지된다.
    public class GachaController : MonoBehaviour
    {
        public static GachaController Instance { get; private set; }

        // 확률·비용·천장이 들어있는 데이터 파일. 인스펙터에서 연결한다.
        [SerializeField] private GachaTable _gachaTable;

        private SaveData _data;
        private GachaSystem _system;
        private CurrencyManager _currency;
        private DroneInventory _inventory;
        private bool _saveOnChange;
        private bool _isReady;

        // 천장 카운트(마지막 SSR 이후 누적 뽑기 횟수)와 천장 횟수. 천장 게이지 "n / 70"에 쓴다.
        public int PityCount => _system != null ? _system.PityCount : 0;
        public int PityLimit => _gachaTable != null ? _gachaTable.PityCount : 0;

        public int SingleCost => _gachaTable != null ? _gachaTable.SingleCost : 0;
        public int TenPullCost => _gachaTable != null ? _gachaTable.TenPullCost : 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // 다른 매니저(CurrencyManager, DroneInventory)는 각자 Awake에서 Instance를 등록한다.
        // 씬의 Awake 실행 순서는 보장되지 않으므로, 모든 Awake가 끝난 뒤인 Start에서 연결해야 확실히 준비되어 있다.
        private void Start()
        {
            if (Instance != this) return; // 중복으로 생겨서 곧 사라질 오브젝트는 건너뛴다
            Initialize(SaveManager.Data, _gachaTable, CurrencyManager.Instance, DroneInventory.Instance, new System.Random(), true);
        }

        // 필요한 부품을 받아서 준비한다. 검증 도구처럼 씬 없이 쓰고 싶을 때(난수 시드 고정, 저장 끄기) 직접 부른다.
        public void Initialize(SaveData data, GachaTable table, CurrencyManager currency, DroneInventory inventory, System.Random random, bool saveOnChange)
        {
            _data = data;
            _gachaTable = table;
            _currency = currency;
            _inventory = inventory;
            _saveOnChange = saveOnChange;

            _isReady = _data != null && _gachaTable != null && _currency != null && _inventory != null && random != null;
            if (!_isReady)
            {
                Debug.LogWarning("[Gacha] GachaTable, CurrencyManager, DroneInventory 중 준비되지 않은 것이 있어 뽑기를 사용할 수 없습니다. 메인 메뉴 씬부터 시작했는지 확인해주세요.");
                return;
            }

            // 저장돼 있던 천장 카운트에서 이어서 시작한다.
            _system = new GachaSystem(_gachaTable, random, _data.gachaPityCount);
            Debug.Log($"[Gacha] 준비 완료: 천장 {PityCount} / {PityLimit}, 1회 {SingleCost}코어, 10연 {TenPullCost}코어");
        }

        public bool CanAffordSingle() => _isReady && _currency.CanAffordCore(_gachaTable.SingleCost);
        public bool CanAffordTen() => _isReady && _currency.CanAffordCore(_gachaTable.TenPullCost);

        // 1회 뽑기 (코어 SingleCost).
        public GachaPullReport PullSingle()
        {
            return Pull(_isReady ? _gachaTable.SingleCost : 0, 1);
        }

        // 10연 뽑기 (코어 TenPullCost — 9회 가격). 1회 뽑기를 순서대로 10번 처리한다.
        public GachaPullReport PullTen()
        {
            return Pull(_isReady ? _gachaTable.TenPullCost : 0, GachaSystem.TenPullCount);
        }

        private GachaPullReport Pull(int cost, int count)
        {
            if (!_isReady) return GachaPullReport.Failed(GachaPullFailure.NotReady, 0);

            // 1) 코어부터 차감한다. 모자라면 차감도 뽑기도 하지 않고 CurrencyManager가 "부족" 이벤트만 알린다.
            if (!_currency.TrySpendCore(cost))
            {
                Debug.Log($"[Gacha] 코어 부족으로 {count}회 뽑기를 하지 못했습니다 (필요 {cost}, 보유 {_currency.Core})");
                return GachaPullReport.Failed(GachaPullFailure.InsufficientCore, PityCount);
            }

            // 2) 뽑기  3) 보유 목록에 반영
            var pulls = new GachaPullResult[count];
            var outcomes = new InventoryPullOutcome[count];
            for (int i = 0; i < count; i++)
            {
                pulls[i] = _system.PullSingle();
                outcomes[i] = _inventory.Apply(pulls[i]);
            }

            // 4) 천장 카운트 저장
            _data.gachaPityCount = _system.PityCount;
            if (_saveOnChange) SaveManager.Save();

            LogReport(count, cost, pulls, outcomes);
            return new GachaPullReport(cost, pulls, outcomes, _system.PityCount);
        }

        // CBT에서 뽑기 기록을 확인할 수 있도록 한 번의 뽑기를 콘솔에 한 줄로 요약한다.
        // 코어 잔액을 함께 찍어서 매번 문구가 달라지게 한다 (콘솔 Collapse에 묻히지 않도록).
        private void LogReport(int count, int cost, GachaPullResult[] pulls, InventoryPullOutcome[] outcomes)
        {
            int[] rarityCounts = new int[Enum.GetValues(typeof(GachaRarity)).Length];
            int newCount = 0, promotedCount = 0, shards = 0;
            for (int i = 0; i < pulls.Length; i++)
            {
                rarityCounts[(int)pulls[i].rarity]++;
                if (outcomes[i].outcome == PullOutcome.New) newCount++;
                else if (outcomes[i].outcome == PullOutcome.Promoted) promotedCount++;
                shards += outcomes[i].shardsGained;
            }

            Debug.Log($"[Gacha] {count}회 뽑기 완료: N {rarityCounts[0]} / R {rarityCounts[1]} / SR {rarityCounts[2]} / SSR {rarityCounts[3]}" +
                      $" · 신규 {newCount} · 승급 {promotedCount} · 조각 +{shards}" +
                      $" · 코어 -{cost} → 잔액 {_currency.Core} · 천장 {_system.PityCount}/{PityLimit}");
        }
    }
}
