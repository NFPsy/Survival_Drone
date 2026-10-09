using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;

namespace SurvivalDrone.UI
{
    // 설정 패널(메인 메뉴 설정 창, 일시정지 메뉴의 볼륨 줄)의 볼륨 슬라이더와 숫자 입력 칸(%)을 처리하고
    // PlayerPrefs에 저장해 다음 실행에도 값이 유지되게 하는 스크립트.
    // 배경음악과 효과음을 따로 조절한다. 슬라이더로 움직이든 숫자로 치든 같은 값을 쓴다.
    //
    // 자식 오브젝트 이름으로 컨트롤을 찾는다: MusicSlider / MusicInput (배경음악), SfxSlider / SfxInput (효과음).
    public class SettingsPanelController : MonoBehaviour
    {
        // 배경음악·효과음 하나를 조절하는 슬라이더와 숫자 칸, 저장 키를 묶은 것.
        // 슬라이더 최대값을 1보다 크게(예: 2) 설정해두면 원본 음원이 작게 녹음됐을 때도 더 크게 증폭해서 들을 수 있다.
        // 숫자는 슬라이더 값 × 100이다. (1 = 100% = 기본, 슬라이더 최대값이 2면 200%까지)
        private class Channel
        {
            public string key;       // PlayerPrefs 저장 키
            public Slider slider;
            public InputField input;
            public float value;      // 지금 볼륨 (1 = 100%)
        }

        // 효과음 볼륨을 바꿀 때 들려줄 시험음. 이 소리가 얼마나 큰지로 효과음 크기를 가늠한다. (보통 버튼 클릭음을 끼운다)
        // 비워 두면 시험음 없이 조용히 바뀐다.
        [SerializeField] private AudioClip sfxPreviewSound;

        // 슬라이더를 끌 때 값이 바뀔 때마다 소리를 내면 겹쳐서 드르륵거리므로, 이 시간(초)보다 촘촘하게는 내지 않는다.
        private const float PreviewMinInterval = 0.15f;
        private float _lastPreviewTime = -10f;

        private Channel music;
        private Channel sfx;

        private void Awake()
        {
            music = CreateChannel("Music", AudioManager.MusicVolumeKey);
            sfx = CreateChannel("Sfx", AudioManager.SfxVolumeKey);

            // 게임 시작 때의 적용은 AudioManager가 하지만, 패널이 처음 켜질 때도 같은 값으로 맞춰 둔다.
            AudioManager.ApplyVolumes(music.value, sfx.value);
        }

        // 자식에서 이름이 prefix로 시작하는 슬라이더·숫자 칸을 찾아 연결한다. (prefix = "Music" 또는 "Sfx")
        // 못 찾아도(이름이 바뀌었거나 지워졌으면) 예외 대신 경고 로그만 남기고 넘어간다.
        private Channel CreateChannel(string prefix, string key)
        {
            var channel = new Channel { key = key };

            var sliderTransform = transform.Find(prefix + "Slider");
            channel.slider = sliderTransform != null ? sliderTransform.GetComponent<Slider>() : null;
            if (channel.slider == null) Debug.LogWarning($"[SettingsPanelController] '{prefix}Slider' 자식 오브젝트를 찾지 못했습니다.");

            var inputTransform = transform.Find(prefix + "Input");
            channel.input = inputTransform != null ? inputTransform.GetComponent<InputField>() : null;
            if (channel.input == null) Debug.LogWarning($"[SettingsPanelController] '{prefix}Input' 자식 오브젝트를 찾지 못했습니다. (숫자로 볼륨 조정 불가)");

            // 이전에 저장해둔 값이 있으면 그 값을, 없으면 기본값(1=슬라이더 원래 최대 음량)을 가져온다.
            channel.value = AudioManager.LoadVolume(key);

            // UI에도 현재 값을 반영해서, 패널을 열었을 때 실제 상태와 다르게 보이지 않도록 한다.
            if (channel.slider != null)
            {
                channel.slider.SetValueWithoutNotify(channel.value);
                // 사용자가 슬라이더를 조작할 때마다 호출되도록 연결.
                channel.slider.onValueChanged.AddListener(value => HandleVolumeChanged(channel, value));
            }

            // 숫자 칸: 처음 값을 보여주고, 글자를 다 입력하고 나서(엔터를 치거나 칸 밖을 누르면) 그 값을 적용한다.
            // 입력하는 도중(글자가 바뀔 때마다)에는 적용하지 않는다. 예를 들어 "50"을 치는 중에 "5"만 적용되어 소리가 튀는 것을 막으려는 것이다.
            if (channel.input != null)
            {
                ShowVolumeNumber(channel, channel.value);
                channel.input.onEndEdit.AddListener(text => HandleVolumeInputEnded(channel, text));
            }

            return channel;
        }

        // 볼륨 슬라이더를 움직일 때마다 호출. 바로 적용하고 저장할 값을 기록해 둔다. 숫자 칸도 같은 값으로 맞춘다.
        private void HandleVolumeChanged(Channel channel, float value)
        {
            channel.value = value;
            PlayerPrefs.SetFloat(channel.key, value);
            AudioManager.ApplyVolumes(music.value, sfx.value);
            ShowVolumeNumber(channel, value);

            // 효과음 크기를 바꿨을 때만 시험음을 들려준다. (배경음악은 계속 흐르고 있어서 바뀐 크기를 바로 들을 수 있다)
            if (channel == sfx) PlayPreview();
        }

        // 시험음을 한 번 낸다. 새 볼륨이 이미 적용된 뒤에 부르므로 바뀐 크기로 들린다.
        // 일시정지 중(Time.timeScale = 0)에도 간격이 흐르도록 unscaledTime을 쓴다.
        private void PlayPreview()
        {
            if (sfxPreviewSound == null || Time.unscaledTime - _lastPreviewTime < PreviewMinInterval) return;
            _lastPreviewTime = Time.unscaledTime;
            AudioManager.Instance?.PlaySfx(sfxPreviewSound);
        }

        // 숫자 칸에 입력을 마쳤을 때: 숫자(%)를 슬라이더 범위 안으로 맞춰서 슬라이더에 넣는다.
        // 슬라이더 값이 바뀌면 위의 HandleVolumeChanged가 불려서 적용·저장·숫자 표시까지 한꺼번에 처리된다.
        // 비었거나 숫자가 아니면 아무것도 바꾸지 않고 현재 값을 다시 보여준다.
        private void HandleVolumeInputEnded(Channel channel, string text)
        {
            if (channel.slider == null) return;

            if (!int.TryParse(text, out int percent))
            {
                ShowVolumeNumber(channel, channel.slider.value);
                return;
            }

            int maxPercent = Mathf.RoundToInt(channel.slider.maxValue * 100f);
            int minPercent = Mathf.RoundToInt(channel.slider.minValue * 100f);
            channel.slider.value = Mathf.Clamp(percent, minPercent, maxPercent) / 100f;

            // 슬라이더 값이 이미 같은 값이면 이벤트가 오지 않으므로(예: 500을 쳤는데 이미 200), 숫자 칸을 한 번 더 맞춘다.
            ShowVolumeNumber(channel, channel.slider.value);
        }

        // 숫자 칸에 현재 볼륨을 %로 보여준다 (0.73 → "73"). 칸을 고쳐 쓰는 이벤트는 일으키지 않는다.
        private static void ShowVolumeNumber(Channel channel, float value)
        {
            if (channel.input == null) return;
            channel.input.SetTextWithoutNotify(Mathf.RoundToInt(value * 100f).ToString());
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
