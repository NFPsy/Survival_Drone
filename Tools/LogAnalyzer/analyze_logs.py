"""
테스트 기록(로그) 분석기 — CBT/OBT 테스터가 보낸 로그 파일을 읽어서 표로 정리해 준다. (2026-10-14)

【이게 뭐예요?】
  게임의 로비 "테스트 로그" 창에서 내려받은 텍스트 파일(drone-commander-log-….txt)을 넣으면,
  테스터마다·저장 슬롯마다 "몇 판 했고, 스테이지별로 얼마나 버텼고, 어떤 적에게 죽었고, 코어를 어떻게 썼고,
  프레임은 어땠는지"를 한 번에 정리해 준다. 지금까지 사람이 손으로 하던 분석을 자동으로 하는 도구다.

【사용법】
  python analyze_logs.py 로그1.txt 로그2.txt ...            결과를 화면에 출력
  python analyze_logs.py 로그*.txt --out 보고서.md           결과를 마크다운 파일로 저장
  python analyze_logs.py tester_logs/cbt                         폴더를 주면 그 안의 .txt를 전부 읽는다 (보관 폴더 통째 분석)
  python analyze_logs.py 로그*.txt --matches-csv 판목록.csv   모든 판을 엑셀에서 열 수 있는 CSV로도 저장

  (같은 테스터가 여러 번 내보낸 파일을 한꺼번에 넣어도 된다. 슬롯마다 기록이 겹칠 수 있으니 가장 늦게 내보낸 파일 하나만 넣는 걸 권장.)

【주의할 점】
  1) 게임이 남기는 기록은 슬롯마다 최근 500줄까지다. 오래 한 슬롯은 앞부분이 잘릴 수 있어서, 잘린 슬롯에는 경고를 띄운다.
     (잘려도 슬롯 맨 위의 [요약] 줄은 처음부터의 누적 숫자다. 이 도구는 그 줄도 그대로 보여 준다.)
  2) 권장 전투력 표(RECOMMENDED_POWER)는 게임의 스테이지 값을 손으로 옮겨 적은 것이다. 스테이지 값이 바뀌면 여기도 고쳐야 한다.
  3) 판 수가 적으면(테스터 6명이면 판이 수십 개) 클리어율은 참고용이다. 숫자 하나로 결론 내리지 말고 판 수를 같이 본다.
  4) 파이썬 3.8 이상이면 된다. 따로 설치할 것이 없다(표준 라이브러리만 사용).
"""
import argparse
import csv
import os
import re
import statistics
import sys
from collections import Counter, defaultdict

# ----- 게임 규칙 값 (손으로 옮겨 적은 것: 게임 값이 바뀌면 같이 고친다) -----
# 스테이지 번호 -> 권장 전투력. 1~5는 노멀, 6~10은 하드 1~5(노멀 N의 어려운 버전). 하드 값은 시작값이라 로그로 튜닝한다.
RECOMMENDED_POWER = {1: 100, 2: 115, 3: 130, 4: 150, 5: 170, 6: 120, 7: 135, 8: 155, 9: 185, 10: 205}
SSR_RATE = 0.025            # 일반 뽑기 SSR 확률(천장 제외)
LOG_MAX_ENTRIES = 500       # 게임이 슬롯마다 남기는 최대 기록 줄 수 (PlayLog.MaxEntries)
SOFTWARE_RENDERERS = ("Basic Render Driver", "SwiftShader", "llvmpipe")   # 그래픽 가속이 꺼진 환경의 이름들

LINE_RE = re.compile(r"^(\d\d-\d\d \d\d:\d\d:\d\d) \| (\w+) \| ?(.*)$")
HEADER_RE = re.compile(r"테스터:\s*(\S+)\s+빌드:\s*(\S+)\s+플랫폼:\s*(\S+)\s+내보낸 시각:\s*(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)\s+슬롯 (\d+)개")
SLOT_RE = re.compile(r"^=== 슬롯 (\d+) ===")
TRUNC_RE = re.compile(r"\[기록\] \(최근 (\d+)개")


# ======================================================================
# 1. 한 줄 읽기
# ======================================================================
def parse_kv(rest):
    """'스테이지=1 결과=실패 생존=545.3초 ...' 같은 문장을 {키: 값} 사전으로 바꾼다.
    값에 공백이 들어 있어도(예: 장착=Melee N Lv1 / Sniper N Lv1) 다음 '키=' 앞까지를 값으로 본다."""
    keys = list(re.finditer(r"(?:(?<=\s)|^)([^\s=|]+)=", rest))
    result = {}
    for i, m in enumerate(keys):
        end = keys[i + 1].start() if i + 1 < len(keys) else len(rest)
        result[m.group(1)] = rest[m.end():end].strip()
    return result


def num(text, default=None):
    """글자 속 첫 숫자를 꺼낸다 ('545.3초' -> 545.3, '+100' -> 100). 없으면 default."""
    if text is None:
        return default
    m = re.search(r"-?\d+(?:\.\d+)?", str(text).replace(",", ""))
    if not m:
        return default
    value = float(m.group(0))
    return int(value) if value.is_integer() and "." not in m.group(0) else value


def parse_ratio(text):
    """'4/9' (맞은 횟수/쓴 횟수) -> (4, 9). 모양이 다르면 None."""
    m = re.match(r"\s*(\d+)\s*/\s*(\d+)", text or "")
    return (int(m.group(1)), int(m.group(2))) if m else None


def parse_boss_entry(text):
    """'속도7.9/체력112(100%)/레벨23' -> {'speed': 7.9, 'max_hp': 112, 'hp_pct': 100, 'level': 23}. 모양이 다르면 None."""
    m = re.match(r"속도([\d.]+)/체력(\d+)\((\d+)%\)/레벨(\d+)", text or "")
    if not m:
        return None
    return {"speed": float(m.group(1)), "max_hp": int(m.group(2)), "hp_pct": int(m.group(3)), "level": int(m.group(4))}


