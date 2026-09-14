using UnityEngine;
using UnityEngine.UI;

namespace SurvivalDrone.UI
{
    // 설정 패널의 볼륨 슬라이더를 처리하고 PlayerPrefs에 저장해
    // 다음 실행에도 값이 유지되게 하는 스크립트.
    public class SettingsPanelController : MonoBehaviour
    {
        // PlayerPrefs에 값을 저장/불러올 때 사용하는 키 이름.
        private const string VolumeKey = "MasterVolume";

        // 마스터 볼륨을 조절하는 슬라이더. 최대값을 1보다 크게(예: 2) 설정해두면
        // 원본 음원이 작게 녹음됐을 때도 더 크게 증폭해서 들을 수 있다.
        private Slider volumeSlider;

        private void Awake()
        {
            // 자식 오브젝트에서 슬라이더 컴포넌트를 찾아온다.
            // 못 찾아도(이름이 바뀌었거나 지워졌으면) 예외 대신 경고 로그만 남기고 넘어간다.
            var sliderTransform = transform.Find("VolumeSlider");
            volumeSlider = sliderTransform != null ? sliderTransform.GetComponent<Slider>() : null;
            if (volumeSlider == null) Debug.LogWarning("[SettingsPanelController] 'VolumeSlider' 자식 오브젝트를 찾지 못했습니다.");

            // 이전에 저장해둔 값이 있으면 그 값을, 없으면 기본값(1=슬라이더 원래 최대 음량)을 가져온다.
            float savedVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);

            // 설정 패널을 열어보지 않아도, 게임이 시작되는 시점에 바로 적용되게 한다.
            AudioListener.volume = savedVolume;

            // UI에도 현재 값을 반영해서, 패널을 열었을 때 실제 상태와 다르게 보이지 않도록 한다.
            if (volumeSlider != null)
            {
                volumeSlider.value = savedVolume;
                // 사용자가 슬라이더를 조작할 때마다 호출되도록 연결.
                volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
            }
        }

        // 볼륨 슬라이더를 움직일 때마다 호출. 바로 적용하고 다음 실행을 위해 저장까지 한다.
        private void HandleVolumeChanged(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(VolumeKey, value);
        }
    }
}
