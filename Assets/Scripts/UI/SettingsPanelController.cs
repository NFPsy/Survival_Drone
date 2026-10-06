using UnityEngine;
using UnityEngine.UI;

namespace SurvivalDrone.UI
{
    // 설정 패널의 볼륨 슬라이더와 숫자 입력 칸(%)을 처리하고 PlayerPrefs에 저장해
    // 다음 실행에도 값이 유지되게 하는 스크립트. 슬라이더로 움직이든 숫자로 치든 같은 값을 쓴다.
    public class SettingsPanelController : MonoBehaviour
    {
        // PlayerPrefs에 값을 저장/불러올 때 사용하는 키 이름.
        private const string VolumeKey = "MasterVolume";

        // 마스터 볼륨을 조절하는 슬라이더. 최대값을 1보다 크게(예: 2) 설정해두면
        // 원본 음원이 작게 녹음됐을 때도 더 크게 증폭해서 들을 수 있다.
        private Slider volumeSlider;

        // 마스터 볼륨을 숫자(%)로 보여주고, 직접 입력해서 조정할 수 있는 입력 칸. 슬라이더와 항상 같은 값을 가리킨다.
        // 숫자는 슬라이더 값 × 100이다. (1 = 100% = 기본, 슬라이더 최대값이 2면 200%까지)
        private InputField volumeInput;

        private void Awake()
        {
            // 자식 오브젝트에서 슬라이더 컴포넌트를 찾아온다.
            // 못 찾아도(이름이 바뀌었거나 지워졌으면) 예외 대신 경고 로그만 남기고 넘어간다.
            var sliderTransform = transform.Find("VolumeSlider");
            volumeSlider = sliderTransform != null ? sliderTransform.GetComponent<Slider>() : null;
            if (volumeSlider == null) Debug.LogWarning("[SettingsPanelController] 'VolumeSlider' 자식 오브젝트를 찾지 못했습니다.");

            var inputTransform = transform.Find("VolumeInput");
            volumeInput = inputTransform != null ? inputTransform.GetComponent<InputField>() : null;
            if (volumeInput == null) Debug.LogWarning("[SettingsPanelController] 'VolumeInput' 자식 오브젝트를 찾지 못했습니다. (숫자로 볼륨 조정 불가)");

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

            // 숫자 칸: 처음 값을 보여주고, 글자를 다 입력하고 나서(엔터를 치거나 칸 밖을 누르면) 그 값을 적용한다.
            // 입력하는 도중(글자가 바뀔 때마다)에는 적용하지 않는다. 예를 들어 "50"을 치는 중에 "5"만 적용되어 소리가 튀는 것을 막으려는 것이다.
            if (volumeInput != null)
            {
                ShowVolumeNumber(savedVolume);
                volumeInput.onEndEdit.AddListener(HandleVolumeInputEnded);
            }
        }

        // 볼륨 슬라이더를 움직일 때마다 호출. 바로 적용하고 저장할 값을 기록해 둔다. 숫자 칸도 같은 값으로 맞춘다.
        private void HandleVolumeChanged(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(VolumeKey, value);
            ShowVolumeNumber(value);
        }

        // 숫자 칸에 입력을 마쳤을 때: 숫자(%)를 슬라이더 범위 안으로 맞춰서 슬라이더에 넣는다.
        // 슬라이더 값이 바뀌면 위의 HandleVolumeChanged가 불려서 적용·저장·숫자 표시까지 한꺼번에 처리된다.
        // 비었거나 숫자가 아니면 아무것도 바꾸지 않고 현재 값을 다시 보여준다.
        private void HandleVolumeInputEnded(string text)
        {
            if (volumeSlider == null) return;

            if (!int.TryParse(text, out int percent))
            {
                ShowVolumeNumber(volumeSlider.value);
                return;
            }

            int maxPercent = Mathf.RoundToInt(volumeSlider.maxValue * 100f);
            int minPercent = Mathf.RoundToInt(volumeSlider.minValue * 100f);
            volumeSlider.value = Mathf.Clamp(percent, minPercent, maxPercent) / 100f;

            // 슬라이더 값이 이미 같은 값이면 이벤트가 오지 않으므로(예: 500을 쳤는데 이미 200), 숫자 칸을 한 번 더 맞춘다.
            ShowVolumeNumber(volumeSlider.value);
        }

        // 숫자 칸에 현재 볼륨을 %로 보여준다 (0.73 → "73"). 칸을 고쳐 쓰는 이벤트는 일으키지 않는다.
        private void ShowVolumeNumber(float value)
        {
            if (volumeInput == null) return;
            volumeInput.SetTextWithoutNotify(Mathf.RoundToInt(value * 100f).ToString());
        }

        // 설정 패널이 닫힐 때(또는 씬이 바뀔 때) 기록해 둔 값을 실제로 저장한다.
        // 슬라이더를 움직이는 동안은 매번 저장하지 않고 이때 한 번만 저장한다.
        // 웹(WebGL) 빌드에서는 Save()를 불러야 브라우저 저장소로 옮겨지는데, 게임 종료 시점에 자동 저장되기를
        // 기대할 수 없다(탭을 닫으면 종료 알림이 오지 않을 수 있음). PC에서도 무해하다.
        private void OnDisable()
        {
            PlayerPrefs.Save();
        }
    }
}
