using System;
using System.Collections.Generic;
using System.Text;
using SurvivalDrone.Drones;

namespace SurvivalDrone.Meta
{
    // 락온 뽑기 한 판이 지금 어느 단계인지.
    //  Idle   = 아직 시작 전 (또는 한 판을 끝내고 초기화된 상태)
    //  Active = 3칸이 공개돼 있고 잠그기·재뽑기를 하는 중
    //  Done   = 확정해서 끝남 (결과를 보여주는 중)
    public enum LockOnPhase { Idle, Active, Done }

    // 요청이 실패한 이유.
    //  InsufficientCore = 코어가 모자람 (아무것도 차감·변경하지 않음)
    //  WrongPhase       = 지금 단계에서는 할 수 없는 동작 (예: 시작 전에 재뽑기)
    //  NothingToReroll  = 다시 뽑을 칸이 없음 (3칸을 모두 잠근 상태)
    //  NotReady         = 필요한 매니저(재화·보유 드론)나 수치표가 준비되지 않음
    public enum LockOnFailure { None, InsufficientCore, WrongPhase, NothingToReroll, NotReady }

    // 공개된 칸 하나. locked가 true인 칸은 재뽑기에서 제외되고, 확정하면 받는다.
    public struct LockOnSlot
    {
        public GachaRarity rarity;
        public DroneType drone;
        public bool locked;
    }

    // 확정한 뒤 칸 하나가 어떻게 처리됐는지. 결과 화면이 카드에 NEW / 승급 / 조각을 표시할 때 쓴다.
    public readonly struct LockOnSlotResult
    {
        public readonly GachaRarity rarity;
        public readonly DroneType drone;

        // true = 잠근 칸이라 보유 목록에 반영됨(outcome이 의미 있음), false = 안 가져간 칸(조각으로 환산됨).
        public readonly bool received;
        public readonly InventoryPullOutcome outcome;

        // 안 가져간 칸에서 환산된 조각 수와, 그 조각이 들어간 드론. (받은 칸이면 0)
        public readonly int convertedShards;
        public readonly DroneType convertedTarget;

        public LockOnSlotResult(GachaRarity rarity, DroneType drone, bool received, InventoryPullOutcome outcome, int convertedShards, DroneType convertedTarget)
        {
            this.rarity = rarity;
            this.drone = drone;
            this.received = received;
            this.outcome = outcome;
            this.convertedShards = convertedShards;
            this.convertedTarget = convertedTarget;
        }
    }

    // 확정 한 번의 전체 결과.
    public readonly struct LockOnConfirmReport
    {
        public readonly bool success;
        public readonly LockOnFailure failure;
        public readonly LockOnSlotResult[] slots;

        // 이번 판에서 쓴 코어 합계(첫 공개 + 재뽑기들)와 재뽑기 횟수.
        public readonly int spentCore;
        public readonly int rerollCount;

        public LockOnConfirmReport(LockOnSlotResult[] slots, int spentCore, int rerollCount)
        {
            success = true;
            failure = LockOnFailure.None;
            this.slots = slots;
            this.spentCore = spentCore;
            this.rerollCount = rerollCount;
        }

        private LockOnConfirmReport(LockOnFailure failure)
        {
            success = false;
            this.failure = failure;
            slots = new LockOnSlotResult[0];
            spentCore = 0;
            rerollCount = 0;
        }

        public static LockOnConfirmReport Failed(LockOnFailure failure) => new LockOnConfirmReport(failure);
    }

    // 락온 뽑기 "한 판"의 두뇌. 시작(첫 공개) → 잠그기 → 재뽑기(여러 번) → 확정 흐름을 처리한다.
    //
    // GachaController와 달리 MonoBehaviour가 아니라 그냥 C# 클래스다. 화면(LockOnUI)이 열릴 때 만들어 쓰고,
    // 검증 도구는 임시 저장 데이터·시드 고정 난수를 넣어 씬 없이 돌린다. 코어는 단계마다 그 자리에서 낸다(선결제 후 환불이 아님).
    //
    // 이 클래스가 하는 일 / 하지 않는 일:
    //  - 하는 일: 코어 차감(CurrencyManager), 칸 굴리기(LockOnTable), 확정 때 보유 목록 반영(DroneInventory), 테스트 로그(PlayLog).
    //  - 하지 않는 일: 천장·소천장(락온 뽑기에는 없다), 저장 데이터에 진행 중인 판 저장(탭을 닫으면 진행 중인 판은 사라진다).
    public class LockOnSession
    {
        private readonly LockOnTable _table;
        private readonly CurrencyManager _currency;
        private readonly DroneInventory _inventory;
        private readonly SaveData _data;
        private readonly Random _random;
        private readonly bool _saveOnChange;
        private readonly bool _isReady;
        private readonly bool _isSimulation;
        private readonly DroneType[] _droneTypes;

