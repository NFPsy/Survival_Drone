using System;
using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 스테이지 진행 상황(어느 스테이지가 열렸는지, 지금 어떤 스테이지를 골랐는지, 최고 기록)을 관리하는 매니저.
    // CurrencyManager와 같은 방식: 메인 메뉴 씬에 한 번 놓아두면 DontDestroyOnLoad로 씬이 바뀌어도 유지된다.
    //
    // 씬이 바뀌어도 "내가 고른 스테이지"를 기억해야 한다.
    // 로비에서 스테이지를 고르고 InGame 씬으로 넘어가면, 거기서 이 매니저의 CurrentStage를 읽어 적 배율을 적용한다.
    public class StageProgress : MonoBehaviour
    {
        public static StageProgress Instance { get; private set; }

        // 게임에 있는 (노멀) 스테이지 목록. 순서대로 1번, 2번, 3번 스테이지가 된다. 인스펙터에서 연결한다.
        [SerializeField] private StageData[] _stages;

        // 노멀 + 하드(스위치가 켜졌을 때만) 스테이지를 이어 붙인 전체 목록. 하드 스테이지는 노멀 5개 뒤(6~10번)에 붙고,
        // 하드 n번은 "노멀 n번의 어려운 버전"이다 (노멀 n을 클리어하면 하드 n이 열린다).
        // 하드가 꺼져 있으면 _stages와 똑같아서 예전과 동작이 같다.
        private StageData[] _all;
        private int _normalCount;

        private SaveData _data;
        private bool _saveOnChange;
        private int _selectedIndex;

        // 고른 스테이지가 바뀔 때 발생 (로비 UI가 카드를 갱신하려고 구독한다).
        public event Action<int> OnSelectionChanged;

        public int StageCount => _all != null ? _all.Length : 0;

        // 노멀 스테이지 개수 (하드를 뺀 것).
        public int NormalStageCount => _normalCount;

        // 하드 스테이지가 있는지 (스위치가 켜져 있고 하드 스테이지 데이터가 연결돼 있을 때). 스테이지 선택 화면의 "하드" 탭을 보일지 정하는 데 쓴다.
        public bool HasHardStages => StageCount > _normalCount;

        // 첫 하드 스테이지의 위치(0부터). 하드가 없으면 -1.
        public int FirstHardIndex => HasHardStages ? _normalCount : -1;

        // 이 위치의 스테이지가 하드 스테이지인지.
        public bool IsHardStage(int index) => index >= _normalCount && index < StageCount;

        // 해금된 "노멀" 스테이지 개수 (1 ~ 노멀 개수). 이전 노멀을 클리어하면 하나씩 늘어난다.
        // 하드 스테이지의 해금은 이 숫자와 별개다(IsUnlocked 참고: 하드 n은 노멀 n을 클리어하면 열린다).
        public int UnlockedCount => _data != null ? Mathf.Clamp(_data.unlockedStageCount, 1, Mathf.Max(1, _normalCount)) : 1;

        // 지금 고른 스테이지의 위치 (0부터 시작).
        public int SelectedIndex => _selectedIndex;

        // 지금 고른 스테이지의 데이터. 스테이지 목록이 비어 있으면 null.
        public StageData CurrentStage => GetStage(_selectedIndex);

        // 지금 고른 스테이지의 적 배율. 스테이지 정보가 없으면 1(원래 밸런스 그대로)을 돌려줘서 게임이 멈추지 않게 한다.
        public float CurrentEnemyMultiplier => CurrentStage != null ? CurrentStage.EnemyMultiplier : 1f;

        // 지금 고른 스테이지의 체력 배율 / 접촉 피해 배율. 하드 스테이지는 두 값이 다르다. 스테이지 정보가 없으면 1배.
        public float CurrentHealthMultiplier => CurrentStage != null ? CurrentStage.HealthMultiplier : 1f;
        public float CurrentDamageMultiplier => CurrentStage != null ? CurrentStage.DamageMultiplier : 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize(SaveManager.Data, _stages, true, HardStageSettings.LoadEnabledStages());
            SaveManager.DataReplaced += HandleDataReplaced; // 저장 슬롯을 새로 고르면 새 데이터로 다시 연결한다
        }

        private void OnDestroy()
        {
            if (Instance == this) SaveManager.DataReplaced -= HandleDataReplaced;
        }

        private void HandleDataReplaced() => Rebind(SaveManager.Data);

        // 저장 슬롯을 바꿨을 때 새 저장 데이터로 다시 연결한다. (고른 스테이지는 새 데이터의 해금 상태에 맞게 다시 정해진다)
        public void Rebind(SaveData data) => Initialize(data, _stages, _saveOnChange, HardStageSettings.LoadEnabledStages());

        // 저장 데이터와 스테이지 목록을 받아서 준비한다. (CurrencyManager.Initialize와 같은 목적: 검증 도구에서 씬 없이 쓰기 위함)
        // hardStages = 노멀 5개 뒤에 이어 붙일 하드 스테이지들(없으면 null). 게임에서는 HardStageSettings의 스위치가 켜졌을 때만 넘어온다.
        // (검증 도구는 스위치 상태와 상관없이 같은 결과가 나오도록 이 값을 직접 넘기거나 아예 안 넘긴다)
        public void Initialize(SaveData data, StageData[] stages, bool saveOnChange, StageData[] hardStages = null)
        {
            _data = data;
            _stages = stages;
            _saveOnChange = saveOnChange;
            _normalCount = stages != null ? stages.Length : 0;
            _all = BuildAllStages(stages, hardStages);

            if (StageCount == 0)
                Debug.LogWarning("[Stage] 스테이지 목록이 비어 있습니다. StageProgress에 StageData를 연결해주세요.");

            // 저장된 해금 개수가 스테이지 수를 넘거나 0 이하면 바로잡는다.
            _data.unlockedStageCount = UnlockedCount;

            // 처음에는 해금된 스테이지 중 가장 마지막(가장 어려운 도전 가능 스테이지)을 골라 둔다.
            _selectedIndex = UnlockedCount - 1;
        }

        // 노멀 목록 뒤에 하드 목록을 이어 붙인 배열을 만든다. 하드가 없거나 비었으면 노멀 목록 그대로.
        private static StageData[] BuildAllStages(StageData[] normal, StageData[] hard)
        {
            normal = normal ?? new StageData[0];
            if (hard == null || hard.Length == 0) return normal;

            var all = new StageData[normal.Length + hard.Length];
            normal.CopyTo(all, 0);
            hard.CopyTo(all, normal.Length);
            return all;
        }

        public StageData GetStage(int index)
        {
            if (_all == null || index < 0 || index >= _all.Length) return null;
            return _all[index];
        }

        // 이 스테이지가 열려 있는지.
        //  - 노멀: 앞 노멀을 클리어해서 해금 개수(UnlockedCount) 안에 들어와 있으면 열림.
        //  - 하드 n: 짝이 되는 노멀 n을 클리어한 적이 있으면 열림. (하드는 순서대로 열릴 필요가 없다)
        public bool IsUnlocked(int index)
        {
            if (index < 0 || index >= StageCount) return false;
            if (IsHardStage(index)) return IsNormalCleared(index - _normalCount);
            return index < UnlockedCount;
        }

        // 노멀 스테이지(0부터)를 클리어한 적이 있는지.
        //  - 클리어하면 기록 비트(normalClearedMask)가 켜진다.
        //  - 옛 저장(비트가 없음)을 위해, 스테이지 1~4는 "다음 스테이지가 열려 있다"는 사실로도 클리어를 짐작한다.
        //    (마지막 노멀은 해금 개수로 알 수 없어서, 이 업데이트 뒤에 한 번 더 클리어해야 하드 5가 열린다)
        public bool IsNormalCleared(int normalIndex)
        {
            if (_data == null || normalIndex < 0 || normalIndex >= _normalCount) return false;
            if ((_data.normalClearedMask & (1 << normalIndex)) != 0) return true;
            return normalIndex < _normalCount - 1 && _data.unlockedStageCount >= normalIndex + 2;
        }

        // 스테이지를 고른다. 해금되지 않았거나 범위 밖이면 무시하고 false를 돌려준다.
        public bool TrySelect(int index)
        {
            if (!IsUnlocked(index)) return false;
            if (index == _selectedIndex) return true;

            _selectedIndex = index;
            Debug.Log($"[Stage] 스테이지 선택: {index + 1}번 '{CurrentStage.DisplayName}' (적 체력 x{CurrentStage.HealthMultiplier}, 피해 x{CurrentStage.DamageMultiplier})");
            OnSelectionChanged?.Invoke(_selectedIndex);
            return true;
        }

        // 스테이지의 최고 생존 시간(초). 기록이 없으면 0.
        public float GetBestSurvivalSeconds(int index)
        {
            if (_data == null || index < 0 || index >= _data.bestSurvivalSeconds.Count) return 0f;
            return _data.bestSurvivalSeconds[index];
        }

        // 한 판이 끝났을 때 부른다. 최고 생존 시간을 갱신하고, 클리어했다면
        //  - 노멀: 클리어 기록을 남기고 다음 노멀을 해금한다. (하드가 있으면 짝이 되는 하드도 이때 열린다)
        //  - 하드: 최고 기록만 남긴다. (하드의 보상은 마일스톤: 3분·6분·클리어에서 만능 조각, 로비 마일스톤 창에서 받는다)
        public void RecordMatchResult(bool cleared, float survivalSeconds)
        {
            if (_data == null) return;

            int index = _selectedIndex;

            // 기록 칸이 모자라면 0으로 채워서 늘린다.
            while (_data.bestSurvivalSeconds.Count <= index) _data.bestSurvivalSeconds.Add(0f);

            bool isNewBest = survivalSeconds > _data.bestSurvivalSeconds[index];
            if (isNewBest) _data.bestSurvivalSeconds[index] = survivalSeconds;

            string extra = "";
            if (cleared && !IsHardStage(index))
            {
                bool firstTime = !IsNormalCleared(index);
                _data.normalClearedMask |= 1 << index;

                if (index + 1 >= UnlockedCount && index + 1 < _normalCount)
                {
                    _data.unlockedStageCount = index + 2;
                    extra += $" → 스테이지 {index + 2} 해금";
                    PlayLog.RecordUnlock(_data, index + 2);
                }

                // 노멀 n을 처음 클리어하면 짝이 되는 하드 n이 열린다.
                if (firstTime && HasHardStages)
                {
                    int hardStageNumber = _normalCount + index + 1;
                    extra += $" → 하드 {index + 1}(스테이지 {hardStageNumber}) 해금";
                    PlayLog.RecordUnlock(_data, hardStageNumber);
                }
            }

            Debug.Log($"[Stage] 스테이지 {index + 1} {(cleared ? "클리어" : "실패")}: 생존 {survivalSeconds:F1}초" +
                      $"{(isNewBest ? " (최고 기록 갱신)" : "")}{extra}");

            if (_saveOnChange) SaveManager.Save();
        }
    }
}
