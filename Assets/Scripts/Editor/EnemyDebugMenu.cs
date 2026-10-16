using SurvivalDrone.Drones;
using SurvivalDrone.Enemies;
using SurvivalDrone.Player;
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

        // 9분을 기다리지 않고 "진짜 보스전"(잡몹 소멸 → 경기장 → 보스 → 처치하면 승리)을 바로 시작한다. 단축키 F8.
        // 판 시간을 보스 등장 0.5초 전으로 건너뛰는 방식이라, 평소 흐름과 똑같이 진행된다.
        // 위의 "Spawn Final Boss"는 보스 한 마리만 불러내는 옛 도구라서 보스전 규칙(경기장 등)이 적용되지 않는다.
        [MenuItem("SurvivalDrone/Enemies/Debug/Start Boss Fight (Play mode) _F8")]
        private static void StartBossFight()
        {
            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            if (spawner == null) return;
            spawner.DebugStartBossFight();
            Debug.Log("[Debug] 판 시간을 보스 등장 직전으로 건너뛰었습니다. 곧 보스전이 시작됩니다.");
        }

        [MenuItem("SurvivalDrone/Enemies/Debug/Start Boss Fight (Play mode) _F8", true)]
        private static bool StartBossFightValidate() => Application.isPlaying && Object.FindFirstObjectByType<EnemySpawner>() != null && !BossFight.Active;

        // F8은 레벨 1 상태(이동속도 5, 드론 2개)로 바로 보스전에 들어가서 이길 수가 없다.
        // 그래서 "9분까지 잘 키운 판"을 흉내 낸 뒤 보스전을 시작하는 메뉴를 따로 만들었다. 단축키 F9.
        // 실제로 스테이지 1을 클리어한 판들의 기록(이동속도 4번 · 체력 1번 · 드론 5종 · 드론 강화 약 12번)을 본떴다.
        //  - 이동속도를 4번 고른 것처럼 올린다 (한 번에 +6%씩 곱해지는 규칙 그대로)
        //  - 최대 체력 +12 한 번
        //  - 드론 5종을 모두 얻고, 강화를 12번 나눠서 올린다 (최대 레벨 5 안에서 돌아가며 하나씩)
        // 이미 가진 것은 건너뛰므로 여러 번 눌러도 드론 수·최대 레벨을 넘지 않는다. 이동속도·체력은 누를 때마다 늘어난다.
        private const int CheatMoveSpeedPicks = 4;
        private const int CheatUpgradeCount = 12;

        [MenuItem("SurvivalDrone/Enemies/Debug/Start Boss Fight With Build (Play mode) _F9")]
        private static void StartBossFightWithBuild()
        {
            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            var drones = Object.FindFirstObjectByType<DroneManager>();
            var stats = Object.FindFirstObjectByType<PlayerStats>();
            if (spawner == null || drones == null || stats == null) return;

            // 이동속도: 레벨업 선택지와 같은 계산(현재 값의 6%를 더함)을 4번 반복한다.
            for (int i = 0; i < CheatMoveSpeedPicks; i++) stats.AddMoveSpeed(stats.MoveSpeed * 0.06f);
            stats.AddMaxHealth(12f);

            // 드론: 없는 종류는 새로 얻는다.
            foreach (DroneType type in System.Enum.GetValues(typeof(DroneType)))
            {
                if (drones.CanAddDrone(type)) drones.AddDrone(type);
            }

            // 강화: 드론을 돌아가며 한 단계씩 올린다. 더 올릴 수 있는 드론이 없으면 거기서 멈춘다.
            int upgraded = 0;
            bool progressed = true;
            while (upgraded < CheatUpgradeCount && progressed)
            {
                progressed = false;
                foreach (DroneType type in System.Enum.GetValues(typeof(DroneType)))
                {
                    if (upgraded >= CheatUpgradeCount) break;
                    if (drones.CanUpgradeDrone(type) && drones.UpgradeDrone(type))
                    {
                        upgraded++;
                        progressed = true;
                    }
                }
            }

            spawner.DebugStartBossFight();
            Debug.Log($"[Debug] 키운 상태로 보스전을 시작합니다: 이동속도 {stats.MoveSpeed:0.0}, 최대 체력 {stats.MaxHealth:0}, 드론 {drones.OwnedCount}종, 이번에 올린 강화 {upgraded}번.");
        }

        [MenuItem("SurvivalDrone/Enemies/Debug/Start Boss Fight With Build (Play mode) _F9", true)]
        private static bool StartBossFightWithBuildValidate() => StartBossFightValidate();
    }
}
