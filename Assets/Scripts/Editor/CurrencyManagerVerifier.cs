using SurvivalDrone.Meta;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // CurrencyManager와 SaveData가 규칙대로 동작하는지 확인하는 에디터 전용 검증 도구.
    // 메뉴 SurvivalDrone → Meta → Verify Currency 로 실행하면 결과가 콘솔에 [Currency] 접두사로 찍힌다.
    // 진짜 저장 파일은 건드리지 않고, 임시 데이터(메모리 안)로만 검사한다.
    public static class CurrencyManagerVerifier
    {
        private const string TablePath = "Assets/Data/Meta/CurrencyTable.asset";

        [MenuItem("SurvivalDrone/Meta/Verify Currency")]
        public static void Verify()
        {
            if (!EditorSafety.CanRunEditModeTool("Verify Currency")) return;

            var table = AssetDatabase.LoadAssetAtPath<CurrencyTable>(TablePath);
            if (table == null)
            {
                Debug.LogError($"[Currency] 검증 실패: {TablePath} 를 찾을 수 없습니다.");
                return;
            }

            // 씬 없이 쓰려고 임시 오브젝트를 만들고, 검사가 끝나면 지운다.
            var go = new GameObject("CurrencyManagerVerifier_Temp") { hideFlags = HideFlags.HideAndDontSave };
            int fails = 0;
            try
            {
                var manager = go.AddComponent<CurrencyManager>();
                var data = new SaveData();
                manager.Initialize(data, table, false);

                int changedCore = -1, changedCredit = -1;
                int insufficientCount = 0;
                CurrencyType lastType = CurrencyType.Core;
                int lastNeed = 0, lastHave = 0;
                manager.OnCoreChanged += v => changedCore = v;
                manager.OnCreditChanged += v => changedCredit = v;
                manager.OnInsufficient += (t, need, have) => { insufficientCount++; lastType = t; lastNeed = need; lastHave = have; };

                // 1) 시작 지급: 표에 적힌 값과 같고, 다시 Initialize해도 또 지급되지 않는다.
                fails += Check(manager.Core == table.StartCore && manager.Credit == table.StartCredit,
                    $"시작 지급 = 코어 {manager.Core}, 크레딧 {manager.Credit} (표: {table.StartCore}/{table.StartCredit})");
                manager.AddCore(500);
                manager.Initialize(data, table, false);
                fails += Check(manager.Core == table.StartCore + 500, $"이미 초기화된 데이터는 시작 지급을 다시 하지 않음 (코어 {manager.Core})");

                // 2) 획득 + 이벤트
                int before = manager.Core;
                manager.AddCore(1000);
                fails += Check(manager.Core == before + 1000 && changedCore == manager.Core, $"코어 +1000 → {manager.Core}, 이벤트 값 {changedCore}");
                manager.AddCredit(300);
                fails += Check(manager.Credit == table.StartCredit + 300 && changedCredit == manager.Credit, $"크레딧 +300 → {manager.Credit}, 이벤트 값 {changedCredit}");

                // 3) 차감 성공
                before = manager.Core;
                bool spent = manager.TrySpendCore(300);
                fails += Check(spent && manager.Core == before - 300, $"코어 300 사용 성공 → {manager.Core}");

                // 4) 부족: 차감도 없고 이벤트만 발생
                before = manager.Core;
                int need = before + 1;
                bool failedSpend = manager.TrySpendCore(need);
                fails += Check(!failedSpend && manager.Core == before && insufficientCount == 1 && lastType == CurrencyType.Core && lastNeed == need && lastHave == before,
                    $"코어 부족 시 차감 없음 + 부족 이벤트 1회 (필요 {lastNeed}, 보유 {lastHave})");

                // 5) 정확히 전액 사용은 가능
                bool spentAll = manager.TrySpendCore(manager.Core);
                fails += Check(spentAll && manager.Core == 0, $"보유량 전액 사용 가능 → {manager.Core}");

                // 6) 0 이하 금액은 무시
                before = manager.Core;
                // (이 세 줄은 일부러 잘못된 금액을 넣는 것이라 콘솔에 노란 경고 3개가 뜨는 것이 정상이다)
                manager.AddCore(0);
                manager.AddCore(-5);
                bool negativeSpend = manager.TrySpendCore(-5);
                fails += Check(manager.Core == before && !negativeSpend, "0 이하 금액은 무시됨 (경고 3개는 정상)");

                // 7) 판 보상 수치가 기획(결정 기록)과 같은지: 클리어 코어 100/150/200, 크레딧 200/300/400, 실패는 60초 미만이면 없음
                fails += Check(table.GetClearCore(1) == 100 && table.GetClearCore(2) == 150 && table.GetClearCore(3) == 200
                               && table.GetClearCredit(1) == 200 && table.GetClearCredit(2) == 300 && table.GetClearCredit(3) == 400
                               && Mathf.Approximately(table.FailMinSurviveSeconds, 60f),
                    $"판 보상 표 = 클리어 코어 {table.GetClearCore(1)}/{table.GetClearCore(2)}/{table.GetClearCore(3)}, 크레딧 {table.GetClearCredit(1)}/{table.GetClearCredit(2)}/{table.GetClearCredit(3)}, 실패 최소 생존 {table.FailMinSurviveSeconds}초");

                // 8) 보상 계산: 클리어는 스테이지별 전액 (범위 밖 스테이지 번호는 마지막 칸 값). 계산 확인용으로 판 길이를 360초로 넘긴다 (실제 게임의 판 길이와는 무관).
                table.CalculateMatchReward(true, 1, 400f, 360f, out int c, out int cr);
                bool clear1 = c == 100 && cr == 200;
                table.CalculateMatchReward(true, 2, 400f, 360f, out c, out cr);
                bool clear2 = c == 150 && cr == 300;
                table.CalculateMatchReward(true, 3, 400f, 360f, out c, out cr);
                bool clear3 = c == 200 && cr == 400;
                table.CalculateMatchReward(true, 99, 400f, 360f, out c, out cr);
                bool clear99 = c == 200 && cr == 400;
                fails += Check(clear1 && clear2 && clear3 && clear99, "클리어 보상: 스테이지 1 = 100/200, 2 = 150/300, 3 = 200/400 (범위 밖 스테이지는 200/400)");

                // 9) 보상 계산: 실패는 코어 항상 0, 크레딧은 60초 미만이면 0 · 60초 이후부터 (생존-60) ÷ (판 길이-60) 비율
                table.CalculateMatchReward(false, 1, 10f, 360f, out c, out cr);
                bool under1 = c == 0 && cr == 0;
                table.CalculateMatchReward(false, 1, 59.9f, 360f, out c, out cr);
                bool under2 = c == 0 && cr == 0;
                table.CalculateMatchReward(false, 1, 60f, 360f, out c, out cr);
                bool exactly60 = c == 0 && cr == 0;
                fails += Check(under1 && under2 && exactly60, "실패: 10초·59.9초·정확히 60초 생존은 보상 0");

                table.CalculateMatchReward(false, 1, 210f, 360f, out c, out cr);
                bool f210 = c == 0 && cr == 100;    // (210-60)/300 = 50% → 크레딧만
                table.CalculateMatchReward(false, 2, 330f, 360f, out c, out cr);
                bool f330 = c == 0 && cr == 270;    // (330-60)/300 = 90%
                table.CalculateMatchReward(false, 3, 359.9f, 360f, out c, out cr);
                bool f359 = c == 0 && cr == 400;    // 99.97% → 반올림하면 크레딧 전액
                table.CalculateMatchReward(false, 1, 500f, 360f, out c, out cr);
                bool over = c == 0 && cr == 200;    // 남은 적을 잡다가 판 길이를 넘겨 죽어도 크레딧 전액을 넘지 않음
                fails += Check(f210 && f330 && f359 && over, "실패는 코어 0 + 크레딧 비례: 스테이지1 210초 = 0/100, 스테이지2 330초 = 0/270, 스테이지3 359.9초 = 0/400, 판 길이 초과도 크레딧 전액까지만");

                // 10) 실제 지급: 클리어는 잔액이 늘고, 1분 미만 실패는 잔액이 그대로이며 경고도 없다
                int coreBefore = manager.Core, creditBefore = manager.Credit;
                manager.GrantMatchReward(true, 2, 400f, 360f, out int clearCore, out int clearCredit);
                fails += Check(clearCore == 150 && clearCredit == 300 && manager.Core == coreBefore + 150 && manager.Credit == creditBefore + 300,
                    $"스테이지 2 클리어 지급 코어 +{clearCore}, 크레딧 +{clearCredit}");
                coreBefore = manager.Core; creditBefore = manager.Credit;
                manager.GrantMatchReward(false, 1, 30f, 360f, out int shortCore, out int shortCredit);
                fails += Check(shortCore == 0 && shortCredit == 0 && manager.Core == coreBefore && manager.Credit == creditBefore,
                    "30초 만에 실패하면 지급 없음 (잔액 그대로)");
                coreBefore = manager.Core; creditBefore = manager.Credit;
                manager.GrantMatchReward(false, 1, 210f, 360f, out int failCore, out int failCredit);
                fails += Check(failCore == 0 && failCredit == 100 && manager.Core == coreBefore && manager.Credit == creditBefore + 100,
                    $"210초 실패 지급 코어 +{failCore} (잔액 그대로), 크레딧 +{failCredit}");

                // 11) 마일스톤: 달성해야 받을 수 있고(받기를 눌러야 지급), 번호마다 계정 전체에서 한 번만 지급 (3분 → 6분 → 처음 클리어), 두 번째부터는 0, 잘못된 번호도 0
                coreBefore = manager.Core;
                int mEarly = manager.TryClaimMilestone(0) + manager.TryClaimMilestone(1) + manager.TryClaimMilestone(MatchMilestones.ClearIndex);
                fails += Check(mEarly == 0 && manager.Core == coreBefore && !MatchMilestones.IsDone(data, 0),
                    $"마일스톤: 달성 전에는 받을 수 없음 (받은 코어 {mEarly}, 잔액 그대로)");

                bool newDone = MatchMilestones.MarkDone(data, 0);
                bool againDone = MatchMilestones.MarkDone(data, 0);
                MatchMilestones.MarkDone(data, 1);
                MatchMilestones.MarkDone(data, MatchMilestones.ClearIndex);
                bool badDone = MatchMilestones.MarkDone(data, 99);
                fails += Check(newDone && !againDone && !badDone && manager.Core == coreBefore && MatchMilestones.IsDone(data, 0) && !MatchMilestones.IsClaimed(data, 0),
                    "마일스톤: 달성 기록은 한 번만 새로 기록되고, 달성만으로는 코어가 들어오지 않음");

                // 옛 저장(달성 기록 없이 받은 기록만 있는 파일)도 달성한 것으로 읽힌다
                var legacy = new SaveData { milestoneClaimedMask = 1 };
                fails += Check(MatchMilestones.IsDone(legacy, 0) && MatchMilestones.IsClaimed(legacy, 0) && !MatchMilestones.IsDone(legacy, 1),
                    "마일스톤: 받은 기록만 있는 옛 저장도 안전하게 읽힘");

                int m0 = manager.TryClaimMilestone(0);
                int m0Again = manager.TryClaimMilestone(0);
                int m1 = manager.TryClaimMilestone(1);
                int mClear = manager.TryClaimMilestone(MatchMilestones.ClearIndex);
                int mClearAgain = manager.TryClaimMilestone(MatchMilestones.ClearIndex);
                int mBad = manager.TryClaimMilestone(99);
                fails += Check(m0 == MatchMilestones.Cores[0] && m1 == MatchMilestones.Cores[1] && mClear == MatchMilestones.Cores[2]
                               && m0Again == 0 && mClearAgain == 0 && mBad == 0
                               && manager.Core == coreBefore + MatchMilestones.Cores[0] + MatchMilestones.Cores[1] + MatchMilestones.Cores[2],
                    $"마일스톤 지급 3분 +{m0} / 6분 +{m1} / 처음 클리어 +{mClear}, 재청구·잘못된 번호는 0 (재청구 {m0Again}/{mClearAgain}, 잘못된 번호 {mBad})");

                // 12) 일일 퀘스트: 달성 전에는 못 받고, 달성하면 한 번만 받고, 다음 날이 되면 기록이 초기화되어 다시 받을 수 있다
                const string day1 = "2026-10-01", day2 = "2026-10-02";
                coreBefore = manager.Core;
                int q0Early = manager.TryClaimDailyQuest(0, day1);
                DailyQuests.MarkDone(data, 0, day1);
                int q0 = manager.TryClaimDailyQuest(0, day1);
                int q0Again = manager.TryClaimDailyQuest(0, day1);
                int q1NotDone = manager.TryClaimDailyQuest(1, day1);
                int qBad = manager.TryClaimDailyQuest(99, day1);
                fails += Check(q0Early == 0 && q0 == DailyQuests.Cores[0] && q0Again == 0 && q1NotDone == 0 && qBad == 0 && manager.Core == coreBefore + DailyQuests.Cores[0],
                    $"일일 퀘스트: 달성 전 {q0Early} / 달성 후 +{q0} / 재청구 {q0Again} / 미달성 {q1NotDone} / 잘못된 번호 {qBad}");
                bool resetOk = !DailyQuests.IsDone(data, 0, day2) && !DailyQuests.IsClaimed(data, 0, day2);
                DailyQuests.MarkDone(data, 2, day2);
                int q2NextDay = manager.TryClaimDailyQuest(2, day2);
                fails += Check(resetOk && q2NextDay == DailyQuests.Cores[2], $"다음 날(날짜 변경) 기록 초기화 후 새 퀘스트 +{q2NextDay}");

                // 13) 저장 형식(JSON) 왕복: 글자로 바꿨다가 되돌려도 값이 같다 (받은 마일스톤 기록 포함)
                string json = JsonUtility.ToJson(data);
                var loaded = JsonUtility.FromJson<SaveData>(json);
                fails += Check(loaded.core == data.core && loaded.credit == data.credit && loaded.isCurrencyInitialized == data.isCurrencyInitialized
                               && loaded.milestoneClaimedMask == data.milestoneClaimedMask && data.milestoneClaimedMask == 7
                               && loaded.milestoneDoneMask == data.milestoneDoneMask,
                    $"JSON 저장/불러오기 왕복 일치: {json}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            if (fails == 0) Debug.Log($"[Currency] 검증 완료: 모든 항목 통과 ({System.DateTime.Now:HH:mm:ss})");
            else Debug.LogError($"[Currency] 검증 완료: {fails}개 항목 실패 ({System.DateTime.Now:HH:mm:ss})");
        }

        private static int Check(bool ok, string message)
        {
            if (ok) Debug.Log($"[Currency] 통과 — {message}");
            else Debug.LogError($"[Currency] 실패 — {message}");
            return ok ? 0 : 1;
        }
    }
}
