using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 검증 도구·씬 생성 도구가 "플레이 모드(게임 실행 중)"에서는 돌지 않게 막아주는 안전장치.
    //
    // 왜 필요한가:
    //  게임을 플레이해 보는 중에 이런 도구를 실행하면, 도구가 만드는 임시 오브젝트가 게임의 매니저(재화·보유 드론·스테이지)
    //  자리를 차지하거나 플레이 세션이 깨진다. (2026-09-30에 실제로 겪은 일 — 노션 버그 수정 기록 참고)
    //  검증은 플레이 모드가 아닌 평소 편집 상태에서만 돌린다.
    public static class EditorSafety
    {
        // 도구를 실행해도 되는 상태면 true. 플레이 중이면 이유를 콘솔에 남기고 false.
        public static bool CanRunEditModeTool(string toolName)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;

            Debug.LogWarning($"[검증] '{toolName}'은(는) 플레이 모드에서는 실행하지 않습니다. 플레이를 정지(Stop)한 뒤 다시 실행해주세요. (게임 실행 중에 실행하면 플레이 세션이 깨질 수 있습니다)");
            return false;
        }
    }
}
