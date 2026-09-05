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
        private const string VolumeKey = "MasterVolume";
        private const string FullscreenKey = "Fullscreen";

        private Slider volumeSlider;
        private Toggle fullscreenToggle;

        private void Awake()
        {
            volumeSlider = transform.Find("VolumeSlider").GetComponent<Slider>();
            fullscreenToggle = transform.Find("FullscreenToggle").GetComponent<Toggle>();

            // 저장된 값이 있으면 설정 패널을 열지 않아도 게임 시작 시점에 바로 적용한다.
            float savedVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            AudioListener.volume = savedVolume;
            bool savedFullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            Screen.fullScreen = savedFullscreen;

            volumeSlider.value = savedVolume;
            fullscreenToggle.isOn = savedFullscreen;

            volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
            fullscreenToggle.onValueChanged.AddListener(HandleFullscreenChanged);
        }

        private void HandleVolumeChanged(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(VolumeKey, value);
        }

        private void HandleFullscreenChanged(bool isOn)
        {
            Screen.fullScreen = isOn;
            PlayerPrefs.SetInt(FullscreenKey, isOn ? 1 : 0);
        }
    }
}
