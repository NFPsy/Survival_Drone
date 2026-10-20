using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 트레일러·홍보 영상용 게임 화면을 "깨끗하게" 녹화하는 에디터 전용 메뉴. 게임 빌드에는 포함되지 않는다.
    // Unity Recorder 패키지(유니티가 만든 공식 녹화 도구)를 코드로 설정해서, 창을 열고 옵션을 맞추는 과정 없이 키 하나로 시작한다.
    //
    // 사용법: InGame을 에디터에서 플레이한 상태에서
    //   F10 = 녹화 시작   F11 = 녹화 멈춤
    // 결과: video_work/clips/tr_take_001.mp4 처럼 저장된다. (번호는 녹화할 때마다 하나씩 커진다. 끝나면 이름을 바꿔서 쓴다)
    //
    // 설정: 게임 화면(Game 탭)만 1920x1080, 60프레임, MP4(H.264), 게임 소리 포함.
    //  - 에디터의 메뉴·패널은 찍히지 않고 게임 화면만 찍힌다. Game 탭이 눈에 보이는 상태여야 한다.
    //  - HUD를 숨기려면 같이 쓰는 RecordingMenu의 F7(HUD), F6(데미지 숫자)을 누른다.
    //  - 녹화 중에는 컴퓨터가 평소보다 무거워질 수 있다. 일정한 프레임 간격으로 찍기 때문에 게임이 조금 느려 보여도 영상은 부드럽다.
    [InitializeOnLoad]
    public static class RecordingRecorder
    {
        private static RecorderController controller;

        static RecordingRecorder()
        {
            // 녹화 도중 플레이를 멈추면 파일이 깨질 수 있으니, 플레이가 끝나기 전에 먼저 녹화를 멈춘다.
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) StopIfRecording();
            };
        }

        private static bool IsRecording => controller != null && controller.IsRecording();

        [MenuItem("SurvivalDrone/Recording/Start Recording (Play mode) _F10")]
        private static void StartRecording()
        {
            if (IsRecording) return;

            // 저장 폴더: 프로젝트 맨 위 폴더의 video_work/clips (깃에는 올라가지 않는 폴더)
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "video_work", "clips"));
            Directory.CreateDirectory(folder);

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "SurvivalD Trailer Recorder";
            movie.Enabled = true;
            // MP4(H.264), 화질은 높음. (예전 방식의 OutputFormat 설정은 더 이상 권장되지 않아 EncoderSettings로 지정한다)
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
            };
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
            movie.AudioInputSettings.PreserveAudio = true;
            // <Take>는 녹화할 때마다 001, 002…로 바뀌는 자리표시자다. 확장자(.mp4)는 자동으로 붙는다.
            // (Path.Combine은 < > 같은 글자를 "잘못된 경로 글자"로 거부하므로, 문자열을 직접 이어 붙인다)
            movie.OutputFile = folder.Replace('\\', '/') + "/tr_take_<Take>";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 60f;
            settings.CapFrameRate = true; // 60프레임 간격을 일정하게 지킨다

            controller = new RecorderController(settings);
            controller.PrepareRecording();
            controller.StartRecording();
            Debug.Log("[Recording] 녹화를 시작했습니다. (F11로 멈춤) 저장: video_work/clips/tr_take_번호.mp4");
        }

        [MenuItem("SurvivalDrone/Recording/Start Recording (Play mode) _F10", true)]
        private static bool StartRecordingValidate() => Application.isPlaying && !IsRecording;

        [MenuItem("SurvivalDrone/Recording/Stop Recording _F11")]
        private static void StopRecording()
        {
            StopIfRecording();
        }

        [MenuItem("SurvivalDrone/Recording/Stop Recording _F11", true)]
        private static bool StopRecordingValidate() => IsRecording;

        private static void StopIfRecording()
        {
            if (!IsRecording) return;
            controller.StopRecording();
            Debug.Log("[Recording] 녹화를 멈췄습니다. 파일은 video_work/clips 폴더에 저장됩니다.");
        }
    }
}
