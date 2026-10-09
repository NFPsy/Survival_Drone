using System.IO;
using SurvivalDrone.Meta;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 개발 중에 재화를 빠르게 채우거나 저장을 초기화하는 에디터 전용 메뉴.
    // 뽑기 화면을 테스트하려면 코어가 필요한데, 매번 판을 돌 수는 없으니 만든 도구다. (게임 빌드에는 포함되지 않는다)
    public static class MetaDebugMenu
    {
        // 메뉴는 Play 모드(게임 실행 중)에서만 눌러진다 — CurrencyManager가 실행 중에만 존재하기 때문.
        [MenuItem("SurvivalDrone/Meta/Debug/Add 3000 Core (Play mode)")]
        private static void AddCore()
        {
            CurrencyManager.Instance.AddCore(3000);
        }

        [MenuItem("SurvivalDrone/Meta/Debug/Add 3000 Core (Play mode)", true)]
        private static bool AddCoreValidate() => Application.isPlaying && CurrencyManager.Instance != null;

        [MenuItem("SurvivalDrone/Meta/Debug/Add 1000 Credit (Play mode)")]
        private static void AddCredit()
        {
            CurrencyManager.Instance.AddCredit(1000);
        }

        [MenuItem("SurvivalDrone/Meta/Debug/Add 1000 Credit (Play mode)", true)]
        private static bool AddCreditValidate() => Application.isPlaying && CurrencyManager.Instance != null;

        // "하루가 지난 상태"를 흉내 내는 메뉴. 일일 퀘스트·출석·조각 교환 횟수는 "저장된 날짜가 오늘과 다르면 새로 시작"하는 규칙이라,
        // 저장된 날짜 두 개를 어제로 돌려놓으면 다음에 열 때 정말 다음 날처럼 초기화된다. (기기 날짜를 바꾸지 않아도 된다)
        // 확인 방법: 이 메뉴를 누른 뒤 로비로 다시 들어가(다른 화면에 갔다 오기) "일일 퀘스트"에서 출석하기가 "받기"로 바뀌는지,
        // 격납고의 조각 교환이 "오늘 0/3"으로 돌아왔는지 본다. 달성·수령 기록은 진짜 다음 날처럼 모두 지워진다(코어·크레딧은 그대로).
        [MenuItem("SurvivalDrone/Meta/Debug/Simulate Next Day (Play mode)")]
        private static void SimulateNextDay()
        {
            string yesterday = System.DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var data = SaveManager.Data;
            data.dailyQuestDate = yesterday;
            data.shardExchangeDate = yesterday;
            SaveManager.Save();
            Debug.Log($"[Debug] 다음 날 시뮬레이션: 일일 퀘스트·조각 교환 날짜를 {yesterday}로 돌렸습니다. 로비로 다시 들어가서 확인하세요.");
        }

        [MenuItem("SurvivalDrone/Meta/Debug/Simulate Next Day (Play mode)", true)]
        private static bool SimulateNextDayValidate() => Application.isPlaying && CurrencyManager.Instance != null;

        // 스테이지 2~5를 직접 해 보려고 앞 스테이지를 매번 깨지 않도록, 모든 스테이지를 열어 주는 메뉴.
        // 확인 방법: 이 메뉴를 누른 뒤 로비의 "스테이지 목록"을 다시 열면(다른 화면에 갔다 와도 됨) 5장이 모두 열려 있다.
        [MenuItem("SurvivalDrone/Meta/Debug/Unlock All Stages (Play mode)")]
        private static void UnlockAllStages()
        {
            SaveManager.Data.unlockedStageCount = StageProgress.Instance.StageCount;
            SaveManager.Save();
            Debug.Log($"[Debug] 모든 스테이지({SaveManager.Data.unlockedStageCount}개)를 열었습니다. 스테이지 목록을 다시 열어서 확인하세요.");
        }

        [MenuItem("SurvivalDrone/Meta/Debug/Unlock All Stages (Play mode)", true)]
        private static bool UnlockAllStagesValidate() => Application.isPlaying && StageProgress.Instance != null;

        // 뽑기 화면이 생기기 전에 뽑기 흐름을 직접 눌러 볼 수 있는 메뉴. 결과는 콘솔에 [Gacha] 로그로 찍힌다.
        [MenuItem("SurvivalDrone/Meta/Debug/Gacha Pull x1 (Play mode)")]
        private static void PullSingle()
        {
            GachaController.Instance.PullSingle();
        }

        [MenuItem("SurvivalDrone/Meta/Debug/Gacha Pull x1 (Play mode)", true)]
        private static bool PullSingleValidate() => Application.isPlaying && GachaController.Instance != null;

        [MenuItem("SurvivalDrone/Meta/Debug/Gacha Pull x10 (Play mode)")]
        private static void PullTen()
        {
            GachaController.Instance.PullTen();
        }

        [MenuItem("SurvivalDrone/Meta/Debug/Gacha Pull x10 (Play mode)", true)]
        private static bool PullTenValidate() => Application.isPlaying && GachaController.Instance != null;

        // 저장 파일을 "모두" 지운다: 예전 save.json과 슬롯 파일(save_slot1~3.json) 전부.
        // 예전에는 "지금 쓰는 파일 하나"만 지웠는데, 에디터에서는 슬롯을 고르지 않은 상태라 save.json만 지워지고
        // 슬롯 파일은 그대로 남아서, 슬롯을 다시 고르면 옛 데이터가 되살아난 것처럼 보였다.
        // 게임을 실행 중이 아닐 때 눌러야 다음 실행이 완전히 새 데이터로 시작한다. (실행 중이면 게임이 곧바로 다시 저장해 버린다)
        [MenuItem("SurvivalDrone/Meta/Debug/Delete Save File")]
        private static void DeleteSave()
        {
            // 지우면 되돌릴 수 없으므로, 지우기 전에 "어떤 저장 파일이 있는지"를 하나씩 보여 주고 한 번 더 확인한다.
            // (직접 플레이해서 생긴 저장을 실수로 지우는 사고를 막기 위한 장치)
            var files = SaveManager.ListSaveFiles();
            if (files.Count == 0)
            {
                EditorUtility.DisplayDialog("저장 파일 삭제", "지울 저장 파일이 없습니다. (예전 save.json과 슬롯 1~3 파일을 모두 확인했습니다)", "확인");
                return;
            }

            var summary = new System.Text.StringBuilder();
            foreach (string path in files)
            {
                string info = "(내용을 읽지 못했습니다)";
                try
                {
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                    info = $"코어 {data.core}, 크레딧 {data.credit}, 드론 {data.ownedDrones.Count}종, 해금 스테이지 {data.unlockedStageCount}개, 테스터 {data.testerId}, 기록 {data.playLog.Count}줄";
                }
                catch (System.Exception) { }

                string modified = File.GetLastWriteTime(path).ToString("MM-dd HH:mm:ss");
                summary.AppendLine($"- {Path.GetFileName(path)} (수정 {modified}): {info}");
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "저장 파일 삭제",
                $"아래 저장 파일 {files.Count}개를 모두 삭제합니다. 되돌릴 수 없습니다.\n\n{summary}\n직접 플레이해서 생긴 저장이 아닌지 확인하세요.",
                "모두 삭제", "취소");
            if (!confirmed) return;

            SaveManager.DeleteAllSaves();
        }

        // 게임을 실행 중일 때는 지워도 게임이 곧바로 다시 저장해 버리므로 메뉴를 막아 둔다.
        [MenuItem("SurvivalDrone/Meta/Debug/Delete Save File", true)]
        private static bool DeleteSaveValidate() => !Application.isPlaying;
    }
}