def parse_damage(text):
    """'강한126/약한90/빠른66' -> {'강한': 126, ...}"""
    result = {}
    for part in (text or "").split("/"):
        m = re.match(r"(.+?)(\d+(?:\.\d+)?)$", part.strip())
        if m:
            result[m.group(1)] = float(m.group(2))
    return result


def parse_minutes(text):
    """'1분12/4분80/9분210' (분별 받은 피해) -> {1: 12, 4: 80, 9: 210}. 없으면 None."""
    result = {}
    for part in (text or "").split("/"):
        m = re.match(r"(\d+)분(\d+(?:\.\d+)?)$", part.strip())
        if m:
            result[int(m.group(1))] = float(m.group(2))
    return result or None


def parse_contrib(text):
    """'Sniper피해1234/Explosion피해560/Heal회복210' (드론별 기여) -> {'Sniper': 1234, 'Explosion': 560, 'Heal': 210}. 없으면 None."""
    result = {}
    for part in (text or "").split("/"):
        m = re.match(r"([A-Za-z]+)(?:피해|회복)(\d+(?:\.\d+)?)$", part.strip())
        if m:
            result[m.group(1)] = float(m.group(2))
    return result or None


def parse_final_drones(text):
    """'Sniper(5)/Heal(3)' (최종드론) -> {'Sniper': 5, 'Heal': 3}. 없으면 None."""
    result = {}
    for part in (text or "").split("/"):
        m = re.match(r"([A-Za-z]+)\((\d+)\)$", part.strip())
        if m:
            result[m.group(1)] = int(m.group(2))
    return result or None


def parse_order(text):
    """'M,M,N:Sniper,U:Sniper,H' (레벨업 선택 순서) -> ['M', 'M', 'N:Sniper', 'U:Sniper', 'H']. 없으면 None."""
    items = [x.strip() for x in (text or "").split(",") if x.strip()]
    return items or None


def parse_picks(text):
    """'이동속도7/체력0/신규드론2/드론강화9' -> {'이동속도': 7, ...}"""
    result = {}
    for part in (text or "").split("/"):
        m = re.match(r"(.+?)(\d+)$", part.strip())
        if m:
            result[m.group(1)] = int(m.group(2))
    return result


# ======================================================================
# 2. 파일 한 개 읽기
# ======================================================================
class Slot:
    def __init__(self, number, saved):
        self.number = number
        self.saved = saved
        self.summary = []        # 슬롯 맨 위 [요약] 줄들(처음부터의 누적 숫자)
        self.events = []         # (시각, 이벤트 이름, {키: 값})
        self.logged = None       # [기록] (최근 N개)의 N
        self.matches = []        # 끝난 판
        self.abandons = []       # 도중에 나간 판
        self.starts = []         # 시작한 판


class Tester:
    def __init__(self, path):
        self.path = path
        self.tester = "?"
        self.build = "?"
        self.platform = "?"
        self.exported = "?"
        self.slot_count = 0
        self.slots = []
        self.graphics = Counter()    # 그래픽 이름 -> 접속 횟수


def read_file(path):
    t = Tester(path)
    slot = None
    with open(path, encoding="utf-8-sig") as f:
        for raw in f:
            line = raw.rstrip("\r\n")
            h = HEADER_RE.search(line)
            if h:
                t.tester, t.build, t.platform, t.exported, count = h.groups()
                t.slot_count = int(count)
                continue
            s = SLOT_RE.match(line)
            if s:
                slot = Slot(int(s.group(1)), line)
                t.slots.append(slot)
                continue
            if slot is None:
                continue
            tr = TRUNC_RE.search(line)
            if tr:
                slot.logged = int(tr.group(1))
                continue
            if line.startswith("- "):
                slot.summary.append(line[2:])
                continue
            m = LINE_RE.match(line)
            if m:
                when, name, rest = m.groups()
                kv = parse_kv(rest) if name != "screen" else {"화면": rest}
                slot.events.append((when, name, kv))
    for slot in t.slots:
        classify(t, slot)
    return t


def classify(t, slot):
    """이벤트를 판 시작/판 끝/도중 이탈로 나눠 담고, 접속 때 찍힌 그래픽 이름을 센다."""
    for when, name, kv in slot.events:
        if name == "match":
            slot.matches.append(make_match(when, kv))
        elif name == "match_abandon":
            slot.abandons.append({"when": when, "stage": num(kv.get("스테이지")), "how": kv.get("나간방법", "?"),
                                  "survive": num(kv.get("생존")), "power": num(kv.get("내전투력")),
                                  "level": num(kv.get("도달레벨"))})
        elif name == "match_start":
            slot.starts.append({"when": when, "stage": num(kv.get("스테이지")), "power": num(kv.get("내전투력")),
                                "loadout": kv.get("장착", "")})
        elif name == "session_start":
            gpu = kv.get("그래픽")
            if gpu:
                t.graphics[gpu] += 1


