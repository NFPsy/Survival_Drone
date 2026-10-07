"""
Survival_Drone 경제 시뮬레이터 (몬테카를로 시뮬레이션)

【이게 뭐예요?】
  "플레이어가 30일 동안 매일 접속해서 코어·크레딧을 모으고, 뽑기를 하고, 드론을 강화하면
   결국 SSR을 몇 개 얻고, 드론을 얼마나 빨리 다 키울까?"를 컴퓨터로 수만 번 가상 플레이해서 평균을 내 보는 도구입니다.
  (몬테카를로 = 확률이 들어간 일을 수천~수만 번 반복해서 평균 결과를 보는 방법)

【실제 게임과 같은 규칙】
  뽑기(GachaSystem.cs), 보유·승급·조각·강화·조각 교환(DroneInventory.cs)의 규칙을 그대로 옮겼습니다.
  뽑기 로직은 노션 기록(SSR 평균 33.2회, 꽝 10연 18.6% → 0%)과 일치하는 것을 validate.py로 확인했습니다.

【조심할 점 2가지】
  1) 아래 "실제 규칙" 숫자들은 에셋(GachaTable, DroneGrowthTable, CurrencyTable)에서 손으로 옮겨 적은 것입니다.
     게임 쪽 값을 바꾸면 이 파일의 숫자도 같이 고쳐야 합니다. (자동으로 읽어 오지 않습니다)
  2) 플레이어가 "하루에 몇 판 하는지, 얼마나 자주 접속하는지" 같은 행동은 실제 데이터가 아니라 '가정'입니다.
     PROFILES에 모아 두었으니, CBT 로그로 실제 값을 알게 되면 거기를 고치세요.

이 파일은 유니티 프로젝트에 영향을 주지 않는 독립 도구입니다. 실행: python econ_sim.py [시뮬레이션 인원수]
"""
import random
import statistics
import sys
from dataclasses import dataclass, replace  # noqa: F401  (replace는 sweep.py에서 쓰려고 함께 내보냄)

# =====================================================================
# 1. 실제 게임 규칙 (에셋 값을 옮겨 적은 것)
# =====================================================================
RATES = [55.0, 30.0, 12.5, 2.5]            # 뽑기 확률(%): N, R, SR, SSR 순서 (GachaTable)
N_, R_, SR_, SSR_ = 0, 1, 2, 3             # 등급을 숫자로 부르는 이름 (큰 숫자일수록 높은 등급)
SINGLE_COST, TEN_COST = 300, 2700          # 1회 뽑기 / 10연 뽑기 코어 비용
DUP_SHARDS = [10, 20, 30, 50]              # 이미 가진 드론을 또 뽑았을 때 받는 조각 (N, R, SR, SSR)
GRADE_MULT = [1.0, 1.1, 1.25, 1.45]        # 등급별 전투력 배율 (DroneGrowthTable)
BASE_POWER, LEVEL_BONUS = 50, 0.06         # 드론 1개 기본 전투력 / 레벨 1 오를 때마다 +6%
UP_SHARDS = [30, 50, 80, 120]              # 강화에 드는 조각: Lv1→2, 2→3, 3→4, 4→5
UP_CREDITS = [300, 500, 800, 1200]         # 강화에 드는 크레딧 (같은 순서)
MAX_LEVEL = 5
EX_SHARDS, EX_CREDIT, EX_LIMIT = 10, 500, 3   # 조각 교환: 크레딧 500 → 조각 10개, 하루 3번까지
START_CORE, START_CREDIT = 2700, 0         # 처음 시작할 때 받는 코어 / 크레딧
CLEAR_CREDIT = [300, 600, 900]             # 판 클리어 크레딧 (스테이지 1 / 2 / 3, CurrencyTable)
# 스테이지마다 한 번씩 받는 마일스톤 코어의 합계 (3분 + 6분 + 처음 클리어)
MILESTONE_CORES = [100 + 150 + 200, 200 + 300 + 400, 300 + 450 + 600]   # = 450 / 900 / 1350
NUM_DRONES = 5                             # 드론 종류 수 (근접·저격·수집·폭발·회복)


