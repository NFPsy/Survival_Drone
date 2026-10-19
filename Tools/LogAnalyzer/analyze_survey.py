"""OBT 설문 응답 분석기.

구글폼 응답을 구글 시트에서 CSV로 내려받아 이 스크립트에 넣으면, 질문마다 답을 한눈에 정리한 마크다운 보고서를 만든다.
  - 객관식(보기가 몇 개 안 되는 질문): 보기별 인원수와 비율
  - 1~5점 질문: 평균과 점수별 인원수
  - 서술형: 답을 그대로 모아서 보여 줌
  - "플레이 로그" 칸에 붙여 넣은 로그에서 테스터 번호를 찾아, 어느 응답이 어느 테스터 것인지 표시한다
    (로그를 파일로 보냈다면 "파일로 보냄"이라고 적혀 있어서 번호를 모른다. 그 응답은 '번호 모름'으로 센다)

사용법 (저장소 맨 위 폴더에서):
    python Tools/LogAnalyzer/analyze_survey.py 응답.csv --out 설문보고서.md

- 질문 문구에 상관없이 열 모양만 보고 정리하므로, 폼 질문을 고쳐도 그대로 쓸 수 있다.
- 응답 CSV에는 개인 답변이 들어 있어서 저장소에 두지 말고 바탕화면 같은 곳에 저장한다. (저장소가 공개다)
- 파이썬 3.8 이상이면 되고, 따로 설치할 것은 없다.
"""
import argparse
import csv
import os
import sys
from collections import Counter

# analyze_logs.py 와 같은 폴더에 있으므로, 로그 머리글을 읽는 규칙(HEADER_RE)을 그대로 빌려 쓴다.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from analyze_logs import HEADER_RE  # noqa: E402

MAX_CHOICES = 8        # 서로 다른 답이 이 개수 이하이고 짧으면 객관식으로 본다
MAX_CHOICE_LEN = 40    # 객관식 보기의 최대 글자 수
LOG_MIN_LEN = 300      # 이보다 긴 답이 있는 열은 "플레이 로그 칸"으로 본다
TEXT_PREVIEW = 300     # 서술형 답은 이 글자 수까지만 보여 준다


def is_score(values):
    """모든 답이 1~5 정수면 점수 질문이다."""
    return all(v.isdigit() and 1 <= int(v) <= 5 for v in values)


def find_log_column(headers, rows):
    """가장 긴 답이 LOG_MIN_LEN 이상인 열을 로그 칸으로 고른다. 없으면 제목에 '로그'가 든 열."""
    best = None
    for i, _ in enumerate(headers):
        longest = max((len(r[i]) for r in rows if i < len(r)), default=0)
        if longest >= LOG_MIN_LEN and (best is None or longest > best[1]):
            best = (i, longest)
    if best:
        return best[0]
    for i, h in enumerate(headers):
        if "로그" in h:
            return i
    return None


def tester_of(log_text):
    """붙여 넣은 로그 맨 위의 '테스터: ABC123' 에서 번호를 꺼낸다. 못 찾으면 None."""
    m = HEADER_RE.search(log_text or "")
    return (m.group(1), m.group(2)) if m else None


def summarize_column(header, values):
    """질문 하나의 답을 마크다운 줄들로 정리한다."""
    answers = [v.strip() for v in values if v.strip()]
    lines = [f"### {header}", f"답한 사람 {len(answers)}명 / 응답 {len(values)}개", ""]
    if not answers:
        return lines + ["(답 없음)", ""]
    counts = Counter(answers)
    if is_score(answers):
        scores = [int(a) for a in answers]
        lines.append(f"평균 {sum(scores) / len(scores):.2f}점")
        lines.append("")
        lines.append("| 점수 | 인원 |")
        lines.append("|---|---|")
        for s in range(5, 0, -1):
            lines.append(f"| {s} | {counts.get(str(s), 0)} |")
    elif len(counts) <= MAX_CHOICES and all(len(a) <= MAX_CHOICE_LEN and "\n" not in a for a in counts):
        lines.append("| 답 | 인원 | 비율 |")
        lines.append("|---|---|---|")
        for a, n in counts.most_common():
            lines.append(f"| {a} | {n} | {round(n * 100 / len(answers))}% |")
    else:
        for a in answers:
            text = a.replace("\n", " ")
            lines.append(f"- {text[:TEXT_PREVIEW]}{'…' if len(text) > TEXT_PREVIEW else ''}")
    return lines + [""]


def build_report(headers, rows):
    log_col = find_log_column(headers, rows)
    out = ["# OBT 설문 응답 분석", "", f"응답 {len(rows)}개", ""]

    # 어느 응답이 어느 테스터 것인지 (로그 칸에서 번호 찾기)
    if log_col is not None:
        known = [tester_of(r[log_col] if log_col < len(r) else "") for r in rows]
        found = [k for k in known if k]
        out.append(f"로그 칸: '{headers[log_col]}' — 테스터 번호를 찾은 응답 {len(found)}개, 번호 모름(파일로 보냄 등) {len(known) - len(found)}개")
        if found:
            out.append("")
            out.append("| 테스터 | 빌드 | 응답 수 |")
            out.append("|---|---|---|")
            for (tester, build), n in Counter(found).most_common():
                out.append(f"| {tester} | {build} | {n} |")
        out.append("")

    out.append("## 질문별 답")
    out.append("")
    for i, h in enumerate(headers):
        if i == log_col:
            continue
        values = [r[i] if i < len(r) else "" for r in rows]
        # 맨 앞의 '타임스탬프' 열은 건너뛴다
        if "타임스탬프" in h or h.lower().startswith("timestamp"):
            continue
        out += summarize_column(h, values)
    return "\n".join(out) + "\n"


def main():
    ap = argparse.ArgumentParser(description="구글폼 응답 CSV를 질문별 마크다운 보고서로 정리한다.")
    ap.add_argument("csv_file", help="구글 시트에서 내려받은 응답 CSV")
    ap.add_argument("--out", help="보고서를 저장할 파일(마크다운). 없으면 화면에 출력")
    args = ap.parse_args()

    with open(args.csv_file, encoding="utf-8-sig", newline="") as f:
        data = list(csv.reader(f))
    if len(data) < 2:
        sys.exit("응답이 없다. (머리글 줄 아래에 응답 줄이 있어야 한다)")
    headers, rows = data[0], data[1:]
    report = build_report(headers, rows)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write(report)
        print(f"보고서를 저장했다: {args.out}")
    else:
        sys.stdout.reconfigure(encoding="utf-8")
        print(report)


if __name__ == "__main__":
    main()