def make_match(when, kv):
    stage = num(kv.get("스테이지"))
    power = num(kv.get("내전투력"))
    rec = RECOMMENDED_POWER.get(stage)
    perf = kv.get("성능", "")
    return {
        "when": when, "stage": stage, "cleared": kv.get("결과") == "클리어",
        "survive": num(kv.get("생존")), "real": num(kv.get("실제소요")), "level": num(kv.get("도달레벨")),
        "drones": num(kv.get("드론수")), "power": power, "rec": rec,
        "ratio": (power / rec) if (power is not None and rec) else None,
        "death": kv.get("사망원인"), "damage": parse_damage(kv.get("받은피해")),
        "picks": parse_picks(kv.get("레벨업선택")), "first_speed": num(kv.get("이동속도첫선택")),
        "fps": num(perf), "low_fps_pct": num(kv.get("30fps미만")), "hitches": num(kv.get("100ms넘는끊김")),
        "worst_ms": num(kv.get("최악")), "screen": kv.get("화면"),
        # 보스전(10/17 이후 빌드): "보스전=진입|없음", 진입했을 때만 "보스체력=NN%"(판이 끝났을 때 남은 비율, 처치하면 0), "보스전시간=NN.N초". 옛 기록에는 없다.
        "boss": kv.get("보스전"), "boss_hp": num(kv.get("보스체력")), "boss_seconds": num(kv.get("보스전시간")),
        # 보스전 상세(10/18 이후 빌드): 들어갈 때 내 상태, 분노 시작, 패턴별 맞은 횟수/쓴 횟수, 끝날 때 내 체력 %
        "boss_entry": parse_boss_entry(kv.get("보스입장")), "rage_at": num(kv.get("분노시작")),
        "slam": parse_ratio(kv.get("내려찍기")), "rage_slam": parse_ratio(kv.get("분노내려찍기")),
        "dash": parse_ratio(kv.get("돌진")), "rage_dash": parse_ratio(kv.get("분노돌진")),
        "boss_end_hp": num(kv.get("보스전끝체력")),
        # 시간 구성(10/18 이후): 일시정지 메뉴에 있던 시간, 레벨업 선택 화면에 있던 시간(초), 2초 넘게 멈춘 횟수(탭 전환 등으로 보임)
        "paused": num(kv.get("일시정지")), "select_screen": num(kv.get("선택화면")), "suspended": num(kv.get("2초넘는멈춤")),
        # 마우스 조준(10/18 이후): 감도(%)와 커서 고정 결과. 폭발 드론을 쓰지 않은 판에는 없다.
        "aim_sens": num(kv.get("마우스감도")), "aim_lock": kv.get("조준커서"),
        # 10/18 로그 2·3단계 이후: 1분씩 끊은 받은 피해, 레벨업 선택 순서(M 이동속도·H 체력·N:드론 신규·U:드론 강화), 판이 끝날 때 보유 드론, 드론별 기여
        "minute_damage": parse_minutes(kv.get("분별피해")), "pick_order": parse_order(kv.get("선택순서")),
        "final_drones": parse_final_drones(kv.get("최종드론")), "contrib": parse_contrib(kv.get("드론기여")),
        # 저장 코드를 불러온 슬롯의 판에는 "불러옴=N"이 붙는다(N = 그 슬롯이 불러온 횟수). 코드로 옮겨 온 저장은 값을 고쳤을 수도 있어서 따로 본다.
        "imported": num(kv.get("불러옴"), 0),
    }


# ======================================================================
# 3. 계산
# ======================================================================
def ledger_mismatches(slot):
    """코어 수지 점검: 뽑기·락온·수령 줄의 '남은코어'가 앞 줄 기준으로 계산한 값과 맞는지 본다.
    맞지 않는 줄이 있으면 로그나 게임에 이상이 있다는 신호다."""
    problems = []
    prev = None
    for when, name, kv in slot.events:
        # 저장 코드를 불러와서 저장이 통째로 바뀐 줄(코어=바뀌기 전→바뀐 뒤): 여기서부터는 바뀐 뒤의 코어를 기준으로 다시 센다.
        if name == "save_import":
            m = re.match(r"\s*\d+\s*→\s*(\d+)", kv.get("코어", ""))
            prev = int(m.group(1)) if m else None
            continue
        remaining = num(kv.get("남은코어"))
        if remaining is None:
            continue
        if name in ("pull", "lockon_start", "lockon_reroll"):
            expected = None if prev is None else prev - (num(kv.get("사용코어"), 0))
        elif name == "lockon_confirm":
            expected = prev
        elif name in ("quest_claim", "milestone_claim"):
            expected = None if prev is None else prev + (num(kv.get("코어"), 0))
        else:
            continue
        if expected is not None and expected != remaining:
            problems.append(f"{when} {name}: 계산 {expected} ≠ 기록 {remaining}")
        prev = remaining
    return problems


def pull_stats(slot):
    singles = tens = ssr = sr_plus = 0
    spent = 0
    blocked = 0
    for when, name, kv in slot.events:
        if name == "pull":
            if "10" in kv.get("종류", ""):
                tens += 1
            else:
                singles += 1
            ssr += num(kv.get("SSR"), 0)
            sr_plus += num(kv.get("SR"), 0) + num(kv.get("SSR"), 0)
            spent += num(kv.get("사용코어"), 0)
        elif name == "pull_blocked":
            blocked += 1
    total = singles + tens * 10
    return {"total": total, "singles": singles, "tens": tens, "ssr": ssr, "sr_plus": sr_plus, "spent": spent,
            "blocked": blocked, "p_no_ssr": (1 - SSR_RATE) ** total if total else None}


def lockon_stats(slot):
    starts = rerolls = confirms = 0
    spent = 0
    for when, name, kv in slot.events:
        if name == "lockon_start":
            starts += 1
        elif name == "lockon_reroll":
            rerolls += 1
        elif name == "lockon_confirm":
            confirms += 1
            spent += num(kv.get("총사용코어"), 0)
    return {"starts": starts, "rerolls": rerolls, "confirms": confirms, "spent": spent}


def count_events(slot, name):
    return sum(1 for _, n, _ in slot.events if n == name)


def shop_stats(slot):
    """가상 상점(OBT 2단계) 기록: 상점을 연 횟수, 상품별 클릭 횟수, 안내 창 확인 횟수, 클릭할 때 보유 코어."""
    opens = count_events(slot, "shop_open")
    closes = count_events(slot, "shop_demo_popup")
    clicks = Counter()
    cores = []
    topup = Counter()   # 코어 충전 버튼으로 연 횟수 (경로별)
    for when, name, kv in slot.events:
        if name == "shop_open" and kv.get("경로", "").startswith("코어충전"):
            topup[kv.get("경로")] += 1
        if name == "shop_click":
            clicks[kv.get("상품", "?")] += 1
            core = num(kv.get("보유코어"))
            if core is not None:
                cores.append(core)
    return {"opens": opens, "closes": closes, "clicks": clicks, "cores": cores, "topup": topup}


def pct(a, b):
    return f"{100 * a / b:.0f}%" if b else "-"


def mean(values, digits=1):
    values = [v for v in values if v is not None]
    return f"{statistics.mean(values):.{digits}f}" if values else "-"