@dataclass
class Rules:
    """바꿔 보고 싶은 규칙 값 묶음. 기본값 = 지금 게임에 적용된 값. sweep.py에서 일부만 바꿔서 비교합니다."""
    pity: int = 70                  # 대천장: 이 횟수에 SSR 확정
    soft_pity: int = 10             # 소천장: SR 이상이 안 나온 채 이 횟수째 뽑기는 SR 이상 확정 (0이면 끔)
    daily_attend: int = 150         # 일일 퀘스트 코어: 출석하기
    daily_3: int = 50               #                  3분 버티기
    daily_6: int = 75               #                  6분 버티기
    daily_clear: int = 125          #                  클리어하기
    exchange: bool = True           # 조각 교환 기능을 쓰는지
    ex_limit: int = EX_LIMIT        # 하루 교환 한도
    ex_credit: int = EX_CREDIT      # 1회 교환에 드는 크레딧
    ex_shards: int = EX_SHARDS      # 1회 교환으로 받는 조각
    clear_credit_mult: float = 1.0  # 클리어 크레딧 배율 (0.5면 절반)


@dataclass
class Profile:
    """플레이어 유형(행동 가정). 실제 데이터가 아니라 가정입니다."""
    name: str
    p_login: float        # 하루에 접속할 확률 (1.0 = 매일)
    matches: int          # 접속한 날 하는 판 수
    clear_rate: float     # 한 판을 클리어할 확률
    p_6min: float         # "6분 버티기" 퀘스트를 달성하는 날의 비율
    s1_day: int           # 스테이지 1 / 2 / 3을 끝내고 마일스톤 코어를 다 받는 날 (99 = 한 달 안에 못 함)
    s2_day: int
    s3_day: int
    fail_credit_frac: float = 0.5   # 실패한 판은 클리어 보상의 이 비율만 받는다고 가정


PROFILES = {
    "max(퀘스트 매일 전부)": Profile("max", 1.0, 4, 0.8, 1.0, 1, 3, 7),
    "mid": Profile("mid", 0.8, 3, 0.7, 0.6, 2, 6, 14),
    "casual": Profile("casual", 0.5, 2, 0.5, 0.3, 4, 15, 99),
}


def roll_rarity(rng):
    """확률표(RATES)로 등급 하나를 굴린다. GachaSystem.RollRarity와 같은 방식(누적 확률)."""
    x = rng.random() * 100.0
    c = 0.0
    for i, r in enumerate(RATES):
        c += r
        if x < c:
            return i
    return SSR_   # 계산 오차 대비 안전장치


