using UnityEngine;
using UnityEngine.UI;

namespace SurvivalDrone.UI
{
    // 설정 패널의 볼륨 슬라이더/전체화면 토글을 처리하고 PlayerPrefs에 저장해
    // 다음 실행에도 값이 유지되게 하는 스크립트. 사운드가 아직 없는 시점이라
    // 볼륨 슬라이더는 지금 당장은 체감 효과가 없지만, 6주차에 사운드를 넣을 때
    // 바로 쓸 수 있도록 미리 만들어둔다.
    public class SettingsPanelController : MonoBehaviour
    {
        // PlayerPrefs에 값을 저장/불러올 때 사용하는 키 이름.
        private const string VolumeKey = "MasterVolume";
        private const string FullscreenKey = "Fullscreen";

        // 마스터 볼륨을 조절하는 슬라이더(0~1).
        private Slider volumeSlider;

        // 전체화면 여부를 켜고 끄는 토글.
        private Toggle fullscreenToggle;

        private void Awake()
        {
            // 자식 오브젝트에서 슬라이더/토글 컴포넌트를 찾아온다.
            volumeSlider = transform.Find("VolumeSlider").GetComponent<Slider>();
            fullscreenToggle = transform.Find("FullscreenToggle").GetComponent<Toggle>();

            // 이전에 저장해둔 값이 있으면 그 값을, 없으면 기본값(볼륨 1=최대,
            // 전체화면은 현재 화면 상태)을 가져온다.
            float savedVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            bool savedFullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;

            // 설정 패널을 열어보지 않아도, 게임이 시작되는 시점에 바로 적용되게 한다.
            AudioListener.volume = savedVolume;
            Screen.fullScreen = savedFullscreen;

            // UI에도 현재 값을 반영해서, 패널을 열었을 때 실제 상태와 다르게 보이지 않도록 한다.
            volumeSlider.value = savedVolume;
            fullscreenToggle.isOn = savedFullscreen;

            // 사용자가 슬라이더/토글을 조작할 때마다 아래 함수들이 호출되도록 연결.
            volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
            fullscreenToggle.onValueChanged.AddListener(HandleFullscreenChanged);
        }

        // 볼륨 슬라이더를 움직일 때마다 호출. 바로 적용하고 다음 실행을 위해 저장까지 한다.
        private void HandleVolumeChanged(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(VolumeKey, value);
        }

        // 전체화면 토글을 누를 때마다 호출. 바로 적용하고 저장까지 한다.
        private void HandleFullscreenChanged(bool isOn)
        {
            Screen.fullScreen = isOn;
            PlayerPrefs.SetInt(FullscreenKey, isOn ? 1 : 0);
        }
    }
}
