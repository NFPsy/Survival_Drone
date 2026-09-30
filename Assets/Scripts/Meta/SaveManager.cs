using System.IO;
using UnityEngine;

namespace SurvivalDrone.Meta
{
    // SaveData를 파일로 저장하고 불러오는 담당.
    // 저장 위치: Application.persistentDataPath 안의 save.json
    //  - persistentDataPath는 유니티가 "게임 데이터를 안전하게 저장하라고" 정해준 폴더다 (PC마다 위치가 다름).
    //  - 에디터에서는 Window 기준 C:\Users\사용자\AppData\LocalLow\회사명\게임명\ 아래에 생긴다.
    //
    // 주의(WebGL 배포): 브라우저 게임에서는 이 폴더가 브라우저 저장소(IndexedDB)로 대체된다.
    // 파일을 쓴 뒤 브라우저 저장소로 옮기는 동기화가 자동이 아니라서, 저장할 때마다 WebGLFileSync.Flush()를 부른다.
    // (웹 빌드가 아니면 아무 일도 하지 않는다.) 새로고침·재접속 후에도 저장이 남는지는 WebGL 빌드로 확인해야 한다. (노션 코딩 규칙 14번)
    public static class SaveManager
    {
        private const string FileName = "save.json";

        private static SaveData _data;

        // 저장 파일의 전체 경로.
        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        // 현재 게임이 쓰는 저장 데이터. 처음 접근할 때 파일에서 불러오고, 그 뒤에는 같은 객체를 계속 쓴다.
        public static SaveData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        // 파일에서 불러온다. 파일이 없거나 깨져 있으면 새 데이터로 시작한다(게임이 멈추지 않게 경고만 남김).
        public static void Load()
        {
            _data = null;

            if (File.Exists(FilePath))
            {
                try
                {
                    string json = File.ReadAllText(FilePath);
                    _data = JsonUtility.FromJson<SaveData>(json);
                    if (_data != null)
                        Debug.Log($"[Save] 불러오기 완료: 코어 {_data.core}, 크레딧 {_data.credit} ({FilePath})");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Save] 저장 파일을 읽지 못해 새로 시작합니다: {e.Message}");
                }
            }

            if (_data == null)
            {
                _data = new SaveData();
                Debug.Log("[Save] 저장 파일이 없어 새 데이터로 시작합니다.");
            }
        }

        // 현재 데이터를 파일에 쓴다.
        public static void Save()
        {
            if (_data == null) return;

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(_data, true));
                WebGLFileSync.Flush();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Save] 저장에 실패했습니다: {e.Message}");
            }
        }

        // 저장 파일을 지우고 메모리의 데이터도 비운다 (개발 중 처음부터 다시 테스트할 때 사용).
        public static void DeleteSave()
        {
            _data = null;
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
                WebGLFileSync.Flush();
            }
            Debug.Log($"[Save] 저장 파일을 삭제했습니다 ({FilePath})");
        }
    }
}
