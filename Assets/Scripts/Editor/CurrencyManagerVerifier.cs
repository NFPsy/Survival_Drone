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

                // 7) 판 보상
                int coreBefore = manager.Core, creditBefore = manager.Credit;
                manager.GrantMatchReward(true, out int clearCore, out int clearCredit);
                fails += Check(clearCore == table.ClearCore && clearCredit == table.ClearCredit && manager.Core == coreBefore + table.ClearCore && manager.Credit == creditBefore + table.ClearCredit,
                    $"클리어 보상 코어 +{clearCore}, 크레딧 +{clearCredit}");
                coreBefore = manager.Core; creditBefore = manager.Credit;
                manager.GrantMatchReward(false, out int failCore, out int failCredit);
                fails += Check(failCore == table.FailCore && failCredit == table.FailCredit && manager.Core == coreBefore + table.FailCore && manager.Credit == creditBefore + table.FailCredit,
                    $"실패 보상 코어 +{failCore}, 크레딧 +{failCredit}");

                // 8) 저장 형식(JSON) 왕복: 글자로 바꿨다가 되돌려도 값이 같다
                string json = JsonUtility.ToJson(data);
                var loaded = JsonUtility.FromJson<SaveData>(json);
                fails += Check(loaded.core == data.core && loaded.credit == data.credit && loaded.isCurrencyInitialized == data.isCurrencyInitialized,
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
