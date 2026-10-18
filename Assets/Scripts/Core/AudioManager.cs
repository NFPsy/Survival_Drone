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
            musicSettingScale = musicScale;
            if (musicSource != null) musicSource.volume = musicScale * clipVolumeScale;
            if (sfxSource != null) sfxSource.volume = sfxScale;
        }

        // 설정 창의 배경음악 음량(오디오 소스에 실제로 줄 몫). 곡마다 다른 음량 배율(clipVolumeScale)을 곱해서 소스에 준다.
        private float musicSettingScale = 1f;

        // 지금 곡에만 곱하는 음량 배율. 1 = 그대로. 곡마다 녹음된 크기가 달라서, 곡이 바뀔 때 갑자기 크거나 작게 들리지 않게 맞추는 데 쓴다.
        private float clipVolumeScale = 1f;

        // "여러 곡을 순서대로 틀기"를 하는 중이면 그 곡 목록(아니면 null). 마지막 곡이 끝나면 처음 곡으로 돌아간다.
        private AudioClip[] musicSequence;
        private float[] musicSequenceScales;
        private int musicSequenceIndex;

        // 지금 곡이 실제로 재생되는 것을 한 번이라도 봤는지. "재생이 끝났다"를 "아직 시작 전"과 구별하려고 쓴다.
        private bool sequenceClipStarted;

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
        // 한 곡 반복 재생이라서, 여러 곡 순서대로 틀기(PlayMusicSequence)가 켜져 있었다면 그것은 끈다.
        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (clip == null || musicSource == null) return;

            defaultMusic = clip;   // 한 곡 반복으로 튼 마지막 곡(메인 메뉴 곡)을 기억해 둔다. 로비로 돌아올 때 이 곡으로 되돌린다

            musicSequence = null;
            musicSequenceScales = null;
            SetClipVolumeScale(1f);

            if (musicSource.clip == clip && musicSource.isPlaying)
            {
                musicSource.loop = loop;   // 순서대로 틀기 중이던 곡이면 반복 재생으로 되돌린다
                return;
            }

            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.Play();
        }

        // 한 곡 반복으로 마지막에 튼 곡(보통 메인 메뉴 곡). 판이 끝나고 로비로 돌아올 때 이 곡으로 되돌리려고 기억한다.
        private AudioClip defaultMusic;

        // 판 안에서 여러 곡을 순서대로 틀던 중이면 멈추고, 기억해 둔 한 곡 반복(메인 메뉴 곡, 음량 100%)으로 되돌린다.
        // 로비에 들어올 때 부른다. 순서대로 틀기 중이 아니면(메뉴에서 넘어온 평소 경우) 아무것도 하지 않아서 곡이 끊기지 않는다.
        public void ReturnToDefaultMusic()
        {
            if (musicSequence == null || defaultMusic == null) return;
            PlayMusic(defaultMusic);
        }

        // 여러 곡을 순서대로 이어서 틀고, 마지막 곡이 끝나면 처음 곡으로 돌아가는 배경음악 재생.
        // 판 안(InGame)에서 한 곡만 10분 내내 반복하면 지루해서 만들었다.
        //  clips = 곡 목록(앞에서부터 순서대로), volumeScales = 곡마다 곱할 음량 배율(곡 크기 차이를 맞추는 용, 길이는 clips와 같게).
        // 부르면 항상 목록의 첫 곡부터 처음부터 튼다. (판이 시작될 때마다 첫 곡으로 시작하게 하려는 것이라, 이미 같은 목록으로 틀고 있었어도 다시 첫 곡부터다)
        public void PlayMusicSequence(AudioClip[] clips, float[] volumeScales)
        {
            if (clips == null || clips.Length == 0 || musicSource == null) return;
            if (clips.Length == 1) { PlayMusic(clips[0]); return; }

            if (defaultMusic == null) defaultMusic = clips[clips.Length - 1];   // 메인 메뉴를 거치지 않고 시작한 경우(시험)에도 되돌릴 곡이 있게 한다
            musicSequence = clips;
            musicSequenceScales = volumeScales;
            StartSequenceClip(0);
        }

        private float ScaleAt(int index)
        {
            return musicSequenceScales != null && index < musicSequenceScales.Length ? musicSequenceScales[index] : 1f;
        }

        private void SetClipVolumeScale(float scale)
        {
            clipVolumeScale = scale;
            if (musicSource != null) musicSource.volume = musicSettingScale * clipVolumeScale;
        }

        private void StartSequenceClip(int index)
        {
            musicSequenceIndex = index;
            SetClipVolumeScale(ScaleAt(index));
            musicSource.clip = musicSequence[index];
            musicSource.loop = false;
            sequenceClipStarted = false;
            musicSource.Play();
        }

        // 순서대로 틀기 중에 지금 곡이 끝났는지 매 프레임 살핀다. 끝났으면 다음 곡(마지막이면 처음 곡)을 튼다.
        private void Update()
        {
            if (musicSequence == null || musicSource == null) return;

            if (musicSource.isPlaying)
            {
                sequenceClipStarted = true;
                return;
            }

            // 재생이 시작된 것을 본 뒤에 멈췄을 때만 "곡이 끝났다"고 본다. (브라우저가 소리를 막아서 아직 시작 못 한 경우는 건너뛴다)
            if (!sequenceClipStarted) return;
            StartSequenceClip((musicSequenceIndex + 1) % musicSequence.Length);
        }

        // 배경음악을 멈추는 함수 (예: 결과 화면으로 전환할 때).
        public void StopMusic()
        {
            musicSequence = null;   // 순서대로 틀기도 같이 끈다(안 그러면 멈춘 곡이 "끝난 곡"으로 보여 다음 곡이 켜진다)
            musicSequenceScales = null;
            SetClipVolumeScale(1f);
            if (musicSource != null) musicSource.Stop();
        }
    }
}
