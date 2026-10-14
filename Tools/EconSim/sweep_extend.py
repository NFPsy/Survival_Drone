"""
"성장 상한을 늘리는 두 방안"을 시뮬레이션으로 비교한다 (10/14, OBT 상점 설계용).

  기준 : 지금 규칙 (드론 Lv5 상한, 등급 N/R/SR/SSR)
  B    : 각성 단계 — 드론 레벨 상한을 5 → 8로 늘린다 (Lv6~8은 조각·크레딧이 더 든다)
  C    : 새 등급 UR — SSR 위에 새 등급을 더한다 (확률 0.5%, 전용 천장, 등급 배율 1.8)
  B+C  : 둘 다

  econ_sim.py의 가상 플레이어와 sweep_shop.py의 상품(코어·조각·월정액)을 그대로 쓴다.
  게임 코드에는 영향이 없고, 여기 값들은 모두 "시험해 보려고 정한 가정"이다 (확정 아님).

실행: PYTHONIOENCODING=utf-8 python sweep_extend.py [인원수]
"""
import contextlib
import random
import statistics
import sys

import econ_sim as E
import sweep_shop as S

UR_ = 4                     # 새 등급 번호 (N=0, R=1, SR=2, SSR=3, UR=4)
UR_PITY = 150               # UR 전용 천장: 이 횟수째 뽑기는 UR 확정

# ---- 변형별 규칙 값 (econ_sim의 모듈 값을 잠깐 바꿔 끼운다) ----
VARIANTS = {
    "기준":  {},
    "B 각성(Lv8)": {
        "MAX_LEVEL": 8,
        "UP_SHARDS": [30, 50, 80, 120, 150, 200, 250],
        "UP_CREDITS": [300, 500, 800, 1200, 1500, 2000, 2500],
    },
    "C 새 등급 UR": {
        "RATES": [54.5, 30.0, 12.5, 2.5, 0.5],
        "DUP_SHARDS": [10, 20, 30, 50, 100],
        "GRADE_MULT": [1.0, 1.1, 1.25, 1.45, 1.8],
    },
}
VARIANTS["B+C"] = {**VARIANTS["B 각성(Lv8)"], **VARIANTS["C 새 등급 UR"]}


@contextlib.contextmanager
def patched(values):
    """econ_sim 모듈의 값을 잠깐 바꿨다가 원래대로 돌려놓는다."""
    saved = {k: getattr(E, k) for k in values}
    for k, v in values.items():
        setattr(E, k, v)
    try:
        yield
    finally:
        for k, v in saved.items():
            setattr(E, k, v)


class PlayerUR(E.Player):
    """UR 등급이 있을 때의 플레이어: 뽑기에 UR 확률과 UR 전용 천장이 더해진다."""

    def __init__(self, rules, rng):
        super().__init__(rules, rng)
        self.ur_pity = 0
        self.ur = 0

    def pull_single(self):
        r = self.rules
        self.pity += 1
        self.soft += 1
        self.ur_pity += 1
        ur_big = self.ur_pity >= UR_PITY
        big = (not ur_big) and self.pity >= r.pity
        small = (not ur_big) and (not big) and r.soft_pity > 0 and self.soft >= r.soft_pity
        if ur_big:
            rar = UR_
        elif big:
            rar = E.SSR_
            self.pity_hits += 1
        elif small:
            self.soft_triggers += 1
            x = self.rng.random() * 100.0
            rar = UR_ if x < E.RATES[UR_] else (E.SSR_ if x < E.RATES[UR_] + E.RATES[E.SSR_] else E.SR_)
        else:
            rar = E.roll_rarity(self.rng)
        if rar >= E.SSR_:
            self.pity = 0
        if rar == UR_:
            self.ur_pity = 0
            self.ur += 1
        if rar >= E.SR_:
            self.soft = 0
        drone = self.rng.randrange(E.NUM_DRONES)
        self.pulls += 1
        if rar == E.SSR_:
            self.ssr += 1
        self._apply(drone, rar)
        return rar


def simulate_days(profile, rules, buys, daily_core, daily_days, days, seed, use_ur):
    """sweep_shop.simulate_with_shop과 같은 흐름. 30일·days일째 전투력과 '다 키운 날'을 돌려준다."""
    rng = random.Random(seed)
    p = (PlayerUR if use_ur else E.Player)(rules, rng)
    snaps = {}
    done_day = None
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
            if day == 1 and buys.get("shard", 0):
                S.add_shards(p, buys["shard"])
            p.try_upgrades()
            p.try_exchanges()
            p.try_upgrades()
        if done_day is None and len(p.owned) == E.NUM_DRONES and all(o[1] >= E.MAX_LEVEL for o in p.owned.values()):
            done_day = day
        if day in (30, 60):
            snaps[day] = dict(power=p.combat_power(), ssr=p.ssr, ur=getattr(p, "ur", 0), ups=p.upgrades)
    return snaps, done_day


def ceiling():
    """전투력이 이론상 얼마까지 오를 수 있는지: 가장 센 2기가 최고 등급·최고 레벨일 때."""
    return round(2 * E.BASE_POWER * max(E.GRADE_MULT) * (1 + E.LEVEL_BONUS * (E.MAX_LEVEL - 1)))


def run_variant(profile, scenario, n, use_ur, days=90):
    name, buys, dc, dd, *extra = scenario
    rules = extra[0] if extra else E.Rules()
    rows = [simulate_days(profile, rules, buys, dc, dd, days, 1000003 + i, use_ur) for i in range(n)]
    m = statistics.mean
    done = [r[1] if r[1] else days + 1 for r in rows]
    return {
        "p30": m(r[0][30]["power"] for r in rows),
        "p60": m(r[0][60]["power"] for r in rows),
        "ssr30": m(r[0][30]["ssr"] for r in rows),
        "ur60": m(r[0][60]["ur"] for r in rows),
        "소진일": statistics.median(done),
        "소진90%": 100 * m(1 if d <= days else 0 for d in done),
    }


PACKS = [s for s in S.SCENARIOS if s[0] in ("무과금(기준)", "코어 대 7,800", "코어 특대 14,000", "조각 300", "월정액C B+조각교환한도 5회")]

if __name__ == "__main__":
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 1500
    for pname in ("max", "mid", "casual"):
        prof = E.PROFILES[pname if pname != "max" else "max(퀘스트 매일 전부)"]
        print(f"\n===== 유형 {pname} (n={n}) =====")
        for vname, values in VARIANTS.items():
            with patched(values):
                use_ur = "RATES" in values
                cap = ceiling()
                base = run_variant(prof, PACKS[0], n, use_ur)
                print(f"\n[{vname}] 이론 상한 전투력 {cap}")
                print(f"  무과금: 30일 {base['p30']:.0f} ({100 * base['p30'] / cap:.0f}%) | 60일 {base['p60']:.0f} ({100 * base['p60'] / cap:.0f}%)"
                      f" | 다 키운 날 중앙 {base['소진일']:.0f}일 | 90일 안에 다 키움 {base['소진90%']:.0f}% | 30일 SSR {base['ssr30']:.2f} | 60일 UR {base['ur60']:.2f}")
                for sc in PACKS[1:]:
                    r = run_variant(prof, sc, n, use_ur)
                    print(f"  {sc[0]:<26} 30일 전투력 {r['p30']:.0f} ({r['p30'] - base['p30']:+.0f}) | 60일 {r['p60']:.0f} ({r['p60'] - base['p60']:+.0f})"
                          f" | 다 키운 날 {r['소진일']:.0f}일 ({r['소진일'] - base['소진일']:+.0f}) | 60일 UR {r['ur60']:.2f}")
