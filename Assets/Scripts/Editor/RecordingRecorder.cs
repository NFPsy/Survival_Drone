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
    //   Ctrl+F10 = 녹화 시작   Ctrl+F11 = 녹화 멈춤
    //   (F10은 Unity Recorder 패키지가 이미 "Quick Recording"으로 쓰고 있어서 겹침 창이 뜨기 때문에 Ctrl을 붙였다)
    // 결과: video_work/clips/tr_1023_092136.mp4 처럼 "tr_월일_시분초"로 저장된다.
    //   (예전에는 tr_take_001로 저장되어서, 녹화를 다시 하면 앞 파일을 덮어쓰는 문제가 있었다. 이제는 이름이 매번 다르다)
    // 녹화 중인지 눈으로 보고 싶으면 메뉴 SurvivalDrone/Recording/Show Status Window로 작은 상태 창을 연다.
    //
    // 설정: 게임 화면(Game 탭)만 1920x1080, 60프레임, MP4(H.264), 게임 소리 포함.
    //  - 에디터의 메뉴·패널은 찍히지 않고 게임 화면만 찍힌다. Game 탭이 눈에 보이는 상태여야 한다.
    //  - HUD를 숨기려면 같이 쓰는 RecordingMenu의 F7(HUD), F6(데미지 숫자)을 누른다.
    //  - 녹화 중에는 컴퓨터가 평소보다 무거워질 수 있다. 일정한 프레임 간격으로 찍기 때문에 게임이 조금 느려 보여도 영상은 부드럽다.
    [InitializeOnLoad]
    public static class RecordingRecorder
    {
        private static RecorderController controller;

        // 상태 창(RecordingStatusWindow)이 읽어 가는 값: 녹화를 시작한 시각(에디터가 켜진 뒤 흐른 초)과 마지막으로 저장한 파일 이름
        public static double StartTime { get; private set; }
        public static string CurrentFileName { get; private set; } = "";
        public static bool IsRecordingNow => controller != null && controller.IsRecording();

        static RecordingRecorder()
        {
            // 녹화 도중 플레이를 멈추면 파일이 깨질 수 있으니, 플레이가 끝나기 전에 먼저 녹화를 멈춘다.
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) StopIfRecording();
            };
        }

        private static bool IsRecording => IsRecordingNow;

        [MenuItem("SurvivalDrone/Recording/Start Recording (Play mode) %F10")]
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
            // 파일 이름은 "tr_월일_시분초"로 정한다. 확장자(.mp4)는 자동으로 붙는다.
            // (예전의 <Take> 자리표시자는 녹화를 다시 할 때마다 001부터 시작해서 앞 파일을 덮어썼다. 시각은 매번 달라서 덮어쓸 일이 없다)
            CurrentFileName = "tr_" + System.DateTime.Now.ToString("MMdd_HHmmss");
            movie.OutputFile = folder.Replace('\\', '/') + "/" + CurrentFileName;

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 60f;
            settings.CapFrameRate = true; // 60프레임 간격을 일정하게 지킨다

            controller = new RecorderController(settings);
            controller.PrepareRecording();
            controller.StartRecording();
            StartTime = EditorApplication.timeSinceStartup;
            Debug.Log("[Recording] 녹화를 시작했습니다. (Ctrl+F11로 멈춤) 저장: video_work/clips/" + CurrentFileName + ".mp4");
        }

        [MenuItem("SurvivalDrone/Recording/Start Recording (Play mode) %F10", true)]
        private static bool StartRecordingValidate() => Application.isPlaying && !IsRecording;

        [MenuItem("SurvivalDrone/Recording/Stop Recording %F11")]
        private static void StopRecording()
        {
            StopIfRecording();
        }

        [MenuItem("SurvivalDrone/Recording/Stop Recording %F11", true)]
        private static bool StopRecordingValidate() => IsRecording;

        private static void StopIfRecording()
        {
            if (!IsRecording) return;
            controller.StopRecording();
            Debug.Log("[Recording] 녹화를 멈췄습니다. 파일은 video_work/clips 폴더에 저장됩니다.");
        }
    }
}
