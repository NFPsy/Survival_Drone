using SurvivalDrone.UI;
using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 트레일러·홍보 영상을 녹화할 때 화면의 글자와 막대(HUD)를 잠깐 숨기는 에디터 전용 메뉴.
    // 게임 빌드에는 포함되지 않는다. (Editor 폴더에 있는 스크립트는 빌드에서 빠진다)
    //
    // 사용법: InGame을 에디터에서 플레이한 상태에서 단축키를 누른다. 한 번 누르면 숨기고, 다시 누르면 보인다.
    //   F7 = HUD 숨기기/보이기  (체력바·경험치바·오버드라이브 바·레벨·시간, 위쪽 알림 문구, 보스 체력바)
    //   F6 = 데미지 숫자 숨기기/보이기  (적 위로 떠오르는 숫자와 회복 숫자)
    // 레벨업 선택 화면, 일시정지 화면, 결과 화면, 오버드라이브·저체력 화면 효과, 바닥의 보스 예고 표시는 그대로 보인다.
    //
    // 어떻게 숨기나(쉬운 설명): 화면 UI 묶음에 CanvasGroup(묶음 전체의 투명도를 조절하는 부품)을 붙이고 투명도를 0으로 한다.
    // 오브젝트를 끄는 것이 아니라 "투명하게" 만드는 것이라서, 게임 규칙(체력 계산, 시간, 보스 체력바 갱신)은 그대로 돌아간다.
    // 이 부품은 플레이 중에만 붙이므로 씬 파일은 바뀌지 않고, 플레이를 멈추면 저절로 사라진다.
    [InitializeOnLoad]
    public static class RecordingMenu
    {
        // 지금 숨기는 중인지.
        private static bool hudHidden;
        private static bool damageNumbersHidden;

        // 한 번이라도 숨겼는지. 숨긴 적이 없으면 매 프레임 UI를 뒤지지 않는다.
        private static bool touched;

        static RecordingMenu()
        {
            // 에디터가 매 프레임 부르는 함수에 연결한다. (보스전이 시작되어 새 보스 체력바가 만들어져도 HUD의 자식이라 같이 숨겨진다)
            EditorApplication.update += Apply;

            // 플레이를 멈추면 상태를 되돌려서, 다음 플레이는 평소처럼 HUD가 보이게 한다.
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    hudHidden = false;
                    damageNumbersHidden = false;
                    touched = false;
                }
            };
        }

        [MenuItem("SurvivalDrone/Recording/Toggle HUD (Play mode) _F7")]
        private static void ToggleHud()
        {
            hudHidden = !hudHidden;
            touched = true;
            Apply();
            Debug.Log(hudHidden ? "[Recording] HUD를 숨겼습니다. (F7로 다시 보이기)" : "[Recording] HUD를 다시 보입니다.");
        }

        [MenuItem("SurvivalDrone/Recording/Toggle HUD (Play mode) _F7", true)]
        private static bool ToggleHudValidate() => Application.isPlaying;

        [MenuItem("SurvivalDrone/Recording/Toggle Damage Numbers (Play mode) _F6")]
        private static void ToggleDamageNumbers()
        {
            damageNumbersHidden = !damageNumbersHidden;
            touched = true;
            Apply();
            Debug.Log(damageNumbersHidden ? "[Recording] 데미지 숫자를 숨겼습니다. (F6로 다시 보이기)" : "[Recording] 데미지 숫자를 다시 보입니다.");
        }

        [MenuItem("SurvivalDrone/Recording/Toggle Damage Numbers (Play mode) _F6", true)]
        private static bool ToggleDamageNumbersValidate() => Application.isPlaying;

        // 지금 상태(숨김/보임)를 화면에 반영한다.
        private static void Apply()
        {
            if (!Application.isPlaying || !touched) return;

            var hud = Object.FindFirstObjectByType<HUDController>();
            if (hud != null) SetAlpha(hud.gameObject, hudHidden ? 0f : 1f);

            var numbers = Object.FindFirstObjectByType<DamageNumberDisplay>();
            if (numbers != null) SetAlpha(numbers.gameObject, damageNumbersHidden ? 0f : 1f);
        }

        // 오브젝트에 CanvasGroup을 (없으면) 붙이고 투명도를 바꾼다. 이미 같은 값이면 아무것도 하지 않는다.
        private static void SetAlpha(GameObject target, float alpha)
        {
            var group = target.GetComponent<CanvasGroup>();
            if (group == null)
            {
                if (alpha >= 1f) return; // 보이게 하려는데 부품이 없으면 이미 보이는 상태다
                group = target.AddComponent<CanvasGroup>();
            }
            if (!Mathf.Approximately(group.alpha, alpha)) group.alpha = alpha;
        }
    }
}
