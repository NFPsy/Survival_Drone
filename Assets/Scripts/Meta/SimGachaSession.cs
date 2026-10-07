using System;

namespace SurvivalDrone.Meta
{
    // 뽑기 "시뮬레이터" 한 판. 재미로 무한히 뽑아보면서 확률과 천장 구조를 체험하는 기능이다.
    //
    // 실제 뽑기와 완전히 분리되어 있다:
    //  - 코어(재화)를 쓰지 않는다.
    //  - 보유 드론(DroneInventory)에 아무것도 넣지 않는다.
    //  - 천장 카운트는 이 시뮬레이터 전용(처음엔 0)이고, 저장 데이터(SaveData)나 파일에 쓰지 않는다.
    //  - 테스트(CBT) 플레이 기록(PlayLog)에도 남기지 않는다.
    // 화면을 나가면(씬이 바뀌면) 이 객체는 사라지므로 천장도 0에서 다시 시작한다.
    //
    // 확률·천장 규칙은 실제 뽑기와 같은 GachaSystem 클래스를 그대로 쓴다 ("표기 = 실제" 원칙).
    // GachaSystem은 천장 횟수를 객체 안에 따로 들고 있어서, 새로 하나 더 만들면 실제 천장과 섞이지 않는다.
    public class SimGachaSession
    {
        private readonly GachaTable _table;
        private readonly Random _random;
        private GachaSystem _system;

        // 시뮬레이터에서 지금까지 뽑은 총 횟수와, 그중 SSR이 나온 횟수.
        public int TotalPulls { get; private set; }
        public int SsrCount { get; private set; }

        // 시뮬레이터 전용 천장 / 소천장 카운트와 한도 (게이지 "n / 70", "n / 10"에 쓴다).
        public int PityCount => _system.PityCount;
        public int SoftPityCount => _system.SoftPityCount;
        public int PityLimit => _table.PityCount;
        public int SoftPityLimit => _table.SoftPityCount;

        // 뽑기를 하거나 초기화해서 숫자가 바뀔 때마다 알려준다. 화면이 게이지를 다시 그리는 데 쓴다.
        public event Action Changed;

        // random을 비워 두면 매번 다른 결과가 나온다. 검증 도구는 시드를 고정한 것을 넣는다.
        public SimGachaSession(GachaTable table, Random random = null)
        {
            if (table == null) throw new ArgumentNullException(nameof(table), "[Gacha] GachaTable이 비어 있습니다.");
            _table = table;
            _random = random ?? new Random();
            _system = new GachaSystem(_table, _random);
        }

        // 1회 뽑기. 결과 화면(GachaResultUI)이 그대로 쓸 수 있는 모양으로 돌려준다.
        public GachaPullReport PullSingle() => Pull(1);

        // 10연 뽑기.
        public GachaPullReport PullTen() => Pull(GachaSystem.TenPullCount);

        // 천장과 누적 숫자를 모두 0으로 되돌린다.
        public void Reset()
        {
            _system = new GachaSystem(_table, _random);
            TotalPulls = 0;
            SsrCount = 0;
            Changed?.Invoke();
        }

        private GachaPullReport Pull(int count)
        {
            var pulls = new GachaPullResult[count];
            var outcomes = new InventoryPullOutcome[count];
            for (int i = 0; i < count; i++)
            {
                pulls[i] = _system.PullSingle();
                // 보유 목록에 반영하지 않으므로 "시뮬레이션" 결과라는 표시만 붙인다. (조각 0)
                outcomes[i] = new InventoryPullOutcome(PullOutcome.Simulated, pulls[i].rarity, 0);

                TotalPulls++;
                if (pulls[i].rarity == GachaRarity.SSR) SsrCount++;
            }

            Changed?.Invoke();
            // 쓴 코어는 0이다.
            return new GachaPullReport(0, pulls, outcomes, _system.PityCount);
        }
    }
}