        private LockOnSlot[] _slots;

        public LockOnPhase Phase { get; private set; } = LockOnPhase.Idle;

        // 시뮬레이터 판이면 true. 시뮬레이터는 코어를 쓰지 않고, 보유 드론·저장 데이터·테스트 로그에 아무것도 남기지 않는다.
        public bool IsSimulation => _isSimulation;

        // 확정한 판들의 누적 숫자. 시뮬레이터 화면의 "누적 N판 · 평균 사용 코어"에 쓴다.
        // 시뮬레이터에서는 "가상으로 쓴 코어"(실제로는 차감하지 않음)가 더해진다. ResetStats로 0으로 되돌린다.
        public int ConfirmedCount { get; private set; }
        public int TotalSpentCore { get; private set; }
        public int TotalRerollCount { get; private set; }
        public int TotalReceivedCount { get; private set; }

        // 이번 판에서 지금까지 쓴 코어(첫 공개 + 재뽑기들)와 재뽑기 횟수. 판이 끝나고 Reset하면 0으로 돌아간다.
        public int SpentCore { get; private set; }
        public int RerollCount { get; private set; }

        // 가장 최근 확정 결과. Done 단계에서 결과 화면이 읽는다.
        public LockOnConfirmReport LastReport { get; private set; }

        // 상태가 바뀔 때마다 알려준다. 화면이 다시 그리는 데 쓴다.
        public event Action Changed;

        public LockOnSession(LockOnTable table, CurrencyManager currency, DroneInventory inventory, SaveData data, Random random, bool saveOnChange)
        {
            _table = table;
            _currency = currency;
            _inventory = inventory;
            _data = data;
            _random = random ?? new Random();
            _saveOnChange = saveOnChange;
            _droneTypes = (DroneType[])Enum.GetValues(typeof(DroneType));
            _isReady = table != null && currency != null && inventory != null && data != null;
            _slots = new LockOnSlot[_isReady ? Math.Max(1, table.SlotCount) : 0];
        }

        // 시뮬레이터 판. 재화·보유 드론·저장 데이터가 필요 없고, 확률·가격 규칙은 실제 판과 같은 LockOnTable을 쓴다. ("표기 = 실제" 원칙)
        // random을 비워 두면 매번 다른 결과가 나온다. 검증 도구는 시드를 고정한 것을 넣는다.
        public LockOnSession(LockOnTable table, Random random = null)
        {
            _table = table;
            _random = random ?? new Random();
            _isSimulation = true;
            _droneTypes = (DroneType[])Enum.GetValues(typeof(DroneType));
            _isReady = table != null;
            _slots = new LockOnSlot[_isReady ? Math.Max(1, table.SlotCount) : 0];
        }

        public bool IsReady => _isReady;
        public int SlotCount => _slots.Length;
        public IReadOnlyList<LockOnSlot> Slots => _slots;

        public int LockedCount
        {
            get
            {
                int n = 0;
                if (Phase == LockOnPhase.Active)
                    foreach (var s in _slots) if (s.locked) n++;
                return n;
            }
        }

        // 지금 다시 뽑을 칸 수 (Active일 때만 의미 있음).
        public int UnlockedCount => Phase == LockOnPhase.Active ? _slots.Length - LockedCount : 0;

        // 처음 공개 비용.
        public int FirstCost => _isReady ? _table.FirstCost : 0;

        // 지금 재뽑기하면 드는 코어. 다시 뽑을 칸이 없거나 Active가 아니면 0.
        public int NextRerollCost => Phase == LockOnPhase.Active && UnlockedCount > 0 ? _table.GetRerollCost(UnlockedCount, LockedCount) : 0;

        // 시뮬레이터는 코어를 쓰지 않으므로 항상 할 수 있다.
        public bool CanAffordStart => _isReady && (_isSimulation || _currency.CanAffordCore(_table.FirstCost));
        public bool CanAffordReroll => NextRerollCost > 0 && (_isSimulation || _currency.CanAffordCore(NextRerollCost));

        // 이 칸을 잠글 수 있는가? (공개 중이고, 잠글 수 있는 등급(기본 SR 이상)일 때)
        public bool CanLock(int index)
        {
            return Phase == LockOnPhase.Active && index >= 0 && index < _slots.Length && _slots[index].rarity >= _table.LockMinRarity;
        }

