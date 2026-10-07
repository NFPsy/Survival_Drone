"""
규칙 값을 바꿔 가며 결과를 비교하는 스크립트 (econ_sim.py가 같은 폴더에 있어야 합니다).

실행: python sweep.py

  1) 규칙 비교: 소천장, 일일 퀘스트 총량, 조각 교환 유무, 클리어 크레딧 배율을 바꿨을 때
     30일 SSR 평균과 전 드론 Lv5 도달일이 어떻게 달라지는지. 목표(30일 SSR 0.5~1.5)를 지키는지 OK/X로 표시.
  2) 조각 교환 비교: 하루 교환 한도 × 1회 크레딧을 바꿨을 때 진행 속도 비교.

값만 바꿔 보는 도구라서 게임 파일은 건드리지 않습니다. 시나리오를 늘리려면 아래 scen에 한 줄 추가하세요.
"""
from dataclasses import replace

from econ_sim import PROFILES, Rules, run

N = 4000            # 한 조건당 가상 플레이어 수 (많을수록 정확하지만 느림)
base = Rules()      # 지금 게임에 적용된 값


def goal_ok(res):
    """BM 목표: 30일 기대 SSR 0.5~1.5. 최대로 하는 사람(max)은 상한을, 가볍게 하는 사람(casual)은 하한을 본다."""
    return res["max"]["SSR30"] <= 1.5 and res["casual"]["SSR30"] >= 0.5


def compare_rules():
    scen = {
        "현재 (소천장10·일일400·교환O)": base,
        "소천장 끔": replace(base, soft_pity=0),
        "소천장 15": replace(base, soft_pity=15),
        "소천장 20": replace(base, soft_pity=20),
        "일일 300 (출석50)": replace(base, daily_attend=50),
        "일일 500 (출석250)": replace(base, daily_attend=250),
        "조각교환 끔": replace(base, exchange=False),
        "클리어크레딧 ×0.5": replace(base, clear_credit_mult=0.5),
        "일일300+소천장끔": replace(base, daily_attend=50, soft_pity=0),
        "일일500+소천장15": replace(base, daily_attend=250, soft_pity=15),
    }
    hdr = f"{'시나리오':<24}|{'SSR30 max/mid/cas':^22}|{'Lv5 완성일(중앙) max/mid/cas':^30}|{'강화30 max/mid/cas':^22}|목표"
    print("== 규칙 비교 (61 = 60일 안에 못 끝냄) ==")
    print(hdr)
    print("-" * len(hdr))
    for name, rules in scen.items():
        res = {prof.name: run(prof, rules, N) for prof in PROFILES.values()}
        a, b, c = res["max"], res["mid"], res["casual"]
        print(f"{name:<24}| {a['SSR30']:.2f} / {b['SSR30']:.2f} / {c['SSR30']:.2f} "
              f"| {a['전Lv5중앙일']:.0f} / {b['전Lv5중앙일']:.0f} / {c['전Lv5중앙일']:.0f}".ljust(80)
              + f"| {a['강화30']:.1f} / {b['강화30']:.1f} / {c['강화30']:.1f} | {'OK' if goal_ok(res) else 'X'}")


def compare_exchange():
    print("\n== 조각 출처 분해 (현재 규칙, 30일 평균) ==")
    for prof in PROFILES.values():
        r = run(prof, base, N)
        tot = r["조각(중복)30"] + r["조각(교환)30"]
        print(f"{prof.name:<7} 중복 {r['조각(중복)30']:.0f} + 교환 {r['조각(교환)30']:.0f} = {tot:.0f}"
              f"  (교환 비중 {100 * r['조각(교환)30'] / tot:.0f}%)  크레딧잔액 {r['크레딧30']:.0f}")

    print("\n== 교환 하루 한도 × 1회 크레딧 ==")
    print(f"{'한도':>4} {'가격':>5} | {'max Lv5일':>9} {'mid Lv5일':>9} {'cas Lv5일':>9} | {'mid 강화30':>9} {'cas 강화30':>9} | {'mid 크레딧잔액':>12}")
    for lim in (0, 1, 2, 3):
        for price in (500, 800, 1200):
            if lim == 0 and price != 500:
                continue
            rules = replace(base, ex_limit=lim, ex_credit=price, exchange=(lim > 0))
            res = {p.name: run(p, rules, N) for p in PROFILES.values()}
            a, b, c = res["max"], res["mid"], res["casual"]
            print(f"{lim:>4} {price:>5} | {a['전Lv5중앙일']:>9.0f} {b['전Lv5중앙일']:>9.0f} {c['전Lv5중앙일']:>9.0f}"
                  f" | {b['강화30']:>9.1f} {c['강화30']:>9.1f} | {b['크레딧30']:>12.0f}")


if __name__ == "__main__":
    compare_rules()
    compare_exchange()
