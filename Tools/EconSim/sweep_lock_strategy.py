"""
잠금 리롤(조립 뽑기)을 "가장 유리하게 쓰는 플레이어"까지 포함해서 검증하는 스크립트 (econ_sim.py가 같은 폴더에 있어야 합니다).

실행: python sweep_lock_strategy.py

【왜 만들었나요?】
  sweep_lock.py는 "SR 이상을 항상 잠그고 끝까지 재뽑기하는 사람"만 가정했습니다. 그런데 칸당 단가(100)가 기본 뽑기(300)보다 싸서
  다른 방법이 더 유리할 수 있다는 것을 10/8 설계 논의에서 발견했습니다. 그래서 플레이어 전략을 3가지로 나눠 모두 돌립니다.
    full = SR 이상을 잠그며 끝까지 채움 (기존 가정)
    once = 첫 공개만 하고 잠글 수 있는 칸만 받고 끝냄 (300코어씩 반복)
    ssr  = SSR만 잠그고 코어가 떨어질 때까지 계속 재뽑기

【수령 규칙(10/8 설계 초안)】 잠근 칸만 가져가고, 안 가져간 칸은 조각으로 환산(기본 뽑기 중복 조각의 conv 비율).
  "옛 규칙"은 안 잠근 칸도 전부 받는 방식이고, 비교하려고 한 줄 넣었습니다.

【읽는 법】 BM 목표 = 30일 기대 SSR 0.5 ~ 1.5. 이 스크립트에서 중요한 건 "가장 유리한 전략에서도 max(퀘스트 매일 전부)가 1.5 이하인가"입니다.
  lock_share는 "코어 중 조립 뽑기에 쓰는 비율"(1.0 = 전부 = 상한).
"""
from dataclasses import replace

from econ_sim import PROFILES, Rules, run

N = 2000
CONV = 0.5
base = Rules(lock_keep_unlocked=False, lock_conv=CONV)

RATES = {
    "SSR 2.5% (문서 그대로)": None,
    "SSR 1.25% (절반)": (55.0, 30.0, 13.75, 1.25),
    "SSR 0% (SR까지)": (55.0, 30.0, 15.0, 0.0),
}
STRATEGIES = {"full": "끝까지 채우기", "once": "첫 공개만", "ssr": "SSR만 잠그기"}


def evaluate(rules):
    return {prof.name: run(prof, rules, N) for prof in PROFILES.values()}


def line(name, res):
    a, b, c = res["max"], res["mid"], res["casual"]
    ok = "OK" if a["SSR30"] <= 1.5 else "초과"
    return (f"{name:<36}| {a['SSR30']:.2f} / {b['SSR30']:.2f} / {c['SSR30']:.2f}   | {a['조립SSR30']:>5.2f} {a['조립횟수30']:>6.1f} "
            f"{a['조각(환산)30']:>7.0f} | {a['전투력30']:>5.0f} {a['강화30']:>5.1f} | {ok}")


HDR = f"{'후보 / 전략':<36}| {'SSR30 max/mid/cas':^20}| {'조립SSR 횟수 환산조각':>21} | {'전투력 강화':>11} | max≤1.5"

cur = evaluate(Rules())
a, b, c = cur["max"], cur["mid"], cur["casual"]
print(f"현재(조립 뽑기 없음): SSR30 {a['SSR30']:.2f} / {b['SSR30']:.2f} / {c['SSR30']:.2f}, 전투력30 {a['전투력30']:.0f}, 강화30 {a['강화30']:.1f}\n")

for share in (0.5, 1.0):
    print(f"== 코어의 {int(share * 100)}%를 조립 뽑기에 사용 (잠근 칸만 수령, 환산 {int(CONV * 100)}%) ==")
    print(HDR)
    print("-" * len(HDR))
    for rname, rates in RATES.items():
        for skey, sname in STRATEGIES.items():
            if skey == "ssr" and rates is not None and rates[3] == 0.0:
                continue                      # SSR이 없는데 SSR만 잠그는 전략은 의미 없음
            res = evaluate(replace(base, lock_share=share, lock_rates=rates, lock_strategy=skey))
            print(line(f"{rname} · {sname}", res))
        print()

print("== 옛 규칙과 비교 (안 잠근 칸도 전부 받음, 코어 100% 사용, SSR 2.5%) — 구멍이 얼마나 컸나 ==")
print(HDR)
print("-" * len(HDR))
for skey, sname in STRATEGIES.items():
    res = evaluate(replace(Rules(), lock_share=1.0, lock_strategy=skey, lock_keep_unlocked=True))
    print(line(f"옛 규칙 · {sname}", res))
print()

print("== 환산 비율 영향 (SSR 1.25%, 코어 100% 사용, 잠근 칸만 수령) ==")
print(HDR)
print("-" * len(HDR))
for conv in (0.0, 0.5, 1.0):
    for skey in ("full", "once"):
        res = evaluate(replace(base, lock_share=1.0, lock_rates=RATES["SSR 1.25% (절반)"], lock_strategy=skey, lock_conv=conv))
        print(line(f"환산 {int(conv * 100)}% · {STRATEGIES[skey]}", res))
