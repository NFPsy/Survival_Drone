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

        // 저장 파일을 지운다. 게임을 실행 중이 아닐 때 눌러야 다음 실행이 완전히 새 데이터로 시작한다.
        [MenuItem("SurvivalDrone/Meta/Debug/Delete Save File")]
        private static void DeleteSave()
        {
            // 지우면 되돌릴 수 없으므로, 지우기 전에 "지금 어떤 저장이 있는지"를 보여 주고 한 번 더 확인한다.
            // (직접 플레이해서 생긴 저장을 실수로 지우는 사고를 막기 위한 장치)
            if (!File.Exists(SaveManager.FilePath))
            {
                EditorUtility.DisplayDialog("저장 파일 삭제", "지울 저장 파일이 없습니다.", "확인");
                return;
            }

            string summary = "(내용을 읽지 못했습니다)";
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveManager.FilePath));
                summary = $"코어 {data.core}, 크레딧 {data.credit}, 보유 드론 {data.ownedDrones.Count}종, 해금 스테이지 {data.unlockedStageCount}개, 테스터 {data.testerId}, 기록 {data.playLog.Count}줄";
            }
            catch (System.Exception) { }

            string modified = File.GetLastWriteTime(SaveManager.FilePath).ToString("yyyy-MM-dd HH:mm:ss");
            bool confirmed = EditorUtility.DisplayDialog(
                "저장 파일 삭제",
                $"이 저장 파일을 삭제합니다. 되돌릴 수 없습니다.\n\n{summary}\n마지막 수정: {modified}\n\n직접 플레이해서 생긴 저장이 아닌지 확인하세요.",
                "삭제", "취소");
            if (!confirmed) return;

            SaveManager.DeleteSave();
        }
    }
}