# ======================================================================
# 4. 보고서 글 만들기 (마크다운)
# ======================================================================
def table(headers, rows):
    out = ["| " + " | ".join(headers) + " |", "|" + "---|" * len(headers)]
    for r in rows:
        out.append("| " + " | ".join(str(c) for c in r) + " |")
    return out


def stage_table(matches):
    by_stage = defaultdict(list)
    for m in matches:
        by_stage[m["stage"]].append(m)
    rows = []
    for stage in sorted(k for k in by_stage if k is not None):
        ms = by_stage[stage]
        fails = [m for m in ms if not m["cleared"]]
        clears = len(ms) - len(fails)
        rows.append([
            stage, len(ms), clears, pct(clears, len(ms)),
            mean([m["survive"] for m in fails], 0) + "초" if fails else "-",
            f"{min(m['survive'] for m in ms):.0f}~{max(m['survive'] for m in ms):.0f}초",
            mean([m["power"] for m in ms], 0), RECOMMENDED_POWER.get(stage, "?"),
            mean([m["ratio"] * 100 if m["ratio"] is not None else None for m in ms], 0) + "%",
        ])
    return table(["스테이지", "판", "클리어", "클리어율", "실패 평균 생존", "생존 범위", "평균 내 전투력", "권장", "권장 대비"], rows)


def power_bins(matches):
    bins = [("80% 미만", 0, 0.8), ("80~100%", 0.8, 1.0), ("100~120%", 1.0, 1.2), ("120% 이상", 1.2, 99)]
    rows = []
    for label, lo, hi in bins:
        ms = [m for m in matches if m["ratio"] is not None and lo <= m["ratio"] < hi]
        clears = sum(1 for m in ms if m["cleared"])
        rows.append([label, len(ms), clears, pct(clears, len(ms))])
    return table(["내 전투력 / 권장", "판", "클리어", "클리어율"], rows)


def survival_hist(matches):
    edges = [(0, 60), (60, 150), (150, 300), (300, 450), (450, 600), (600, 9999)]
    fails = [m for m in matches if not m["cleared"] and m["survive"] is not None]
    rows = []
    for lo, hi in edges:
        n = sum(1 for m in fails if lo <= m["survive"] < hi)
        label = f"{lo}~{hi}초" if hi < 9999 else f"{lo}초 이상"
        rows.append([label, n])
    return table(["실패한 판의 생존 시간", "판 수"], rows)


def death_table(matches):
    fails = [m for m in matches if not m["cleared"]]
    causes = Counter(m["death"] for m in fails if m["death"])
    total_damage = Counter()
    for m in matches:
        for kind, value in m["damage"].items():
            total_damage[kind] += value
    rows = [[k, v] for k, v in causes.most_common()]
    out = table(["사망원인(마지막으로 맞은 적)", "판 수"], rows) if rows else ["(실패한 판이 없거나 사망원인 기록 없음)"]
    if total_damage:
        out.append("")
        out += table(["받은 피해 합계(적 종류)", "피해"], [[k, f"{v:.0f}"] for k, v in total_damage.most_common()])
    return out


def minute_damage_table(matches):
    """몇 분째에 받은 피해가 큰지. 그 분까지 살아 있던 판을 기준으로 판당 평균을 낸다. 분별 기록이 있는 판이 없으면 None."""
    ms = [m for m in matches if m["minute_damage"] and m["survive"] is not None]
    if not ms:
        return None
    rows = []
    for minute in range(1, 11):
        start = (minute - 1) * 60
        alive = [m for m in ms if m["survive"] >= start]
        if not alive:
            continue
        avg = statistics.mean(m["minute_damage"].get(minute, 0) for m in alive)
        died = sum(1 for m in alive if not m["cleared"] and start <= m["survive"] < start + 60) if minute < 10 else             sum(1 for m in alive if not m["cleared"] and m["survive"] >= start)
        rows.append([f"{minute}분", len(alive), f"{avg:.0f}", died])
    return table(["구간 (10분 = 540초 이후)", "그 구간까지 산 판", "판당 받은 피해", "그 구간에서 죽은 판"], rows)


def drone_contrib_table(matches):
    """드론 종류별로 판당 얼마나 기여했는지(준 피해, 회복량). 판이 끝날 때 그 드론을 가진 판만 센다. 기여 기록이 있는 판이 없으면 None."""
    ms = [m for m in matches if m["contrib"] is not None or m["final_drones"]]
    ms = [m for m in ms if m["final_drones"] is not None and m["survive"]]
    has_contrib = [m for m in ms if m["contrib"] is not None]
    if not has_contrib:
        return None
    rows = []
    types = sorted({d for m in ms for d in m["final_drones"]})
    for kind in types:
        if kind == "Collector":
            continue   # 수집 드론은 피해도 회복도 없다
        owners = [m for m in has_contrib if kind in m["final_drones"]]
        if not owners:
            continue
        amounts = [m["contrib"].get(kind, 0) for m in owners]
        per_sec = [a / m["survive"] for a, m in zip(amounts, owners)]
        levels = [m["final_drones"][kind] for m in owners]
        unit = "회복" if kind == "Heal" else "피해"
        rows.append([f"{kind}({unit})", len(owners), f"{statistics.mean(levels):.1f}", f"{statistics.mean(amounts):.0f}", f"{statistics.mean(per_sec):.1f}"])
    return table(["드론", "가진 판", "평균 레벨", "판당 기여", "초당 기여"], rows)


def pick_order_summary(matches):
    """레벨업 선택 순서. 첫 선택들과 이동속도를 처음 고른 순번. 순서 기록이 있는 판이 없으면 None."""
    ms = [m for m in matches if m["pick_order"]]
    if not ms:
        return None
    first3_speed = sum(1 for m in ms if "M" in m["pick_order"][:3])
    first_idx = [m["pick_order"].index("M") + 1 for m in ms if "M" in m["pick_order"]]
    openings = Counter(" → ".join(m["pick_order"][:4]) for m in ms)
    text = [f"- 판 {len(ms)}개 중 처음 3번 안에 이동속도를 고른 판 {first3_speed}개({pct(first3_speed, len(ms))})"]
    if first_idx:
        text.append(f"- 이동속도를 처음 고른 순번: 평균 {statistics.mean(first_idx):.1f}번째(중앙 {statistics.median(first_idx):.0f}번째)")
    text.append("- 가장 흔한 처음 4번의 선택: " + ", ".join(f"{k}({v}판)" for k, v in openings.most_common(3)))
    return text


