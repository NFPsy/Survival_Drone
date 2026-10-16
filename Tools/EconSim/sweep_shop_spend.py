"""
소과금·고과금 30일 시뮬레이션: 10/16에 정한 새 상점 가격(원)으로 "돈을 쓰면 SSR이 몇 개 나오나"를 계산한다.

【이게 뭐예요?】
  옛 BM 문서의 "30일 시뮬레이션" 표(소과금·고과금)는 1코어 = 10원, 월정액 4,900원이라는 옛 가정으로 만든 것이라,
  새 상점 가격으로 같은 질문을 다시 푸는 도구입니다. sweep_shop.py는 "1일차에 한 번 구매"만 지원해서,
  이 파일은 "N일차에 구매"와 "여러 번 구매"를 지원하도록 흐름을 새로 썼습니다. (sweep_shop.py의 simulate_with_shop과 같은 흐름이고
  구매가 들어가는 날만 다릅니다. 게임 규칙은 econ_sim.py에서 가져옵니다.)

【가정】
  - 구매한 날은 반드시 접속한다. 구매한 코어는 그날 바로 뽑기에 쓰고, 조각은 뽑기 뒤에 가진 드론에 자동으로 들어간다(후한 가정).
  - 월정액은 1일차에 가입한다: 그날 즉시 코어 300 + 접속하는 날마다 코어 100(30일) + 조각 교환 하루 한도 5회.
  - 플레이어 유형(max / mid / casual)은 econ_sim.py의 가정이며 실제 데이터가 아니다. 옛 문서의 무/소/고(8·10·12판, 클리어율 40·50·60%)와 모델이 다르다.

【상품(새 가격, 10/16 확정)】
  입문 1,100원(코어 330) / 코어 중 8,900원(3,000) / 코어 대 18,900원(7,800) / 코어 특대 29,900원(14,000)
  스타터 팩 2,900원(코어 1,500 + 크레딧 1,000) / 조각 소 1,100원(100) · 중 2,900원(300) · 대 4,900원(700) / 월정액 5,900원

실행: python sweep_shop_spend.py [인원수]      (한글이 깨지면: PYTHONIOENCODING=utf-8 python sweep_shop_spend.py)
"""
import random
import statistics
import sys

import econ_sim as E
from sweep_extend import VARIANTS, PlayerUR, ceiling, patched
from sweep_shop import add_shards

# ---- 상품 (이름: (가격 원, 코어, 크레딧, 조각)) ----
PRODUCTS = {
    "입문":      (1100, 330, 0, 0),
    "코어 중":   (8900, 3000, 0, 0),
    "코어 대":   (18900, 7800, 0, 0),
    "코어 특대": (29900, 14000, 0, 0),
    "스타터":    (2900, 1500, 1000, 0),
    "조각 소":   (1100, 0, 0, 100),
    "조각 중":   (2900, 0, 0, 300),
    "조각 대":   (4900, 0, 0, 700),
}
PASS_PRICE = 5900      # 월정액
PASS_IMMEDIATE = 300   # 월정액 가입 즉시 코어
PASS_DAILY = 100       # 월정액 하루 코어 (접속하는 날, 30일)


def make_plan(items_by_day):
    """{일차: [상품 이름, ...]}를 {일차: {core, credit, shard}}와 총 지출로 바꾼다."""
    plan, won = {}, 0
    for day, names in items_by_day.items():
        c = cr = sh = 0
        for n in names:
            price, core, credit, shard = PRODUCTS[n]
            won += price
            c += core
            cr += credit
            sh += shard
        plan[day] = {"core": c, "credit": cr, "shard": sh}
    return plan, won


def simulate(profile, rules, plan, with_pass, days=60, seed=None, player_cls=E.Player):
    rng = random.Random(seed)
    p = player_cls(rules, rng)
    stage_done = [False] * E.NUM_STAGES
    done_days = list(profile.stage_days)
    all_day, snap30 = None, None
    for day in range(1, days + 1):
        buy = plan.get(day)
        login = day == 1 or buy is not None or rng.random() < profile.p_login
        if with_pass and day == 1:
            p.gain_core(PASS_IMMEDIATE)
        if buy:
            p.gain_core(buy["core"])
            p.credit += buy["credit"]
        if login:
            if with_pass and day <= 30:
                p.gain_core(PASS_DAILY)
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
            if buy and buy["shard"]:
                add_shards(p, buy["shard"])      # 뽑기 뒤에 넣어야 "가진 드론"이 정해진 상태에서 들어간다
            p.try_upgrades()
            p.try_exchanges()
            p.try_upgrades()
        if all_day is None and len(p.owned) == E.NUM_DRONES and all(o[1] == E.MAX_LEVEL for o in p.owned.values()):
            all_day = day       # 5종이 전부 최대 레벨(각성이 켜져 있으면 Lv8)이 된 첫 날
        if day == 30:
            snap30 = dict(ssr=p.ssr, pulls=p.pulls, ups=p.upgrades, power=p.combat_power(), ur30=getattr(p, 'ur', 0))
    snap60 = dict(ssr=p.ssr, pulls=p.pulls, ups=p.upgrades, power=p.combat_power())
    return dict(snap30, power60=snap60["power"], ups60=snap60["ups"], ur60=getattr(p, 'ur', 0), all_day=all_day if all_day else days + 1)


