using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 웹(WebGL) 빌드에서 "파일로 저장한 내용"을 브라우저 저장소(IndexedDB)에 실제로 옮겨 적게 하는 도우미.
    //
    // 왜 필요한가:
    //  웹 게임에는 진짜 하드디스크가 없어서, save.json 같은 파일은 먼저 메모리 안의 가짜 파일 시스템에 써진다.
    //  그 내용이 브라우저 저장소로 옮겨져야 새로고침이나 재접속 후에도 남는데, 이 "옮기기(동기화)"는 자동이 아니다.
    //  옮기기 전에 탭을 닫으면 방금 한 저장이 사라진다. 그래서 파일을 쓸 때마다 이 함수로 옮기기를 시작시킨다.
    //
    // 유니티 에디터나 PC 빌드에서는 아무 일도 하지 않는다 (웹 빌드에서만 동작).
    //
    // 참고: 유니티가 이 함수(JS_FileSystem_Sync)를 "나중에 없앨 예정"이라고 브라우저 콘솔에 한 번 경고한다.
    //  지금 쓰는 유니티(6000.3)에서는 정상 동작한다. 나중에 없어지면 웹 페이지 설정(WebGL 템플릿)에
    //  config.autoSyncPersistentDataPath = true; 를 넣는 방식으로 바꾸면 된다.
    public static class WebGLFileSync
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // 유니티 웹 플레이어 안에 이미 들어있는 함수를 그대로 가져다 쓴다 ("__Internal" = 플레이어 내부).
        [DllImport("__Internal")]
        private static extern void JS_FileSystem_Sync();
#endif

        // 파일을 쓴 직후에 부른다. 웹 빌드가 아니면 아무 일도 하지 않는다.
        public static void Flush()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                JS_FileSystem_Sync();
            }
            catch (Exception e)
            {
                // 동기화에 실패해도 게임은 계속되어야 하므로 경고만 남긴다.
                Debug.LogWarning($"[Save] 브라우저 저장소 동기화에 실패했습니다: {e.Message}");
            }
#endif
        }
    }
}