def import_summary(testers, matches):
    """저장 코드 사용 현황. 불러온 저장이 섞인 판은 경제·난이도 분석에서 따로 봐야 하므로 판 수와 코어 변화를 보여 준다. 사용 흔적이 없으면 None."""
    imports = []   # (테스터, 슬롯, 시각, 바뀌기 전 코어, 바뀐 뒤 코어)
    exports = 0
    for tester in testers:
        for slot in tester.slots:
            for when, name, kv in slot.events:
                if name == "save_export":
                    exports += 1
                elif name == "save_import":
                    m = re.match(r"\s*(\d+)\s*→\s*(\d+)", kv.get("코어", ""))
                    before, after = (int(m.group(1)), int(m.group(2))) if m else (None, None)
                    imports.append((tester.tester, slot.number, when, before, after))
    imported_matches = [m for m in matches if (m.get("imported") or 0) > 0]
    if not imports and not exports and not imported_matches:
        return None
    lines = [f"- 저장 코드를 복사한 횟수 {exports}번 · 불러온 횟수 {len(imports)}번",
             f"- 불러온 저장이 섞인 판: 전체 {len(matches)}판 중 {len(imported_matches)}판({pct(len(imported_matches), len(matches))}) — 코드는 값을 고쳤을 수도 있으니 클리어율·코어 경제는 이 판을 빼고도 한 번 더 본다"]
    for tester, slot, when, before, after in imports:
        note = ""
        if before is not None and after is not None:
            note = f" 코어 {before:,}→{after:,}"
            if after - before > 3000:
                note += " ⚠️ 코어가 크게 늘어남"
        lines.append(f"- 테스터 {tester} 슬롯 {slot} {when}에 불러옴{note}")
    return lines


def picks_summary(matches):
    ms = [m for m in matches if m["picks"]]
    if not ms:
        return "(레벨업 선택 기록 없음)"
    keys = ["이동속도", "체력", "신규드론", "드론강화"]
    avgs = {k: statistics.mean(m["picks"].get(k, 0) for m in ms) for k in keys}
    firsts = [m["first_speed"] for m in ms if m["first_speed"] is not None]
    text = " · ".join(f"{k} 판당 {avgs[k]:.1f}회" for k in keys)
    if firsts:
        text += f" · 이동속도 첫 선택 평균 {statistics.mean(firsts):.0f}초(중앙 {statistics.median(firsts):.0f}초)"
    return text


def perf_summary(matches):
    ms = [m for m in matches if m["fps"] is not None]
    if not ms:
        return "(성능 기록 없음)"
    hitch = [m for m in ms if (m["hitches"] or 0) > 0]
    text = f"판 {len(ms)}개: 평균 {mean([m['fps'] for m in ms], 0)}fps (최저 {min(m['fps'] for m in ms):.0f}), 30fps 미만 비율 평균 {mean([m['low_fps_pct'] for m in ms], 1)}%"
    if hitch:
        worst = max(hitch, key=lambda m: m["worst_ms"] or 0)
        text += f", 100ms 넘는 끊김이 있던 판 {len(hitch)}개(가장 심한 {worst['worst_ms']:.0f}ms, 스테이지 {worst['stage']})"
    else:
        text += ", 100ms 넘는 끊김 없음"
    stalls = [m for m in ms if (m["suspended"] or 0) > 0]
    if stalls:
        text += f" · 2초 넘게 멈춘 판 {len(stalls)}개(합 {sum(m['suspended'] for m in stalls):.0f}회, 탭 전환 등으로 보여 위 끊김에는 안 섞음)"
    screens = Counter(m["screen"] for m in ms if m["screen"])
    if screens:
        text += " · 화면 " + ", ".join(f"{k}({v})" for k, v in screens.most_common(3))
    return text


