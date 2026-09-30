using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 웹 브라우저(WebGL)와 이어주는 도우미: 글을 클립보드에 복사하기, 글을 파일로 내려받기.
    // 테스트(CBT) 기록을 테스터가 개발자에게 보낼 때 쓴다.
    //
    // 웹 빌드에서는 Assets/Plugins/WebGL/BrowserBridge.jslib 의 자바스크립트 함수를 부른다.
    // 웹이 아닌 곳(유니티 에디터, PC 빌드)에서는 같은 이름의 함수가 아래처럼 대신 동작한다:
    //  - 복사: 유니티가 제공하는 클립보드 (GUIUtility.systemCopyBuffer)
    //  - 파일 저장: persistentDataPath 폴더에 텍스트 파일로 저장
    public static class BrowserBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void BrowserBridge_CopyToClipboard(string text);

        [DllImport("__Internal")]
        private static extern void BrowserBridge_DownloadTextFile(string fileName, string text);
#endif

        // 글을 클립보드에 복사한다. (웹에서는 브라우저 보안 때문에 실패할 수 있어서, 실패하면 옛 방식으로 한 번 더 시도한다)
        public static void CopyToClipboard(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BrowserBridge_CopyToClipboard(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }

        // 글을 텍스트 파일로 저장한다. 웹에서는 브라우저의 다운로드로 내려받게 되고 "브라우저 다운로드"라고 돌려준다.
        // 웹이 아닐 때는 저장한 파일의 전체 경로를 돌려준다. 저장에 실패하면 null.
        public static string DownloadTextFile(string fileName, string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BrowserBridge_DownloadTextFile(fileName, text);
            return "브라우저 다운로드";
#else
            try
            {
                string path = Path.Combine(Application.persistentDataPath, fileName);
                File.WriteAllText(path, text, new UTF8Encoding(true)); // BOM을 붙이면 메모장에서도 한글이 깨지지 않는다
                return path;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Save] 텍스트 파일 저장에 실패했습니다: {e.Message}");
                return null;
            }
#endif
        }
    }
}