# =====================================================================
# 2. 가상 플레이어 한 명
# =====================================================================
class Player:
    def __init__(self, rules: Rules, rng):
        self.rules, self.rng = rules, rng
        self.core, self.credit = START_CORE, START_CREDIT
        self.pity = 0           # 대천장 카운트 (SSR이 나오면 0으로)
        self.soft = 0           # 소천장 카운트 (SR 이상이 나오면 0으로)
        # 보유 드론: 종류 번호 -> [등급, 레벨, 조각]. 처음에는 근접(0)·저격(1) N등급을 가지고 시작한다.
        self.owned = {0: [N_, 1, 0], 1: [N_, 1, 0]}
        # 기록용 숫자들
        self.pulls = 0
        self.ssr = 0
        self.upgrades = 0
        self.exchanges = 0
        self.soft_triggers = 0
        self.pity_hits = 0
        self.first_ssr_day = None
        self.shards_dup = 0     # 중복 뽑기로 얻은 조각 합계
        self.shards_ex = 0      # 조각 교환으로 얻은 조각 합계

    # ---- 뽑기 1회: GachaSystem.PullSingle과 같은 순서로 처리 ----
    def pull_single(self):
        r = self.rules
        self.pity += 1
        self.soft += 1
        big = self.pity >= r.pity                                              # 대천장 도달?
        small = (not big) and r.soft_pity > 0 and self.soft >= r.soft_pity     # 소천장 도달?
        if big:
            rar = SSR_
            self.pity_hits += 1
        elif small:
            # 소천장: SR 이상 확정. SSR 기본 확률로 한 번 굴려서 맞으면 SSR, 아니면 SR
            self.soft_triggers += 1
            rar = SSR_ if self.rng.random() * 100.0 < RATES[SSR_] else SR_
        else:
            rar = roll_rarity(self.rng)
        if rar == SSR_:
            self.pity = 0
        if rar >= SR_:
            self.soft = 0
        drone = self.rng.randrange(NUM_DRONES)   # 드론 종류는 5종 중 균등 확률
        self.pulls += 1
        if rar == SSR_:
            self.ssr += 1
        self._apply(drone, rar)
        return rar

    # ---- 뽑은 결과를 보유 목록에 반영: DroneInventory.Apply와 같은 규칙 ----
    def _apply(self, drone, rar):
        o = self.owned.get(drone)
        if o is None:
            self.owned[drone] = [rar, 1, 0]        # 처음 얻는 드론 → 새로 추가
        elif rar > o[0]:
            o[0] = rar                             # 더 높은 등급 → 승급 (레벨·조각은 그대로)
        else:
            o[2] += DUP_SHARDS[rar]                # 같거나 낮은 등급 → 그 등급만큼 조각
            self.shards_dup += DUP_SHARDS[rar]

    def power(self, d):
        """드론 1개의 전투력 = 기본 × 등급 배율 × 레벨 배율."""
        o = self.owned[d]
        return BASE_POWER * GRADE_MULT[o[0]] * (1 + LEVEL_BONUS * (o[1] - 1))

    def combat_power(self):
        """출격 슬롯이 2개라서, 가장 센 2기를 장착했다고 보고 합산한다."""
        ps = sorted((self.power(d) for d in self.owned), reverse=True)[:2]
        return round(sum(ps))

    # ---- 강화: 조각과 크레딧이 둘 다 충분할 때만, 센 드론부터 ----
    def try_upgrades(self):
        while True:
            best = None
            for d, o in self.owned.items():
                if o[1] >= MAX_LEVEL:
                    continue
                sc, cc = UP_SHARDS[o[1] - 1], UP_CREDITS[o[1] - 1]
                if o[2] >= sc and self.credit >= cc:
                    if best is None or self.power(d) > self.power(best):
                        best = d
            if best is None:
                return
            o = self.owned[best]
            self.owned[best][2] -= UP_SHARDS[o[1] - 1]
            self.credit -= UP_CREDITS[o[1] - 1]
            o[1] += 1
            self.upgrades += 1

    # ---- 조각 교환: 하루 한도까지. 다음 강화까지 조각이 가장 조금 모자란 드론에 산다 ----
    def try_exchanges(self):
        if not self.rules.exchange:
            return
        done = 0
        while done < self.rules.ex_limit and self.credit >= self.rules.ex_credit:
            target, deficit = None, None
            for d, o in self.owned.items():
                if o[1] >= MAX_LEVEL:
                    continue
                need = UP_SHARDS[o[1] - 1] - o[2]
                if need <= 0:
                    continue          # 조각은 이미 충분하고 크레딧만 기다리는 드론은 교환하지 않는다
                if target is None or need < deficit or (need == deficit and self.power(d) > self.power(target)):
                    target, deficit = d, need
            if target is None:
                break
            self.credit -= self.rules.ex_credit
            self.owned[target][2] += self.rules.ex_shards
            self.shards_ex += self.rules.ex_shards
            self.exchanges += 1
            done += 1

    # ---- 코어로 뽑기: 10연이 가능하면 10연, 남는 코어로 1회씩 ----
    def spend_cores(self, day):
        before = self.pulls
        while self.core >= TEN_COST:
            self.core -= TEN_COST
            for _ in range(10):
                self._pull_and_log(day)
        while self.core >= SINGLE_COST:
            self.core -= SINGLE_COST
            self._pull_and_log(day)
        return self.pulls - before

    def _pull_and_log(self, day):
        rar = self.pull_single()
        if rar == SSR_ and self.first_ssr_day is None:
            self.first_ssr_day = day


