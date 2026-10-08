using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SurvivalDrone.Meta
{
    // SaveData를 파일로 저장하고 불러오는 담당.
    // 저장 위치: Application.persistentDataPath 안
    //  - persistentDataPath는 유니티가 "게임 데이터를 안전하게 저장하라고" 정해준 폴더다 (PC마다 위치가 다름).
    //  - 에디터에서는 Window 기준 C:\Users\사용자\AppData\LocalLow\회사명\게임명\ 아래에 생긴다.
    //
    // 두 가지 모드 (SaveSlotSettings의 Enabled 스위치로 정한다):
    //  1) 슬롯 꺼짐(기본): 예전처럼 save.json 하나만 쓴다. 슬롯 기능이 없던 때와 똑같이 동작한다.
    //  2) 슬롯 켜짐: 메인 메뉴에서 슬롯(1~N번)을 고르면 그 슬롯의 파일(save_slot1.json ~)을 읽고 쓴다.
    //     - 슬롯을 고르기 전에는 "임시 데이터"만 있고 파일에는 아무것도 쓰지 않는다. (매니저들이 시작할 때 데이터를 요구하기 때문에 필요하다)
    //     - 슬롯을 고르면 DataReplaced 이벤트가 발생하고, 재화·보유 드론·스테이지·뽑기 매니저가 새 데이터로 다시 연결된다.
    //     - 슬롯은 삭제할 수 없다. (개발용 DeleteSave는 에디터 메뉴에서만 쓴다)
    //     - 처음 슬롯 기능을 켠 날 save.json이 있으면 슬롯 1로 복사해서 이어서 쓴다. (원래 save.json은 지우지 않고 백업으로 남는다)
    //
    // 주의(WebGL 배포): 브라우저 게임에서는 이 폴더가 브라우저 저장소(IndexedDB)로 대체된다.
    // 파일을 쓴 뒤 브라우저 저장소로 옮기는 동기화가 자동이 아니라서, 저장할 때마다 WebGLFileSync.Flush()를 부른다.
    // (웹 빌드가 아니면 아무 일도 하지 않는다.) 새로고침·재접속 후에도 저장이 남는지는 WebGL 빌드로 확인해야 한다. (노션 코딩 규칙 14번)
    public static class SaveManager
    {
        private const string FileName = "save.json";
        private const string SlotFilePrefix = "save_slot";

        private static SaveData _data;

        // 지금 고른 슬롯 번호(1부터). 0 = 슬롯 모드가 아니거나, 슬롯 모드인데 아직 고르지 않음.
        private static int _slot;

        private static SaveSlotSettings _settings;
        private static bool _settingsLoaded;
        private static bool _warnedUnselected;

        // ---- 검증 도구 전용: 진짜 저장 파일을 건드리지 않고 임시 폴더·스위치로 검사하려고 열어 둔 값들 ----
        private static string _directoryOverride;
        private static int _enabledOverride = -1;   // -1 = 설정 파일을 따른다, 0 = 강제로 끔, 1 = 강제로 켬
        private static int _slotCountOverride;

        // 슬롯을 새로 골라서 SaveManager.Data가 다른 데이터로 바뀌었을 때 발생한다. (매니저들이 구독해서 새 데이터로 다시 연결한다)
        public static event Action DataReplaced;

        // ---------------- 슬롯 설정 ----------------

        // 슬롯 기능이 켜져 있는가? (Resources/SaveSlotSettings.asset의 Enabled. 파일이 없으면 꺼짐)
        public static bool SlotsEnabled
        {
            get
            {
                if (_enabledOverride >= 0) return _enabledOverride == 1;
                var settings = LoadSettings();
                return settings != null && settings.Enabled;
            }
        }

        // 슬롯 개수 (기본 3).
        public static int SlotCount
        {
            get
            {
                if (_slotCountOverride > 0) return _slotCountOverride;
                var settings = LoadSettings();
                return settings != null ? settings.SlotCount : 3;
            }
        }

        // 지금 고른 슬롯 번호. 슬롯 모드가 아니거나 아직 고르지 않았으면 0.
        public static int CurrentSlot => _slot;

        // 저장할 곳이 정해졌는가? (슬롯 모드가 아니면 항상 true, 슬롯 모드면 슬롯을 골랐을 때만 true)
        public static bool HasSelectedSlot => !SlotsEnabled || _slot > 0;

        private static SaveSlotSettings LoadSettings()
        {
            if (!_settingsLoaded)
            {
                _settings = Resources.Load<SaveSlotSettings>("SaveSlotSettings");
                _settingsLoaded = true;
            }
            return _settings;
        }

        private static string Directory => _directoryOverride ?? Application.persistentDataPath;

        // 슬롯 파일의 전체 경로. (예: ...\save_slot2.json)
        public static string SlotPath(int slot) => Path.Combine(Directory, $"{SlotFilePrefix}{slot}.json");

        // 지금 쓰는 저장 파일의 전체 경로. 슬롯 모드에서 슬롯을 골랐으면 그 슬롯 파일, 아니면 save.json.
        public static string FilePath => SlotsEnabled && _slot > 0 ? SlotPath(_slot) : Path.Combine(Directory, FileName);

        // ---------------- 불러오기 / 저장 ----------------

        // 현재 게임이 쓰는 저장 데이터. 처음 접근할 때 파일에서 불러오고, 그 뒤에는 같은 객체를 계속 쓴다.
        // 슬롯 모드에서 슬롯을 고르기 전에는 파일과 상관없는 임시 데이터를 돌려준다.
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

            // 슬롯 모드인데 슬롯을 아직 안 골랐다: 임시 데이터. (저장하지 않는다)
            if (SlotsEnabled && _slot == 0)
            {
                _data = new SaveData();
                return;
            }

            string path = FilePath;
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    _data = JsonUtility.FromJson<SaveData>(json);
                    if (_data != null)
                        Debug.Log($"[Save] 불러오기 완료: 코어 {_data.core}, 크레딧 {_data.credit} ({path})");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Save] 저장 파일을 읽지 못해 새로 시작합니다: {e.Message}");
                    BackUpBrokenFile(path);
                }
            }

            if (_data == null)
            {
                _data = new SaveData();
                Debug.Log("[Save] 저장 파일이 없어 새 데이터로 시작합니다.");
            }
        }

        // 현재 데이터를 파일에 쓴다. 슬롯 모드에서 슬롯을 아직 안 골랐으면 아무것도 쓰지 않는다.
        public static void Save()
        {
            if (_data == null) return;

            if (SlotsEnabled && _slot == 0)
            {
                if (!_warnedUnselected)
                {
                    _warnedUnselected = true;
                    Debug.Log("[Save] 슬롯을 아직 고르지 않아 저장하지 않습니다. (메인 메뉴에서 슬롯을 고르면 저장됩니다)");
                }
                return;
            }

            try
            {
                _data.lastSavedAt = DateTime.Now.ToString("MM-dd HH:mm");
                File.WriteAllText(FilePath, JsonUtility.ToJson(_data, true));
                WebGLFileSync.Flush();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 저장에 실패했습니다: {e.Message}");
            }
        }

        // 저장 파일을 지우고 메모리의 데이터도 비운다 (개발 중 처음부터 다시 테스트할 때 사용. 게임 화면에는 이 기능이 없다).
        public static void DeleteSave()
        {
            _data = null;
            string path = FilePath;
            if (File.Exists(path))
            {
                File.Delete(path);
                WebGLFileSync.Flush();
            }
            Debug.Log($"[Save] 저장 파일을 삭제했습니다 ({path})");
        }

        // ---------------- 슬롯 ----------------

        // 슬롯을 고른다. 파일이 있으면 불러오고, 없으면 새 데이터로 시작해서 바로 저장한다(createdNew가 true).
        // 성공하면 DataReplaced 이벤트가 발생해서 매니저들이 새 데이터로 다시 연결된다.
        // 슬롯 기능이 꺼져 있거나 번호가 범위 밖이면 아무것도 하지 않고 false.
        public static bool SelectSlot(int slot, out bool createdNew)
        {
            createdNew = false;
            if (!SlotsEnabled || slot < 1 || slot > SlotCount) return false;

            MigrateLegacyIfNeeded();
            _slot = slot;

            if (File.Exists(SlotPath(slot)))
            {
                Load();
            }
            else
            {
                // 새 슬롯: 테스터 번호는 다른 슬롯과 같은 값을 쓴다 (한 사람의 기록이라는 것을 알 수 있게).
                _data = new SaveData { testerId = FindExistingTesterId() };
                createdNew = true;
                Save();
                Debug.Log($"[Save] 슬롯 {slot}을 새로 시작합니다 ({SlotPath(slot)})");
            }

            DataReplaced?.Invoke();
            return true;
        }

        // 슬롯의 저장 내용을 "읽기만" 한다. 비어 있거나 읽을 수 없으면 null. (슬롯 선택 화면의 요약, 로그 내보내기에 쓴다)
        // 지금 고른 슬롯이면 메모리의 최신 데이터를 돌려준다.
        public static SaveData PeekSlot(int slot)
        {
            if (slot < 1 || slot > SlotCount) return null;
            if (slot == _slot && _data != null) return _data;

            MigrateLegacyIfNeeded();
            string path = SlotPath(slot);
            if (!File.Exists(path)) return null;
            try
            {
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 슬롯 {slot} 파일을 읽지 못했습니다: {e.Message}");
                return null;
            }
        }

        // 내용이 있는 모든 슬롯을 (슬롯 번호, 데이터) 목록으로 돌려준다. 로그를 한꺼번에 내보낼 때 쓴다.
        public static List<KeyValuePair<int, SaveData>> PeekAllSlots()
        {
            var result = new List<KeyValuePair<int, SaveData>>();
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                var data = PeekSlot(slot);
                if (data != null) result.Add(new KeyValuePair<int, SaveData>(slot, data));
            }
            return result;
        }

        // 슬롯 기능을 처음 켰을 때 예전 save.json이 있고 슬롯 1이 비어 있으면, save.json을 슬롯 1로 복사한다.
        // 원래 save.json은 지우지 않는다(되돌릴 수 있게 백업으로 남는다).
        private static void MigrateLegacyIfNeeded()
        {
            if (!SlotsEnabled) return;
            string legacy = Path.Combine(Directory, FileName);
            string slot1 = SlotPath(1);
            if (!File.Exists(legacy) || File.Exists(slot1)) return;

            try
            {
                File.Copy(legacy, slot1);
                WebGLFileSync.Flush();
                Debug.Log("[Save] 예전 save.json을 슬롯 1로 복사했습니다. (save.json은 백업으로 남겨 둡니다)");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 예전 저장을 슬롯 1로 옮기지 못했습니다: {e.Message}");
            }
        }

        // 이미 있는 슬롯 중 테스터 번호가 적힌 첫 값. 없으면 빈 글자(새로 만들어진다).
        private static string FindExistingTesterId()
        {
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                if (slot == _slot) continue;
                var data = PeekSlot(slot);
                if (data != null && !string.IsNullOrEmpty(data.testerId)) return data.testerId;
            }
            return "";
        }

        // 읽을 수 없는 파일을 새 데이터가 덮어쓰기 전에 ".broken" 이름으로 복사해 둔다. (데이터를 되살릴 여지를 남기려고)
        private static void BackUpBrokenFile(string path)
        {
            try
            {
                File.Copy(path, path + ".broken", true);
            }
            catch (Exception)
            {
                // 백업이 안 돼도 게임은 계속되어야 한다.
            }
        }

        // ---------------- 검증 도구 전용 ----------------

        // 진짜 저장 파일 대신 임시 폴더를 쓰고, 슬롯 스위치를 강제로 켜거나 끈다. 상태(고른 슬롯, 메모리 데이터)도 처음으로 되돌린다.
        // slotsEnabled가 null이면 설정 파일을 그대로 따른다.
        public static void SetTestOverrides(string directory, bool? slotsEnabled, int slotCount)
        {
            _directoryOverride = directory;
            _enabledOverride = slotsEnabled.HasValue ? (slotsEnabled.Value ? 1 : 0) : -1;
            _slotCountOverride = slotCount;
            ResetState();
        }

        // 검증이 끝나면 원래대로 돌린다.
        public static void ClearTestOverrides()
        {
            _directoryOverride = null;
            _enabledOverride = -1;
            _slotCountOverride = 0;
            ResetState();
        }

        private static void ResetState()
        {
            _data = null;
            _slot = 0;
            _warnedUnselected = false;
        }
    }
}