        // 칸을 잠그거나 푼다. 풀기는 언제나 되고, 잠그기는 잠글 수 있는 등급일 때만 된다. 바뀌었으면 true.
        public bool SetLocked(int index, bool locked)
        {
            if (Phase != LockOnPhase.Active || index < 0 || index >= _slots.Length) return false;
            if (locked && !CanLock(index)) return false;
            if (_slots[index].locked == locked) return false;

            _slots[index].locked = locked;
            Changed?.Invoke();
            return true;
        }

        // ---------------- 시작 / 재뽑기 / 확정 ----------------

        // 첫 공개. 코어를 내고 모든 칸을 굴린다. 코어가 모자라면 아무것도 바꾸지 않는다.
        public LockOnFailure Start()
        {
            if (!_isReady) return LockOnFailure.NotReady;
            if (Phase != LockOnPhase.Idle) return LockOnFailure.WrongPhase;

            int cost = _table.FirstCost;
            // 시뮬레이터는 코어를 차감하지 않는다. (비용은 "가상으로 쓴 코어"로만 센다)
            if (!_isSimulation && !_currency.TrySpendCore(cost))
            {
                PlayLog.RecordLockOnBlocked(_data, "시작", cost, _currency.Core);
                SaveIfNeeded();
                return LockOnFailure.InsufficientCore;
            }

            SpentCore = cost;
            RerollCount = 0;
            for (int i = 0; i < _slots.Length; i++) _slots[i] = RollSlot();
            Phase = LockOnPhase.Active;

            if (!_isSimulation)
            {
                PlayLog.RecordLockOnStart(_data, cost, _currency.Core, SlotsSummary());
                SaveIfNeeded();
            }
            Changed?.Invoke();
            return LockOnFailure.None;
        }

        // 재뽑기. 잠그지 않은 칸만 코어를 내고 다시 굴린다. 잠근 칸은 그대로다.
        public LockOnFailure Reroll()
        {
            if (!_isReady) return LockOnFailure.NotReady;
            if (Phase != LockOnPhase.Active) return LockOnFailure.WrongPhase;
            if (UnlockedCount == 0) return LockOnFailure.NothingToReroll;

            int cost = NextRerollCost;
            if (!_isSimulation && !_currency.TrySpendCore(cost))
            {
                PlayLog.RecordLockOnBlocked(_data, "재뽑기", cost, _currency.Core);
                SaveIfNeeded();
                return LockOnFailure.InsufficientCore;
            }

            SpentCore += cost;
            RerollCount++;
            int lockedNow = LockedCount;
            for (int i = 0; i < _slots.Length; i++)
                if (!_slots[i].locked) _slots[i] = RollSlot();

            if (!_isSimulation)
            {
                PlayLog.RecordLockOnReroll(_data, RerollCount, cost, lockedNow, SlotsSummary(), _currency.Core);
                SaveIfNeeded();
            }
            Changed?.Invoke();
            return LockOnFailure.None;
        }

        // 확정. 잠근 칸은 기본 뽑기와 같은 규칙(NEW / 승급 / 중복 조각)으로 보유 목록에 넣고,
        // 안 가져간 칸은 조각으로 바꿔 보유 드론에 넣는다. 잠근 칸이 0개여도 확정할 수 있다(화면이 먼저 경고를 띄운다).
        // reason은 테스트 로그에 남는 문구("확정" 또는 화면을 나가며 자동 확정한 "나가기").
        public LockOnConfirmReport Confirm(string reason = "확정")
        {
            if (!_isReady) return LockOnConfirmReport.Failed(LockOnFailure.NotReady);
            if (Phase != LockOnPhase.Active) return LockOnConfirmReport.Failed(LockOnFailure.WrongPhase);
            if (_isSimulation) return ConfirmSimulation();

            var results = new LockOnSlotResult[_slots.Length];
            int lockedCount = 0, newCount = 0, promotedCount = 0, dupShards = 0, convShards = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot.locked)
                {
                    lockedCount++;
                    var outcome = _inventory.Apply(new GachaPullResult(slot.rarity, slot.drone, false, 0));
                    if (outcome.outcome == PullOutcome.New) newCount++;
                    else if (outcome.outcome == PullOutcome.Promoted) promotedCount++;
                    dupShards += outcome.shardsGained;
                    results[i] = new LockOnSlotResult(slot.rarity, slot.drone, true, outcome, 0, slot.drone);
                }
                else
                {
                    int shards = _inventory.AddConvertedShards(slot.drone, slot.rarity, _table.UnlockedShardPercent, out DroneType target);
                    convShards += shards;
                    results[i] = new LockOnSlotResult(slot.rarity, slot.drone, false, default, shards, target);
                }
            }