# =====================================================================
# 3. 플레이어 한 명의 N일 시뮬레이션
# =====================================================================
def simulate(profile: Profile, rules: Rules, days=30, seed=None):
    """한 명을 days일 동안 굴린다. 같은 seed면 항상 같은 결과가 나온다.
    반환: (최종 플레이어, 전투력 130 도달일, 전 드론 Lv5 도달일, 30일째 스냅샷)"""
    rng = random.Random(seed)
    p = Player(rules, rng)
    power130_day = None     # 스테이지 2 권장 전투력(130)에 처음 닿은 날
    all5_day = None         # 5종 모두 보유 + 전부 Lv5가 된 날
    snap = None
    stage_done = [False, False, False]
    done_days = [profile.s1_day, profile.s2_day, profile.s3_day]
    for day in range(1, days + 1):
        login = day == 1 or rng.random() < profile.p_login   # 첫날은 반드시 접속
        if login:
            # 지금 몇 번 스테이지에서 크레딧을 버는지 (끝낸 스테이지 수로 결정)
            stage = sum(1 for d in done_days if d < day)
            stage_idx = min(stage, 2)
            # 마일스톤 코어: 정해진 날이 되면 그 스테이지의 합계를 한 번에 받는다
            for i, d in enumerate(done_days):
                if not stage_done[i] and day >= d:
                    stage_done[i] = True
                    p.core += MILESTONE_CORES[i]
            # 일일 퀘스트 코어
            p.core += rules.daily_attend
            p.core += rules.daily_3
            if rng.random() < profile.p_6min:
                p.core += rules.daily_6
            if rng.random() < 1 - (1 - profile.clear_rate) ** profile.matches:   # 하루 중 한 번이라도 클리어
                p.core += rules.daily_clear
            # 판 크레딧 (클리어는 전액, 실패는 fail_credit_frac만큼)
            for _ in range(profile.matches):
                if rng.random() < profile.clear_rate:
                    p.credit += int(CLEAR_CREDIT[stage_idx] * rules.clear_credit_mult)
                else:
                    p.credit += int(CLEAR_CREDIT[stage_idx] * rules.clear_credit_mult * profile.fail_credit_frac)
            # 쓰는 순서: 뽑기 → 강화 → 조각 교환 → 다시 강화
            p.spend_cores(day)
            p.try_upgrades()
            p.try_exchanges()
            p.try_upgrades()
        if power130_day is None and p.combat_power() >= 130:
            power130_day = day
        if all5_day is None and len(p.owned) == NUM_DRONES and all(o[1] == MAX_LEVEL for o in p.owned.values()):
            all5_day = day
        if day == 30:
            snap = dict(ssr=p.ssr, ups=p.upgrades, ex=p.exchanges, soft=p.soft_triggers, pity=p.pity_hits,
                        power=p.combat_power(), shards_dup=p.shards_dup, shards_ex=p.shards_ex,
                        owned=len(p.owned), credit=p.credit, core=p.core, first=p.first_ssr_day)
    return p, power130_day, all5_day, snap


def run(profile, rules, n=5000, days=60, seed0=1):
    """같은 조건으로 n명을 굴려서 평균을 낸다. days=60은 'Lv5 완성일'을 30일 넘어서도 보기 위한 것
    (60일 안에 못 끝낸 사람은 61일로 센다). 'xxx30' 이름의 지표는 모두 30일째 시점 값이다."""
    rows = []
    for i in range(n):
        p, d130, d5, snap = simulate(profile, rules, days, seed=seed0 * 1000003 + i)
        rows.append((snap, d130, d5, p.pity_hits))
    m = statistics.mean
    all5 = [r[2] if r[2] else days + 1 for r in rows]
    return {
        "SSR30": m(r[0]["ssr"] for r in rows),                              # 30일 동안 얻은 SSR 평균 개수
        "SSR≥1(30d)": m(1 if r[0]["ssr"] >= 1 else 0 for r in rows),        # 30일 안에 SSR을 1개 이상 얻은 비율
        "강화30": m(r[0]["ups"] for r in rows),                              # 30일 동안 강화한 횟수 (최대 20 = 5종×4)
        "교환30": m(r[0]["ex"] for r in rows),
        "전투력30": m(r[0]["power"] for r in rows),
        "130도달일": statistics.median(r[1] if r[1] else days + 1 for r in rows),
        "전Lv5중앙일": statistics.median(all5),                              # 전 드론 Lv5 도달일 (중앙값, 61 = 60일 안에 못 함)
        "30일내Lv5완성%": 100 * m(1 if d <= 30 else 0 for d in all5),
        "크레딧30": m(r[0]["credit"] for r in rows),                         # 30일째에 남은 크레딧
        "소천장30": m(r[0]["soft"] for r in rows),
        "조각(중복)30": m(r[0]["shards_dup"] for r in rows),
        "조각(교환)30": m(r[0]["shards_ex"] for r in rows),
    }


def fmt(d):
    return " | ".join(f"{k} {v:.2f}" if isinstance(v, float) else f"{k} {v}" for k, v in d.items())


if __name__ == "__main__":
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 5000
    print("== 기준(현재 규칙) 30일 ==")
    for name, prof in PROFILES.items():
        print(name, "->", fmt(run(prof, Rules(), n)))
