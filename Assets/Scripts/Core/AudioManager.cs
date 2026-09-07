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
