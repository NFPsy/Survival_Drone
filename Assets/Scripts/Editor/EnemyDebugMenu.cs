using SurvivalDrone.Enemies;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 미니 보스(3분)와 최종 보스(9분)를 기다리지 않고 바로 불러내는 에디터 전용 메뉴.
    // 오라 효과·넉백·보스 크기 같은 것을 확인하려고 매번 3분, 9분을 기다릴 수는 없어서 만든 도구다.
    // 게임 빌드에는 포함되지 않는다.
    //
    // 사용법: InGame을 플레이한 상태에서 상단 메뉴 SurvivalDrone → Enemies → Debug 에서 누르면
    // 플레이어 근처(8m)에 바로 나타난다. 정해진 시간에 나오는 진짜 미니 보스·보스는 따로 그대로 나온다.
    // 주의: 미니 보스를 잡으면 진짜처럼 드론 3택1 보상이 뜬다.
    public static class EnemyDebugMenu
    {
        [MenuItem("SurvivalDrone/Enemies/Debug/Spawn Mini Boss (Play mode)")]
        private static void SpawnMiniBoss()
        {
            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            if (spawner == null) return;
            spawner.DebugSpawnMiniBoss();
            Debug.Log("[Debug] 미니 보스를 소환했습니다. (플레이어 근처 8m)");
        }

        [MenuItem("SurvivalDrone/Enemies/Debug/Spawn Mini Boss (Play mode)", true)]
        private static bool SpawnMiniBossValidate() => Application.isPlaying && Object.FindFirstObjectByType<EnemySpawner>() != null;

        [MenuItem("SurvivalDrone/Enemies/Debug/Spawn Final Boss (Play mode)")]
        private static void SpawnFinalBoss()
        {
            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            if (spawner == null) return;
            spawner.DebugSpawnFinalBoss();
            Debug.Log("[Debug] 최종 보스를 소환했습니다. (플레이어 근처 8m)");
        }

        [MenuItem("SurvivalDrone/Enemies/Debug/Spawn Final Boss (Play mode)", true)]
        private static bool SpawnFinalBossValidate() => Application.isPlaying && Object.FindFirstObjectByType<EnemySpawner>() != null;
    }
}
