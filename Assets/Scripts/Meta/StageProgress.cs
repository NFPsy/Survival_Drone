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

        // 게임에 있는 스테이지 목록. 순서대로 1번, 2번, 3번 스테이지가 된다. 인스펙터에서 연결한다.
        [SerializeField] private StageData[] _stages;

        private SaveData _data;
        private bool _saveOnChange;
        private int _selectedIndex;

        // 고른 스테이지가 바뀔 때 발생 (로비 UI가 카드를 갱신하려고 구독한다).
        public event Action<int> OnSelectionChanged;

        public int StageCount => _stages != null ? _stages.Length : 0;

        // 해금된 스테이지 개수 (1 ~ StageCount).
        public int UnlockedCount => _data != null ? Mathf.Clamp(_data.unlockedStageCount, 1, Mathf.Max(1, StageCount)) : 1;

        // 지금 고른 스테이지의 위치 (0부터 시작).
        public int SelectedIndex => _selectedIndex;

        // 지금 고른 스테이지의 데이터. 스테이지 목록이 비어 있으면 null.
        public StageData CurrentStage => GetStage(_selectedIndex);

        // 지금 고른 스테이지의 적 배율. 스테이지 정보가 없으면 1(원래 밸런스 그대로)을 돌려줘서 게임이 멈추지 않게 한다.
        public float CurrentEnemyMultiplier => CurrentStage != null ? CurrentStage.EnemyMultiplier : 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize(SaveManager.Data, _stages, true);
            SaveManager.DataReplaced += HandleDataReplaced; // 저장 슬롯을 새로 고르면 새 데이터로 다시 연결한다
        }

        private void OnDestroy()
        {
            if (Instance == this) SaveManager.DataReplaced -= HandleDataReplaced;
        }

        private void HandleDataReplaced() => Rebind(SaveManager.Data);

        // 저장 슬롯을 바꿨을 때 새 저장 데이터로 다시 연결한다. (고른 스테이지는 새 데이터의 해금 상태에 맞게 다시 정해진다)
        public void Rebind(SaveData data) => Initialize(data, _stages, _saveOnChange);

        // 저장 데이터와 스테이지 목록을 받아서 준비한다. (CurrencyManager.Initialize와 같은 목적: 검증 도구에서 씬 없이 쓰기 위함)
        public void Initialize(SaveData data, StageData[] stages, bool saveOnChange)
        {
            _data = data;
            _stages = stages;
            _saveOnChange = saveOnChange;

            if (StageCount == 0)
                Debug.LogWarning("[Stage] 스테이지 목록이 비어 있습니다. StageProgress에 StageData를 연결해주세요.");

            // 저장된 해금 개수가 스테이지 수를 넘거나 0 이하면 바로잡는다.
            _data.unlockedStageCount = UnlockedCount;

            // 처음에는 해금된 스테이지 중 가장 마지막(가장 어려운 도전 가능 스테이지)을 골라 둔다.
            _selectedIndex = UnlockedCount - 1;
        }

        public StageData GetStage(int index)
        {
            if (_stages == null || index < 0 || index >= _stages.Length) return null;
            return _stages[index];
        }

        public bool IsUnlocked(int index) => index >= 0 && index < UnlockedCount;

        // 스테이지를 고른다. 해금되지 않았거나 범위 밖이면 무시하고 false를 돌려준다.
        public bool TrySelect(int index)
        {
            if (!IsUnlocked(index)) return false;
            if (index == _selectedIndex) return true;

            _selectedIndex = index;
            Debug.Log($"[Stage] 스테이지 선택: {index + 1}번 '{CurrentStage.DisplayName}' (적 배율 x{CurrentStage.EnemyMultiplier})");
            OnSelectionChanged?.Invoke(_selectedIndex);
            return true;
        }

        // 스테이지의 최고 생존 시간(초). 기록이 없으면 0.
        public float GetBestSurvivalSeconds(int index)
        {
            if (_data == null || index < 0 || index >= _data.bestSurvivalSeconds.Count) return 0f;
            return _data.bestSurvivalSeconds[index];
        }

        // 한 판이 끝났을 때 부른다. 최고 생존 시간을 갱신하고, 클리어했다면 다음 스테이지를 해금한다.
        public void RecordMatchResult(bool cleared, float survivalSeconds)
        {
            if (_data == null) return;

            int index = _selectedIndex;

            // 기록 칸이 모자라면 0으로 채워서 늘린다.
            while (_data.bestSurvivalSeconds.Count <= index) _data.bestSurvivalSeconds.Add(0f);

            bool isNewBest = survivalSeconds > _data.bestSurvivalSeconds[index];
            if (isNewBest) _data.bestSurvivalSeconds[index] = survivalSeconds;

            bool unlockedNext = false;
            if (cleared && index + 1 >= UnlockedCount && index + 1 < StageCount)
            {
                _data.unlockedStageCount = index + 2;
                unlockedNext = true;
                PlayLog.RecordUnlock(_data, index + 2);
            }

            Debug.Log($"[Stage] 스테이지 {index + 1} {(cleared ? "클리어" : "실패")}: 생존 {survivalSeconds:F1}초" +
                      $"{(isNewBest ? " (최고 기록 갱신)" : "")}{(unlockedNext ? $" → 스테이지 {index + 2} 해금" : "")}");

            if (_saveOnChange) SaveManager.Save();
        }
    }
}