def slot_report(t, slot):
    out = [f"#### 슬롯 {slot.number}  ({slot.saved.strip('= ').split('(')[-1].rstrip(')') if '(' in slot.saved else ''})"]
    if slot.logged is not None and slot.logged >= LOG_MAX_ENTRIES:
        out.append(f"> ⚠️ 기록이 {slot.logged}줄이라 앞부분이 잘렸을 수 있다(최대 {LOG_MAX_ENTRIES}줄). 아래 [요약]의 누적 숫자와 비교해서 읽을 것.")
    if slot.summary:
        out.append("")
        out.append("게임이 낸 요약(처음부터의 누적):")
        out += ["- " + s for s in slot.summary]
    out.append("")
    ms = slot.matches
    out.append(f"**판 {len(ms)}개 (기록 안)** · 도중에 나간 판 {len(slot.abandons)}개 · 시작만 하고 끝 기록이 없는 판 "
               f"{max(0, len(slot.starts) - len(ms) - len(slot.abandons))}개(탭을 닫았을 가능성)")
    if ms:
        out.append("")
        rows = [[m["when"], m["stage"], "클리어" if m["cleared"] else "실패", f"{m['survive']:.0f}초" if m["survive"] is not None else "-",
                 m["level"], m["power"], f"{100 * m['ratio']:.0f}%" if m["ratio"] is not None else "-",
                 m["death"] or "-", f"{m['fps']:.0f}" if m["fps"] is not None else "-"] for m in ms]
        out += table(["시각", "스테이지", "결과", "생존", "도달레벨", "내 전투력", "권장 대비", "사망원인", "fps"], rows)
    if slot.abandons:
        out.append("")
        out += table(["이탈 시각", "스테이지", "나간 방법", "생존", "내 전투력"],
                     [[a["when"], a["stage"], a["how"], f"{a['survive']:.0f}초" if a["survive"] is not None else "-", a["power"]] for a in slot.abandons])
    p = pull_stats(slot)
    lk = lockon_stats(slot)
    out.append("")
    line = (f"**뽑기** {p['total']}회(1회 {p['singles']}번, 10연 {p['tens']}번) · SSR {p['ssr']}개 · SR 이상 {p['sr_plus']}개 · 쓴 코어 {p['spent']:,}")
    if p["p_no_ssr"] is not None and p["ssr"] == 0 and p["total"] >= 10:
        line += f" · SSR 0개일 확률 약 {100 * p['p_no_ssr']:.0f}% (운이 특별히 나쁜 건 아님)" if p["p_no_ssr"] > 0.2 else f" · SSR 0개일 확률 약 {100 * p['p_no_ssr']:.0f}% (드문 일)"
    if p["blocked"]:
        line += f" · 코어 부족으로 막힘 {p['blocked']}번"
    out.append(line)
    out.append(f"**락온** 시작 {lk['starts']}번 · 재뽑기 {lk['rerolls']}번 · 확정 {lk['confirms']}번 · 쓴 코어 {lk['spent']:,}")
    claims_m = [kv for _, n, kv in slot.events if n == "milestone_claim"]
    claims_q = [kv for _, n, kv in slot.events if n == "quest_claim"]
    out.append(f"**코어 수령** 마일스톤 {len(claims_m)}번(+{sum(num(c.get('코어'), 0) for c in claims_m):,}) · 일일 퀘스트 {len(claims_q)}번(+{sum(num(c.get('코어'), 0) for c in claims_q):,})")
    claims_h = [kv for _, n, kv in slot.events if n == "hard_milestone_claim"]
    if claims_h:
        out.append(f"**하드 마일스톤** 수령 {len(claims_h)}번(조각 +{sum(num(c.get('조각'), 0) for c in claims_h):,}) — 하드 1~5는 스테이지 6~10으로 기록된다")
    imp_in = count_events(slot, "save_import")
    imp_out = count_events(slot, "save_export")
    if imp_in or imp_out:
        out.append(f"**저장 코드** 복사 {imp_out}번 · 불러옴 {imp_in}번 (불러온 뒤의 판은 코드로 옮겨 온 저장일 수 있다)")
    out.append(f"**성장** 드론 강화 {count_events(slot, 'upgrade')}번 · 조각 교환 {count_events(slot, 'shard_exchange')}번 · 스테이지 해금 {count_events(slot, 'unlock')}번 · 장착 변경 {count_events(slot, 'equip')}번")
    shop = shop_stats(slot)
    if shop["opens"] or shop["clicks"]:
        by_product = ", ".join(f"{k} {v}" for k, v in shop["clicks"].most_common())
        low_core = sum(1 for c in shop["cores"] if c < 300)
        topup_text = ""
        if shop["topup"]:
            topup_text = " (코어 충전 버튼으로 " + ", ".join(f"{k.replace('코어충전-', '')} {v}" for k, v in shop["topup"].most_common()) + ")"
        out.append(f"**상점(데모)** 열람 {shop['opens']}번{topup_text} · 구매 버튼 클릭 {sum(shop['clicks'].values())}번" + (f" ({by_product})" if by_product else "")
                   + f" · 안내 창 확인 {shop['closes']}번" + (f" · 코어가 300 미만일 때 누른 클릭 {low_core}번(코어 부족 직후 관심의 단서)" if shop["cores"] else ""))
    if ms:
        out.append(f"**레벨업 선택** {picks_summary(ms)}")
        out.append(f"**성능** {perf_summary(ms)}")
        time_line = time_summary(ms)
        if time_line:
            out.append(f"**시간** {time_line}")
        aim_line = aim_summary(ms)
        if aim_line:
            out.append(f"**조준** {aim_line}")
        boss_line = boss_summary(ms)
        if boss_line:
            out.append(f"**보스전** {boss_line}")
    bad = ledger_mismatches(slot)
    out.append(f"**코어 수지 점검** " + ("모든 줄 일치 ✅" if not bad else f"⚠️ 어긋난 줄 {len(bad)}개: " + "; ".join(bad[:3])))
    return out


def tester_report(t):
    out = [f"### 테스터 {t.tester} — 빌드 {t.build} · {t.platform} · 내보낸 시각 {t.exported} · 슬롯 {t.slot_count}개",
           f"(파일: {t.path})"]
    soft = [g for g in t.graphics if any(s.lower() in g.lower() for s in SOFTWARE_RENDERERS)]
    gpus = ", ".join(f"{g}×{c}" for g, c in t.graphics.most_common(3)) or "기록 없음"
    out.append(f"그래픽: {gpus}")
    if soft:
        out.append("> ⚠️ 그래픽 가속이 꺼진 환경(소프트웨어 렌더링)이다. FPS 분석에서 따로 볼 것.")
    for slot in t.slots:
        out.append("")
        out += slot_report(t, slot)
    return out


def overview_rows(testers):
    rows = []
    for t in testers:
        ms = [m for s in t.slots for m in s.matches]
        clears = sum(1 for m in ms if m["cleared"])
        p = [pull_stats(s) for s in t.slots]
        rows.append([t.tester, t.build, len(t.slots), len(ms), clears, pct(clears, len(ms)),
                     sum(x["total"] for x in p), sum(x["ssr"] for x in p),
                     mean([m["fps"] for m in ms], 0), sum(len(s.abandons) for s in t.slots)])
    return table(["테스터", "빌드", "슬롯", "끝난 판", "클리어", "클리어율", "뽑기 횟수", "SSR", "평균 fps", "도중 이탈"], rows)


def unique_matches(testers):
    """같은 테스터가 로그를 여러 번 내보내면 같은 판이 여러 파일에 들어 있다. (테스터, 슬롯, 시각, 스테이지)가 같으면 한 판으로 센다."""
    seen = set()
    result = []
    for t in testers:
        for s in t.slots:
            for m in s.matches:
                key = (t.tester, s.number, m["when"], m["stage"])
                if key not in seen:
                    seen.add(key)
                    result.append(m)
    return result


