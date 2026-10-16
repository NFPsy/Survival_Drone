"""
상점 상품(코어 패키지·조각 패키지·월정액)을 샀을 때 30일 결과가 얼마나 달라지는지 보는 시험 (10/14, OBT 상점 설계용).

【이게 뭐예요?】
  econ_sim.py의 가상 플레이어에게 "1일차에 이 상품을 샀다"(또는 월정액은 30일 동안 매일 받는다)를 더해서
  무과금과 비교합니다. 상품이 "얼마나 빨리/얼마나 더" 도와주는지 숫자로 봅니다.
  게임 규칙은 econ_sim.py에서 가져오므로, 규칙을 바꾸면 거기를 먼저 고치세요.

【가정】
  - 조각 패키지의 "만능 조각"은 가진 드론 중 다음 강화까지 가장 가까운 드론에 자동으로 들어간다고 본다(후한 가정).
  - 구매는 1일차(시작 직후)에 한 번. 코어는 그날 바로 뽑기에 쓴다(econ_sim의 평소 규칙과 같음).
  - 플레이어 유형(mid, casual 등)은 econ_sim.py의 가정이며 실제 데이터가 아니다.

실행: python sweep_shop.py [인원수]      (한글이 깨지면: PYTHONIOENCODING=utf-8 python sweep_shop.py)
"""
import random
import statistics
import sys

import econ_sim as E


def add_shards(p, amount):
    """만능 조각을 넣는다: 다음 강화까지 가장 조금 남은 드론부터 채운다. 전부 최대 레벨이면 버려진다."""
    left = amount
    while left > 0:
        target, need_min = None, None
        for d, o in p.owned.items():
            if o[1] >= E.MAX_LEVEL:
                continue
            need = E.UP_SHARDS[o[1] - 1] - o[2]
            if need <= 0:
                continue
            if need_min is None or need < need_min:
                target, need_min = d, need
        if target is None:
            # 모두 조각이 넘치게 차 있으면 (크레딧을 기다리는 중) 아무 드론에나 넣는다
            for d, o in p.owned.items():
                if o[1] < E.MAX_LEVEL:
                    o[2] += left
                    return
            return
        give = min(left, need_min)
        p.owned[target][2] += give
        left -= give


def simulate_with_shop(profile, rules, buys, daily_core=0, daily_days=0, days=30, seed=None):
    """econ_sim.simulate와 같은 흐름에 1일차 구매(buys)와 월정액(daily_core)을 더한 것.
    buys = {"core": n, "credit": n, "shard": n}"""
    rng = random.Random(seed)
    p = E.Player(rules, rng)
    power130_day = None
    all5_day = None
    snap = None
    stage_done = [False] * E.NUM_STAGES
    done_days = list(profile.stage_days)
    for day in range(1, days + 1):
        login = day == 1 or rng.random() < profile.p_login
        if day == 1:
            p.gain_core(buys.get("core", 0))
            p.credit += buys.get("credit", 0)
        if login:
            if day <= daily_days:
                p.gain_core(daily_core)
            stage = sum(1 for d in done_days if d < day)
            stage_idx = min(stage, E.NUM_STAGES - 1)
            for i, d in enumerate(done_days):
                if not stage_done[i] and day >= d:
                    stage_done[i] = True
                    p.gain_core(E.MILESTONE_CORES[i])
            p.gain_core(rules.daily_attend)
            p.gain_core(rules.daily_3)
            if rng.random() < profile.p_6min:
                p.gain_core(rules.daily_6)
            if rng.random() < 1 - (1 - profile.clear_rate) ** profile.matches:
                p.gain_core(rules.daily_clear)
            for _ in range(profile.matches):
                if rng.random() < profile.clear_rate:
                    p.credit += int(E.CLEAR_CREDIT[stage_idx] * rules.clear_credit_mult)
                else:
                    p.credit += int(E.CLEAR_CREDIT[stage_idx] * rules.clear_credit_mult * profile.fail_credit_frac)
            p.spend_cores(day)
            p.spend_lock(day)
            if day == 1 and buys.get("shard", 0):
                add_shards(p, buys["shard"])     # 뽑기 뒤에 넣어야 "가진 드론"이 정해진 상태에서 들어간다
            p.try_upgrades()
            p.try_exchanges()
            p.try_upgrades()
        if power130_day is None and p.combat_power() >= 130:
            power130_day = day
        if all5_day is None and len(p.owned) == E.NUM_DRONES and all(o[1] == E.MAX_LEVEL for o in p.owned.values()):
            all5_day = day
        if day == 30:
            snap = dict(ssr=p.ssr, ups=p.upgrades, power=p.combat_power())
    return snap, power130_day, all5_day