def run(profile, plan, with_pass, n, player_cls=E.Player):
    rules = E.Rules(ex_limit=5) if with_pass else E.Rules()
    rows = [simulate(profile, rules, plan, with_pass, seed=2000003 + i, player_cls=player_cls) for i in range(n)]
    m = statistics.mean
    out = {k: m(r[k] for r in rows) for k in ("ssr", "pulls", "ups", "power", "power60", "ups60", "ur30", "ur60")}
    out["all_med"] = statistics.median(r["all_day"] for r in rows)         # 61 = 60일 안에 못 끝냄
    out["all_30pct"] = 100 * m(1 if r["all_day"] <= 30 else 0 for r in rows)
    return out


# 시나리오: (이름, 월정액 가입, {일차: [상품...]}). 옛 BM 문서의 소과금·고과금과 지출 총액이 비슷하게 맞췄다.
SCENARIOS = [
    ("무과금", False, {}),
    # 옛: 월정액 4,900 + 10일차 11,000 = 15,900원 → 새: 월정액 5,900 + 10일차 코어 중 + 입문(10,000) = 15,900원
    ("소과금 (월정액 + 10일차 코어 중·입문)", True, {10: ["코어 중", "입문"]}),
    # 옛: 월정액 4,900 + 1일차·15일차 66,000 x 2 = 136,900원 → 새: 월정액 + 1일차(스타터·특대 2·조각 대) + 15일차(특대 2·조각 대) = 138,200원
    ("고과금 (월정액 + 1·15일차 특대2+조각 대)", True, {1: ["스타터", "코어 특대", "코어 특대", "조각 대"],
                                                         15: ["코어 특대", "코어 특대", "조각 대"]}),
    # 참고: 코어 팩만 쓰는 중간 지출
    ("중과금 (월정액 + 1일차 코어 대·조각 중)", True, {1: ["코어 대", "조각 중"]}),
]

def report(n, label, patch):
    print(f"\n######## {label} ########")
    with patched(patch):
        for pname, prof in E.PROFILES.items():
            print(f"\n== 유형 {pname} (n={n}) ==")
            base = None
            for name, with_pass, items in SCENARIOS:
                plan, won = make_plan(items)
                if with_pass:
                    won += PASS_PRICE
                r = run(prof, plan, with_pass, n)
                if base is None:
                    base = r
                extra = r["ssr"] - base["ssr"]
                per_total = f"{won / r['ssr']:,.0f}" if won and r["ssr"] else "-"
                per_extra = f"{won / extra:,.0f}" if won and extra > 0.005 else "-"
                done = f"{r['all_med']:.0f}일" if r["all_med"] <= 60 else "60일 안에 못 끝냄"
                print(f"{name:<34} 지출 {won:>7,}원 | SSR {r['ssr']:.2f} ({extra:+.2f}) | 뽑기 {r['pulls']:.0f}회"
                      f" | 전투력 30일 {r['power']:.0f} / 60일 {r['power60']:.0f} | 강화 30일 {r['ups']:.1f} / 60일 {r['ups60']:.1f}"
                      f" | 전 드론 최대 레벨 {done} (30일 안 {r['all_30pct']:.0f}%)"
                      f" | SSR 1개당 지출 {per_total}원 | 추가 SSR 1개당 {per_extra}원")


def report_ur(n):
    """각성(B)만 있을 때와 각성 + UR(B+C)일 때를 같은 지출로 비교한다. UR 규칙은 sweep_extend.py의 가정(확률 0.5%, 전용 천장 150, 배율 1.8, 중복 조각 100)."""
    for pname, prof in E.PROFILES.items():
        print(f"\n== 유형 {pname} (n={n}) ==")
        for label, values, cls in (("B 각성만", VARIANTS["B 각성(Lv8)"], E.Player), ("B + C(UR)", VARIANTS["B+C"], PlayerUR)):
            with patched(values):
                print(f"  [{label}] 이론 상한 전투력 {ceiling()}")
                for name, with_pass, items in SCENARIOS:
                    plan, won = make_plan(items)
                    if with_pass:
                        won += PASS_PRICE
                    r = run(prof, plan, with_pass, n, cls)
                    print(f"    {name:<34} 지출 {won:>7,}원 | 전투력 30일 {r['power']:.0f} / 60일 {r['power60']:.0f} | SSR {r['ssr']:.2f} | UR 30일 {r['ur30']:.2f} / 60일 {r['ur60']:.2f}"
                          f" | 강화 60일 {r['ups60']:.1f}")


if __name__ == "__main__":
    n = int(sys.argv[2]) if len(sys.argv) > 2 else 3000
    if len(sys.argv) > 1 and sys.argv[1] == "ur":
        report_ur(n)
    else:
        n = int(sys.argv[1]) if len(sys.argv) > 1 else n
        report(n, "각성 꺼짐 (지금 게임 규칙: 드론 Lv5 상한)", {})
        report(n, "각성 켜짐 (OBT 예정: Lv8 상한)", VARIANTS["B 각성(Lv8)"])
