"""
락온 뽑기(구 조립 뽑기)의 칸 단가·프리미엄을 정하기 위한 비교 스크립트 (econ_sim.py가 같은 폴더에 있어야 합니다).

실행: python sweep_lock_price.py

【전제】 10/8 설계 합의: 슬롯 = 드론 한 칸, SR 이상인 칸만 잠금, 잠근 칸만 수령(안 가져간 칸은 조각 50% 환산), SSR 없음(N55/R30/SR15/SSR0).
        SSR이 없으니 30일 SSR 상한 걱정은 없습니다. 이제 단가는 "SR 이상을 얼마나 싸게 주는가"와 "성장 속도를 얼마나 바꾸는가"로 정합니다.

【보는 지표】
  SR+당 코어  = SR 이상 1개를 얻는 데 드는 코어. 기본 뽑기(소천장 포함)는 약 1,580코어입니다.
  비율        = 락온 뽑기 SR+당 코어 ÷ 기본 뽑기 SR+당 코어. 1보다 작을수록 락온 뽑기가 싼 것.
  전략 full   = SR 이상을 잠그며 끝까지 채움
  전략 once   = 첫 공개만 하고 잠글 수 있는 칸만 받고 반복
  전략 one    = 하나라도 잠그면 멈추고 새로 시작 (잠그기 전 재뽑기는 칸당 단가만 들어서 SR 이상을 가장 싸게 모으는 방법)
  전투력30·전Lv5중앙일 = 코어 100%를 락온 뽑기에 썼을 때(상한)의 성장 속도. 기준(락온 없음)과 비교합니다.
"""
import random
from dataclasses import replace

from econ_sim import PROFILES, Rules, SR_, Player, run

N = 1500
RATES = (55.0, 30.0, 15.0, 0.0)
base = Rules(lock_keep_unlocked=False, lock_conv=0.5, lock_rates=RATES)


def normal_cost_per_sr():
    """기본 뽑기에서 SR 이상 1개를 얻는 데 드는 코어(소천장 포함)를 직접 뽑아서 구한다."""
    rng = random.Random(7)
    p = Player(Rules(), rng)
    hits, pulls = 0, 200000
    for _ in range(pulls):
        if p.pull_single() >= SR_:
            hits += 1
    return 300 * pulls / hits


NORMAL = normal_cost_per_sr()
print(f"기본 뽑기: SR 이상 1개당 약 {NORMAL:,.0f}코어\n")


def ev(rules):
    return {p.name: run(p, rules, N) for p in PROFILES.values()}


cur = ev(Rules())
print(f"기준(락온 없음): 전투력30 max/mid/cas {cur['max']['전투력30']:.0f} / {cur['mid']['전투력30']:.0f} / {cur['casual']['전투력30']:.0f}, "
      f"전Lv5중앙일 {cur['max']['전Lv5중앙일']:.0f} / {cur['mid']['전Lv5중앙일']:.0f} / {cur['casual']['전Lv5중앙일']:.0f}\n")

STRATS = ("full", "once", "one")
HDR = (f"{'첫공개/칸단가/프리미엄':<20}| " + " | ".join(f"{st:^14}" for st in STRATS) + " | 가장 싼 전략 | 전투력30 / 전Lv5중앙일 (max, full)")
print("각 전략 칸 = SR+당 코어 (기본 뽑기 대비 비율)  — 코어 100% 사용")
print(HDR)
print("-" * len(HDR))
for first in (300, 450, 600):
    for unit in (100, 150, 200):
        for prem in (0.5, 1.0):
            cells, ratios, full_m = [], [], None
            for st in STRATS:
                m = ev(replace(base, lock_share=1.0, lock_first_cost=first, lock_base=unit, lock_premium=prem, lock_strategy=st))["max"]
                c = m["조립코어30"] / max(m["조립SR이상30"], 1e-9)
                cells.append(f"{c:>6,.0f} ({c / NORMAL:.2f})")
                ratios.append(c / NORMAL)
                if st == "full":
                    full_m = m
            print(f"{first:>4} / {unit:>4} / {prem:<4}        | " + " | ".join(f"{c:^14}" for c in cells)
                  + f" | {min(ratios):>11.2f} | {full_m['전투력30']:.0f} / {full_m['전Lv5중앙일']:.0f}")
