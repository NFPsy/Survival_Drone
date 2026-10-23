using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 녹화 중인지 눈으로 확인하는 작은 에디터 창. 게임 빌드에는 포함되지 않는다.
    //
    // 왜 따로 창으로 만들었나(쉬운 설명): 녹화 표시(빨간 점, 시간)를 "게임 화면 안"에 띄우면 그것까지 영상에 찍혀 버린다.
    // Unity Recorder는 게임 화면(Game 탭)만 찍으므로, 에디터의 별도 창에 표시하면 영상에는 나오지 않는다.
    //
    // 사용법: 메뉴 SurvivalDrone/Recording/Show Status Window로 연다. 창을 화면 구석(게임 화면 밖)에 두면 편하다.
    //   녹화 중  = 빨간 바탕에 "● REC 00:12"
    //   대기 중  = 회색 바탕에 "대기 (Ctrl+F10으로 시작)"
    // 창은 한 번 열어 두면 플레이를 껐다 켜도 남아 있다.
    // 주의: 플레이를 시작한 뒤에 이 창을 새로 열면 포커스가 창으로 옮겨져 게임 조작이 안 될 수 있으니, 플레이 전에 열어 두자.
    public class RecordingStatusWindow : EditorWindow
    {
        [MenuItem("SurvivalDrone/Recording/Show Status Window")]
        private static void Open()
        {
            // ShowUtility: 에디터 위에 떠 있는 작은 창으로 연다.
            var window = GetWindow<RecordingStatusWindow>(true, "녹화 상태");
            window.minSize = new Vector2(240, 80);
            window.maxSize = new Vector2(240, 80);
            window.ShowUtility();
        }

        private void OnEnable()
        {
            // 에디터가 매 프레임 부르는 함수에 연결해서, 시간이 흐르는 것을 계속 다시 그린다.
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            bool recording = RecordingRecorder.IsRecordingNow;

            // 바탕색: 녹화 중이면 빨강, 아니면 회색
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height),
                recording ? new Color(0.75f, 0.1f, 0.1f) : new Color(0.22f, 0.22f, 0.22f));

            var big = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                normal = { textColor = Color.white }
            };
            var small = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = new Color(1f, 1f, 1f, 0.85f) }
            };

            if (recording)
            {
                int seconds = Mathf.FloorToInt((float)(EditorApplication.timeSinceStartup - RecordingRecorder.StartTime));
                GUI.Label(new Rect(0, 8, position.width, 36), string.Format("● REC {0:00}:{1:00}", seconds / 60, seconds % 60), big);
                GUI.Label(new Rect(0, 48, position.width, 22), RecordingRecorder.CurrentFileName + ".mp4", small);
            }
            else
            {
                GUI.Label(new Rect(0, 8, position.width, 36), "대기", big);
                GUI.Label(new Rect(0, 48, position.width, 22), "Ctrl+F10으로 녹화 시작", small);
            }
        }
    }
}