            LastReport = new LockOnConfirmReport(results, SpentCore, RerollCount);
            Phase = LockOnPhase.Done;
            AddToTotals(lockedCount);

            PlayLog.RecordLockOnConfirm(_data, reason, lockedCount, SpentCore, RerollCount, newCount, promotedCount, dupShards, convShards, _currency.Core, ResultSummary(results));
            SaveIfNeeded();
            Changed?.Invoke();
            return LastReport;
        }

        // 시뮬레이터 확정: 잠근 칸은 "시뮬레이션" 결과로만 돌려주고, 보유 드론·조각·저장 데이터·테스트 로그에는 아무것도 하지 않는다.
        private LockOnConfirmReport ConfirmSimulation()
        {
            var results = new LockOnSlotResult[_slots.Length];
            int lockedCount = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot.locked)
                {
                    lockedCount++;
                    results[i] = new LockOnSlotResult(slot.rarity, slot.drone, true, new InventoryPullOutcome(PullOutcome.Simulated, slot.rarity, 0), 0, slot.drone);
                }
                else
                {
                    results[i] = new LockOnSlotResult(slot.rarity, slot.drone, false, default, 0, slot.drone);
                }
            }

            LastReport = new LockOnConfirmReport(results, SpentCore, RerollCount);
            Phase = LockOnPhase.Done;
            AddToTotals(lockedCount);
            Changed?.Invoke();
            return LastReport;
        }

        private void AddToTotals(int lockedCount)
        {
            ConfirmedCount++;
            TotalSpentCore += SpentCore;
            TotalRerollCount += RerollCount;
            TotalReceivedCount += lockedCount;
        }

        // 누적 숫자를 0으로 되돌린다. 끝난 판(Done)이 있으면 함께 치운다. (시뮬레이터의 "누적 초기화" 버튼용)
        public void ResetStats()
        {
            ConfirmedCount = 0;
            TotalSpentCore = 0;
            TotalRerollCount = 0;
            TotalReceivedCount = 0;
            if (Phase == LockOnPhase.Done) Reset();
            else Changed?.Invoke();
        }

        // 시뮬레이터에서 진행 중인 판을 그냥 버린다. (코어를 쓰지 않았으니 아무것도 잃지 않는다. 화면을 나갈 때 쓴다)
        public void AbandonSimulation()
        {
            if (!_isSimulation || Phase != LockOnPhase.Active) return;
            Phase = LockOnPhase.Idle;
            SpentCore = 0;
            RerollCount = 0;
            for (int i = 0; i < _slots.Length; i++) _slots[i] = default;
            Changed?.Invoke();
        }

        // 끝난 판(Done)을 치우고 처음 상태(Idle)로 돌아간다. 다시 하려면 Start를 부른다.
        public void Reset()
        {
            if (Phase != LockOnPhase.Done) return;
            Phase = LockOnPhase.Idle;
            SpentCore = 0;
            RerollCount = 0;
            for (int i = 0; i < _slots.Length; i++) _slots[i] = default;
            Changed?.Invoke();
        }

        // ---------------- 도우미 ----------------

        // 칸 하나를 굴린다: 등급은 확률표(LockOnTable), 드론 종류는 5종 중 균등 확률(기본 뽑기와 같음).
        private LockOnSlot RollSlot()
        {
            return new LockOnSlot
            {
                rarity = _table.RollRarity(_random),
                drone = _droneTypes[_random.Next(_droneTypes.Length)],
                locked = false,
            };
        }

        // 테스트 로그용 한 줄 요약. 예: "SR:Melee[잠금]/N:Sniper/R:Heal"
        private string SlotsSummary()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append($"{_slots[i].rarity}:{_slots[i].drone}{(_slots[i].locked ? "[잠금]" : "")}");
            }
            return sb.ToString();
        }

        private static string ResultSummary(LockOnSlotResult[] results)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < results.Length; i++)
            {
                if (i > 0) sb.Append('/');
                var r = results[i];
                sb.Append(r.received ? $"{r.rarity}:{r.drone}[받음]" : $"{r.rarity}:{r.drone}[조각+{r.convertedShards}]");
            }
            return sb.ToString();
        }

        private void SaveIfNeeded()
        {
            if (_saveOnChange) SaveManager.Save();
        }
    }
}
