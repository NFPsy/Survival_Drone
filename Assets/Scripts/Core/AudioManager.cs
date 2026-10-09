using UnityEngine;

namespace SurvivalDrone.Core
{
    // 사운드 이펙트(SFX)와 배경음악(BGM)을 재생해주는 매니저.
    // 지금은 오디오 클립이 하나도 없어서 실제로 소리가 나지 않지만,
    // 6주차 사운드 작업 때 클립 파일만 인스펙터에 끼워 넣으면 바로 재생되도록
    // 미리 배관(뼈대)만 만들어두는 스크립트다.
    //
    // MainMenu 씬과 InGame(실제 플레이 씬)을 오가도 배경음악이 끊기지 않도록
    // DontDestroyOnLoad로 씬이 바뀌어도 사라지지 않게 만든다.
    public class AudioManager : MonoBehaviour
    {
        // 어디서든 AudioManager.Instance로 이 매니저에 접근할 수 있게 해주는 정적 변수.
        public static AudioManager Instance { get; private set; }

        // 설정에서 저장한 볼륨을 PlayerPrefs에서 찾을 때 쓰는 키 이름. (저장은 SettingsPanelController가 한다)
        // 배경음악과 효과음을 따로 조절한다. 값은 1 = 100%(기본), 2 = 200%까지.
        public const string MusicVolumeKey = "MusicVolume";
        public const string SfxVolumeKey = "SfxVolume";

        // 예전에 하나뿐이던 "마스터 볼륨"의 키. 지금은 쓰지 않지만, 예전에 소리를 줄여 둔 사람의 설정이 100%로 돌아가지 않도록
        // 새 키가 아직 없을 때만 이 값을 배경음악·효과음의 처음 값으로 물려받는다.
        public const string MasterVolumeKey = "MasterVolume";

        // 저장된 볼륨(배경음악 또는 효과음)을 읽는다. 새 키가 없으면 예전 마스터 볼륨을, 그것도 없으면 1(100%)을 돌려준다.
        public static float LoadVolume(string key)
        {
            if (PlayerPrefs.HasKey(key)) return PlayerPrefs.GetFloat(key, 1f);
            return PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        }

        // 게임이 켜지자마자(첫 씬이 로드되기 전에) 저장된 볼륨을 적용한다.
        // 예전에는 이 일을 SettingsPanelController의 Awake가 했는데, 설정 패널은 씬에서 꺼진 채로 시작해서
        // 패널을 처음 열 때까지 Awake가 실행되지 않았다. 그래서 다시 시작하면 소리가 100%로 크게 들리다가
        // 설정 창을 열어야 저장한 값으로 돌아왔다. 씬 안의 오브젝트에 기대지 않는 이 방식은 어느 씬에서 시작해도 적용된다.
        // (이 시점엔 AudioManager가 아직 없을 수 있어서 전체 음량만 맞춘다. 소리별 음량은 Awake에서 맞춘다)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedVolumes()
        {
            ApplyVolumes(LoadVolume(MusicVolumeKey), LoadVolume(SfxVolumeKey));
        }

        // 배경음악 음량(music)과 효과음 음량(sfx)을 지금 소리에 적용한다. 1 = 100%, 2 = 200%.
        // 오디오 소스의 음량은 1을 넘길 수 없어서, 100%를 넘는 증폭은 전체 음량(AudioListener)이 맡는다.
        //   전체 음량 = 1과 두 값 중 가장 큰 값 / 소스 음량 = 각자의 값 ÷ 전체 음량  →  소리마다 (전체 × 소스) = 자기 값이 된다.
        public static void ApplyVolumes(float music, float sfx)
        {
            float gain = Mathf.Max(1f, music, sfx);
            AudioListener.volume = gain;
            if (Instance != null) Instance.ApplySourceVolumes(music / gain, sfx / gain);
        }

        private void ApplySourceVolumes(float musicScale, float sfxScale)
        {
            if (musicSource != null) musicSource.volume = musicScale;
            if (sfxSource != null) sfxSource.volume = sfxScale;
        }

        // 짧은 효과음(피격, 레벨업, 승리/패배 등)을 재생할 오디오 소스.
        // PlayOneShot을 쓰면 여러 소리가 겹쳐서 재생돼도 서로 끊기지 않는다.
        [SerializeField] private AudioSource sfxSource;

        // 배경음악(BGM)을 재생할 오디오 소스. 보통 loop=true로 계속 반복 재생한다.
        [SerializeField] private AudioSource musicSource;

        private void Awake()
        {
            // 씬 전환(MainMenu -> InGame)으로 AudioManager가 중복 생성될 수 있으므로,
            // 이미 하나가 존재한다면 방금 만들어진 쪽을 스스로 파괴해서 항상 하나만 남긴다.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 저장해 둔 볼륨을 이 매니저의 두 소스에도 맞춘다. (게임 시작 직후엔 매니저가 아직 없어서 위의 static 함수가 못 한 일)
            ApplyVolumes(LoadVolume(MusicVolumeKey), LoadVolume(SfxVolumeKey));
        }

        // 짧은 효과음 하나를 재생하는 함수. clip이 아직 비어있으면(사운드 파일이 없으면)
        // 아무 일도 하지 않고 조용히 넘어가므로, 클립 없이 호출해도 에러가 나지 않는다.
        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip, volumeScale);
        }

        // 배경음악을 재생하는 함수. 이미 같은 곡이 재생 중이라면 처음부터 다시 틀지 않는다.
        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (clip == null || musicSource == null) return;
            if (musicSource.clip == clip && musicSource.isPlaying) return;

            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.Play();
        }

        // 배경음악을 멈추는 함수 (예: 결과 화면으로 전환할 때).
        public void StopMusic()
        {
            if (musicSource != null) musicSource.Stop();
        }
    }
}