def build_report(testers):
    all_matches = unique_matches(testers)
    out = ["# 테스트 로그 분석 보고서", "",
           f"파일 {len(testers)}개 · 테스터 {len({t.tester for t in testers})}명 · 끝난 판 {len(all_matches)}개(같은 판이 여러 파일에 있으면 한 번만 셈)", ""]
    counts = Counter(t.tester for t in testers)
    dup = [name for name, n in counts.items() if n > 1]
    if dup:
        out.append("> ℹ️ 같은 테스터의 파일이 여러 개다: " + ", ".join(dup) + " — 아래 '한눈에 보기' 표는 파일마다 따로 세지만, 스테이지별·구간별 합계는 겹치는 판을 한 번만 센다.")
        out.append("")
    builds = Counter(t.build for t in testers)
    if len(builds) > 1:
        out.append("> ⚠️ 빌드 버전이 섞여 있다: " + ", ".join(f"{b}({n}파일)" for b, n in builds.items()) + " — 규칙·수치가 달라졌다면 버전별로 따로 볼 것.")
        out.append("")
    out.append("## 한눈에 보기")
    out += overview_rows(testers)
    if all_matches:
        out += ["", "## 스테이지별 (모든 테스터 합계)"] + stage_table(all_matches)
        out += ["", "## 내 전투력 ÷ 권장 전투력별 클리어율 (하드 스테이지 배율을 보정할 핵심 지표)"] + power_bins(all_matches)
        out += ["", "## 실패한 판의 생존 시간 분포 (90~150초에 몰리는지 확인)"] + survival_hist(all_matches)
        out += ["", "## 죽은 원인과 받은 피해"] + death_table(all_matches)
        minute_rows = minute_damage_table(all_matches)
        if minute_rows:
            out += ["", "## 구간별 받은 피해 (몇 분에 어려워지는지, 10/18 이후 빌드 기록만)"] + minute_rows
        contrib_rows = drone_contrib_table(all_matches)
        if contrib_rows:
            out += ["", "## 드론별 기여 (판이 끝날 때 가진 드론 기준, 10/18 이후 빌드 기록만)"] + contrib_rows
        import_lines = import_summary(testers, all_matches)
        if import_lines:
            out += ["", "## 저장 코드 사용 (불러온 저장이 섞인 판은 따로 볼 것)"] + import_lines
        order_lines = pick_order_summary(all_matches)
        if order_lines:
            out += ["", "## 레벨업 선택 순서 (M 이동속도 · H 체력 · N 새 드론 · U 드론 강화, 10/18 이후 빌드 기록만)"] + order_lines
        early = [m for m in all_matches if not m["cleared"] and m["survive"] is not None and m["survive"] < 60]
        out += ["", f"60초 안쪽의 이른 사망 {len(early)}판 (조작 실수일 수 있어 판단에서 따로 뗀다)."]
        out += ["", f"레벨업 선택: {picks_summary(all_matches)}", f"성능: {perf_summary(all_matches)}"]
    out += ["", "## 테스터별 상세"]
    for t in testers:
        out.append("")
        out += tester_report(t)
    return "\n".join(out) + "\n"


def boss_summary(matches):
    """보스전(540초에 시작하는 1대1 보스전) 집계. 보스전 항목이 있는 판이 하나도 없으면(옛 빌드) None."""
    ms = [m for m in matches if m["boss"] is not None]
    if not ms:
        return None
    entered = [m for m in ms if m["boss"] == "진입"]
    text = f"판 {len(ms)}개 중 보스전에 들어간 판 {len(entered)}개"
    if entered:
        cleared = [m for m in entered if m["cleared"]]
        text += f" · 보스 처치(클리어) {len(cleared)}개({pct(len(cleared), len(entered))})"
        lost = [m for m in entered if not m["cleared"] and m["boss_hp"] is not None]
        if lost:
            text += f" · 패배한 판의 보스 남은 체력 평균 {mean([m['boss_hp'] for m in lost], 0)}%"
        secs = [m["boss_seconds"] for m in entered if m["boss_seconds"] is not None]
        if secs:
            text += f" · 보스전 평균 {mean(secs, 1)}초(최단 {min(secs):.0f}, 최장 {max(secs):.0f})"
        pattern = Counter()
        for m in entered:
            for kind, value in m["damage"].items():
                if kind.startswith("보스"):
                    pattern[kind] += value
        if pattern:
            text += " · 보스에게 받은 피해 합계 " + ", ".join(f"{k} {v:.0f}" for k, v in pattern.most_common())
        text += boss_detail_text(entered)
    return text


def boss_detail_text(entered):
    """보스전 상세 기록(10/18 이후 빌드)이 있는 판들의 집계. 하나도 없으면 빈 글자."""
    detail = [m for m in entered if m["slam"] is not None or m["dash"] is not None]
    if not detail:
        return ""

    def total(key, ms):
        got = [m[key] for m in ms if m[key] is not None]
        return (sum(h for h, _ in got), sum(u for _, u in got)) if got else None

    def show(label, pair):
        if not pair or pair[1] == 0:
            return ""
        return f"{label} {pair[0]}/{pair[1]}번 맞음({pct(pair[0], pair[1])})"

    parts = []
    entry = [m["boss_entry"] for m in detail if m["boss_entry"]]
    if entry:
        parts.append(f"들어갈 때 평균 이동속도 {mean([e['speed'] for e in entry], 1)}·최대 체력 {mean([e['max_hp'] for e in entry], 0)}·레벨 {mean([e['level'] for e in entry], 0)}")
    rages = [m["rage_at"] for m in detail if m["rage_at"] is not None]
    if rages:
        parts.append(f"분노는 보스전 {mean(rages, 1)}초째에 시작(분노한 판 {len(rages)}/{len(detail)})")
    for label, key in (("내려찍기", "slam"), ("└ 분노 중", "rage_slam"), ("돌진", "dash"), ("└ 분노 중", "rage_dash")):
        text = show(label, total(key, detail))
        if text:
            parts.append(text)
    ends = [m["boss_end_hp"] for m in detail if m["boss_end_hp"] is not None]
    if ends:
        parts.append(f"보스전이 끝날 때 내 체력 평균 {mean(ends, 0)}%")
    return " · 상세(" + str(len(detail)) + "판): " + " / ".join(parts)


