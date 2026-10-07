"""
시뮬레이터의 뽑기 로직이 노션에 기록된 검증 수치와 같은지 확인하는 스크립트.

실행: python validate.py

연속해서 200만 번 뽑았을 때(천장 카운트가 이어지는 상태)의 통계를 냅니다. 기대하는 값(노션 10/6 기록):
  - 소천장 끔: 꽝 10연(SR 이상이 하나도 없는 10연) 18.6%, SR 이상 비율 약 15.5%, SSR 1개당 평균 33.2회
  - 소천장 10: 꽝 10연 0%, SR 이상 비율 약 19%,   SSR 1개당 평균 33.2회
값이 크게 다르면 econ_sim.py의 규칙 숫자가 게임과 달라졌다는 신호이니 에셋 값과 다시 맞춰 보세요.
(처음 10번만 보면 소천장이 항상 10번째에 걸려서 비율이 다르게 나오므로, 꼭 '연속 스트림'으로 잽니다.)
"""
import random

from econ_sim import SR_, SSR_, Player, Rules

N = 2_000_000

for soft in (0, 10):
    p = Player(Rules(soft_pity=soft), random.Random(7))
    rs = [p.pull_single() for _ in range(N)]
    sr = sum(1 for x in rs if x >= SR_) / N
    blocks = (N - 10) // 10 + 1
    dead = sum(1 for i in range(0, N - 10, 10) if all(x < SR_ for x in rs[i:i + 10])) / blocks
    print(f"소천장={soft}: SR 이상 {sr * 100:.1f}% | 꽝 10연 {dead * 100:.1f}% | SSR 평균 비용 {N / sum(1 for x in rs if x == SSR_):.2f}회")
