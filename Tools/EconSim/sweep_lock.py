"""
잠금 리롤(조립 뽑기) 규칙 후보를 비교하는 스크립트 (econ_sim.py가 같은 폴더에 있어야 합니다).

실행: python sweep_lock.py

【무엇을 비교하나요?】
  조립 뽑기는 아직 게임에 없는 "설계 단계" 기능입니다. 이 스크립트는 "이 규칙으로 넣으면 30일 동안 SSR이 몇 개 나오는지"를
  규칙 후보별로 미리 계산해서, 구현하기 전에 BM 목표(무과금 30일 기대 SSR 0.5 ~ 1.5)를 지키는 후보를 고르는 용도입니다.

【"코어를 얼마나 조립 뽑기에 쓰는지"는 플레이어마다 다르니 두 가지로 봅니다】
  lock_share 0.5 = 들어오는 코어의 절반을 조립 뽑기에 씀 (현실적인 중간)
  lock_share 1.0 = 코어를 전부 조립 뽑기에 씀 (SSR이 가장 많이 나오는 최악의 경우 = 상한)
  → 상한(1.0)에서도 목표를 지키면 어떤 플레이어든 안전합니다.

【가정】 슬롯의 의미는 "드론 한 칸"(등급 + 드론 종류가 나오는 기본 뽑기 결과와 같은 것)이고, 조립 뽑기에는 천장·소천장을 적용하지 않습니다.
         성공 슬롯(기본 SR 이상)은 나오는 즉시 잠그고, 코어가 모자라면 그 상태로 확정합니다.
"""
from dataclasses import replace

from econ_sim import PROFILES, Rules, run

N = 3000
base = Rules()

# 후보 이름 → (바꾼 규칙). 조립 뽑기 슬롯 확률은 (N, R, SR, SSR %) 순서
VARIANTS = {
    "문서 그대로 (SSR 2.5%·단가100·프리미엄0.5)": {},
    "SSR 절반 (1.25%, SR로 이동)": {"lock_rates": (55.0, 30.0, 13.75, 1.25)},
    "SSR 없음 (SR까지만)": {"lock_rates": (55.0, 30.0, 15.0, 0.0)},
    "단가 ×1.5 (재뽑기 슬롯당 150)": {"lock_base": 150},
    "프리미엄 1.0 (잠글수록 더 비쌈)": {"lock_premium": 1.0},
    "SSR 절반 + 단가 ×1.5": {"lock_rates": (55.0, 30.0, 13.75, 1.25), "lock_base": 150},
}


def goal_ok(res):
    """BM 목표: 30일 기대 SSR 0.5~1.5. max는 상한, casual은 하한을 본다."""
    return res["max"]["SSR30"] <= 1.5 and res["casual"]["SSR30"] >= 0.5


def evaluate(rules):
    return {prof.name: run(prof, rules, N) for prof in PROFILES.values()}


print("== 30일 기대 SSR (max / mid / casual) — 현재는 조립 뽑기 없음 ==")
cur = evaluate(base)
a, b, c = cur["max"], cur["mid"], cur["casual"]
print(f"현재(조립 뽑기 없음) | {a['SSR30']:.2f} / {b['SSR30']:.2f} / {c['SSR30']:.2f} | 전투력30 {a['전투력30']:.0f}/{b['전투력30']:.0f}/{c['전투력30']:.0f}\n")

for share in (0.5, 1.0):
    print(f"== 조립 뽑기에 코어의 {int(share * 100)}% 사용 ==")
    hdr = f"{'후보':<34}| {'SSR30 max/mid/cas':^20}| {'조립 SSR(max)':>11} {'세션(max)':>8}| {'전투력30 max':>11}| 목표"
    print(hdr)
    print("-" * len(hdr))
    for name, over in VARIANTS.items():
        res = evaluate(replace(base, lock_share=share, **over))
        a, b, c = res["max"], res["mid"], res["casual"]
        print(f"{name:<34}| {a['SSR30']:.2f} / {b['SSR30']:.2f} / {c['SSR30']:.2f}   "
              f"| {a['조립SSR30']:>11.2f} {a['조립횟수30']:>8.1f}| {a['전투력30']:>11.0f}| {'OK' if goal_ok(res) else 'X'}")
    print()