def run_shop(profile, buys, n, daily_core=0, daily_days=0, days=60, rules=None):
    rules = rules or E.Rules()
    rows = [simulate_with_shop(profile, rules, buys, daily_core, daily_days, days, seed=1000003 + i) for i in range(n)]
    m = statistics.mean
    all5 = [r[2] if r[2] else days + 1 for r in rows]
    return {
        "SSR": m(r[0]["ssr"] for r in rows),
        "강화": m(r[0]["ups"] for r in rows),
        "전투력": m(r[0]["power"] for r in rows),
        "전Lv5중앙일": statistics.median(all5),
        "30일내Lv5%": 100 * m(1 if d <= 30 else 0 for d in all5),
    }


# (2026-10-16 메모) 이 파일은 상품의 "구성"(코어·조각 양)만 시뮬레이션하고 가격은 들어 있지 않다. 그래서 상점 가격을 새로 정해도 결과는 그대로 유효하다.
# "코어 소 600"은 10/16에 상점에서 삭제된 상품이다(비교용으로 남겨 둠).
# 시험할 상품 후보. (이름, 1일차 구매 내용, 월정액 하루 코어, 월정액 일수[, 바꾼 규칙])
SCENARIOS = [
    ("무과금(기준)",                 {}, 0, 0),
    ("입문 코어 330",                {"core": 330}, 0, 0),
    ("코어 소 600",                  {"core": 600}, 0, 0),
    ("스타터 코어1,500+크레딧1,000", {"core": 1500, "credit": 1000}, 0, 0),
    ("코어 중 3,000",                {"core": 3000}, 0, 0),
    ("코어 대 7,800",                {"core": 7800}, 0, 0),
    ("코어 특대 14,000",             {"core": 14000}, 0, 0),
    ("월정액A 즉시300+하루40x30",    {"core": 300}, 40, 30),
    ("월정액B 즉시300+하루100x30",   {"core": 300}, 100, 30),
    ("월정액C B+조각교환한도 5회",    {"core": 300}, 100, 30, E.Rules(ex_limit=5)),
    ("조각 100",                     {"shard": 100}, 0, 0),
    ("조각 200",                     {"shard": 200}, 0, 0),
    ("조각 300",                     {"shard": 300}, 0, 0),
    ("조각 700",                     {"shard": 700}, 0, 0),
]

if __name__ == "__main__":
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
    for pname in ("mid", "casual"):
        prof = E.PROFILES[pname]
        print(f"\n== 유형 {pname} (n={n}) ==")
        base = run_shop(prof, {}, n)
        for name, buys, dc, dd, *extra in SCENARIOS:
            r = run_shop(prof, buys, n, dc, dd, rules=extra[0] if extra else None)
            print(f"{name:<28} SSR {r['SSR']:.2f} ({r['SSR'] - base['SSR']:+.2f}) | 강화 {r['강화']:.1f} ({r['강화'] - base['강화']:+.1f})"
                  f" | 전투력 {r['전투력']:.0f} ({r['전투력'] - base['전투력']:+.0f})"
                  f" | 전Lv5 중앙 {r['전Lv5중앙일']:.0f}일 ({r['전Lv5중앙일'] - base['전Lv5중앙일']:+.0f})"
                  f" | 30일내Lv5 {r['30일내Lv5%']:.0f}%")