def time_summary(matches):
    """시간 구성(10/18 이후 빌드): 한 판의 실제 소요 시간을 일시정지·레벨업 선택·그 밖(탭 방치 등)으로 나눈다. 없으면 None."""
    ms = [m for m in matches if m["paused"] is not None and m["select_screen"] is not None and m["real"] is not None]
    reals = [m["real"] for m in matches if m["real"] is not None]
    if not ms:
        if not reals:
            return None
        return f"실제 소요 시간 중앙값 {fmt_clock(statistics.median(reals))} ({len(reals)}판, 일시정지·선택 시간 기록은 아직 없음)"
    others = [max(0.0, m["real"] - (m["survive"] or 0) - m["paused"] - m["select_screen"]) for m in ms]
    active = [m["real"] - m["paused"] - m["select_screen"] for m in ms]
    return (f"판 {len(ms)}개: 실제 소요 중앙값 {fmt_clock(statistics.median([m['real'] for m in ms]))}"
            f" · 일시정지·선택 제외 중앙값 {fmt_clock(statistics.median(active))}"
            f" · 평균 일시정지 {mean([m['paused'] for m in ms], 0)}초, 레벨업 선택 화면 {mean([m['select_screen'] for m in ms], 0)}초"
            f" · 그 밖(게임 시간 외 흐른 시간: 탭 방치 등) 중앙값 {statistics.median(others):.0f}초")


def fmt_clock(seconds):
    total = int(seconds)
    return f"{total // 60}:{total % 60:02d}"


def aim_summary(matches):
    """마우스 조준(10/18 이후 빌드): 감도 분포와 커서 고정 결과. 폭발 드론을 쓴 판이 없으면 None."""
    ms = [m for m in matches if m["aim_lock"] or m["aim_sens"] is not None]
    if not ms:
        return None
    sens = Counter(int(m["aim_sens"]) for m in ms if m["aim_sens"] is not None)
    locks = Counter(m["aim_lock"] for m in ms if m["aim_lock"])
    text = f"폭발 드론을 쓴 판 {len(ms)}개"
    if sens:
        text += " · 감도 " + ", ".join(f"{k}%({v})" for k, v in sorted(sens.items()))
    if locks:
        text += " · 커서 고정 " + ", ".join(f"{k}({v})" for k, v in locks.most_common())
    return text


def write_matches_csv(path, testers):
    fields = ["tester", "build", "slot", "when", "stage", "cleared", "survive", "real", "level", "power", "recommended", "ratio",
              "death", "fps", "hitches", "worst_ms", "boss", "boss_hp", "boss_seconds",
              "paused", "select_screen", "aim_sens", "aim_lock", "slam_hit", "slam_used", "dash_hit", "dash_used", "rage_at", "boss_end_hp"]
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f)
        w.writerow(fields)
        seen = set()
        for t in testers:
            for s in t.slots:
                for m in s.matches:
                    key = (t.tester, s.number, m["when"], m["stage"])
                    if key in seen:
                        continue
                    seen.add(key)
                    w.writerow([t.tester, t.build, s.number, m["when"], m["stage"], int(m["cleared"]), m["survive"], m["real"], m["level"],
                                m["power"], m["rec"], f"{m['ratio']:.3f}" if m["ratio"] is not None else "", m["death"] or "",
                                m["fps"], m["hitches"], m["worst_ms"], m["boss"] or "", m["boss_hp"], m["boss_seconds"],
                                m["paused"], m["select_screen"], m["aim_sens"], m["aim_lock"] or "",
                                m["slam"][0] if m["slam"] else "", m["slam"][1] if m["slam"] else "", m["dash"][0] if m["dash"] else "", m["dash"][1] if m["dash"] else "",
                                m["rage_at"], m["boss_end_hp"]])


def main():
    ap = argparse.ArgumentParser(description="테스트 로그 파일을 읽어 마크다운 보고서로 정리한다.")
    ap.add_argument("files", nargs="+", help="내려받은 로그 파일(.txt) 또는 로그가 들어 있는 폴더(폴더 안의 .txt를 전부 읽는다)")
    ap.add_argument("--out", help="보고서를 저장할 파일(마크다운). 없으면 화면에 출력")
    ap.add_argument("--matches-csv", help="모든 판을 한 줄씩 담은 CSV 저장 경로")
    ap.add_argument("--build", help="이 빌드 버전의 로그 파일만 분석한다(예: 0.3.0 = OBT). CBT(0.2.1)와 OBT 로그가 같은 폴더에 섞여 있을 때 쓴다")
    args = ap.parse_args()

    paths = []
    for p in args.files:
        if os.path.isdir(p):
            # 폴더를 주면 그 안(하위 폴더 포함)의 .txt를 이름 순으로 전부 읽는다. (tester_logs/cbt 같은 보관 폴더를 통째로 분석할 때)
            for root, _, names in sorted(os.walk(p)):
                paths += [os.path.join(root, n) for n in sorted(names) if n.lower().endswith(".txt")]
        else:
            paths.append(p)
    if not paths:
        sys.exit("읽을 로그 파일이 없다.")
    testers = [read_file(p) for p in paths]
    if args.build:
        # 파일 맨 위 머리글의 빌드 버전이 같은 파일만 남긴다. (한 파일 안의 판은 나누지 않는다)
        found = sorted({t.build for t in testers})
        testers = [t for t in testers if t.build == args.build]
        if not testers:
            sys.exit(f"빌드 {args.build}인 로그 파일이 없다. 읽은 파일의 빌드: {', '.join(found)}")
    report = build_report(testers)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write(report)
        print(f"보고서를 저장했다: {args.out}")
    else:
        sys.stdout.reconfigure(encoding="utf-8")
        print(report)
    if args.matches_csv:
        write_matches_csv(args.matches_csv, testers)
        print(f"판 목록 CSV를 저장했다: {args.matches_csv}")


if __name__ == "__main__":
    main()
