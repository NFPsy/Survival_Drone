using UnityEngine;

namespace SurvivalDrone.Core
{
    // 폭발 드론의 폭발 위치를 정하는 "마우스 감도" 설정값.
    // 설정 창(SettingsPanelController)이 PlayerPrefs에 저장하고, 폭발 드론(ExplosionDrone)이 읽어 쓴다.
    //
    // 감도 1 = 100%는 마우스를 움직인 픽셀 수만큼 조준 커서도 움직이는 기본값이다.
    // 감도를 올리면 같은 손 움직임에 커서가 더 멀리 가고(빠름), 내리면 덜 간다(느림).
    public static class AimSettings
    {
        // PlayerPrefs에 저장할 때 쓰는 키 이름.
        public const string SensitivityKey = "AimSensitivity";

        // 설정 창 슬라이더의 범위와 기본값. (숫자 입력 칸에서는 25 ~ 300 %)
        public const float MinSensitivity = 0.25f;
        public const float MaxSensitivity = 3f;
        public const float DefaultSensitivity = 1f;

        // 저장된 감도를 읽는다. 저장한 적이 없으면 기본값(1)이고, 범위를 벗어난 값은 범위 안으로 맞춘다.
        public static float LoadSensitivity()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity), MinSensitivity, MaxSensitivity);
        }
    }
}
