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
            SaveManager.DeleteSave();
        }
    }
}
