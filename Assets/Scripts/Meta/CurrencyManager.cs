using System;
using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 재화의 종류. "부족" 알림에서 어떤 재화가 모자랐는지 알려줄 때 쓴다.
    public enum CurrencyType { Core, Credit }

    // 코어와 크레딧의 보유량을 관리하는 매니저.
    //  - 들어오고(Add), 나가고(TrySpend), 바뀔 때마다 이벤트로 UI에 알려준다.
    //  - 바뀔 때마다 SaveManager로 저장한다.
    //  - 재화가 모자라면 아무것도 차감하지 않고 "부족" 이벤트만 발생시킨다. (뽑기 규칙)
    //
    // AudioManager와 같은 방식: 메인 메뉴 씬에 한 번 놓아두면 DontDestroyOnLoad로
    // 씬이 바뀌어도 사라지지 않고, 어디서든 CurrencyManager.Instance로 접근한다.
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        // 재화 수치(시작 지급, 판 보상)가 들어있는 데이터 파일. 인스펙터에서 연결한다.
        [SerializeField] private CurrencyTable _table;

        private SaveData _data;
        private bool _saveOnChange;

        // 재화가 바뀐 직후 발생. 바뀐 뒤의 보유량을 함께 전달한다. (UI의 재화 바가 구독)
        public event Action<int> OnCoreChanged;
        public event Action<int> OnCreditChanged;

        // 재화가 모자라서 차감에 실패했을 때 발생. (종류, 필요한 양, 현재 보유량) — "코어가 부족합니다" 팝업이 구독.
        public event Action<CurrencyType, int, int> OnInsufficient;

        public int Core => _data != null ? _data.core : 0;
        public int Credit => _data != null ? _data.credit : 0;

        private void Awake()
        {
            // 이미 다른 CurrencyManager가 있으면(메인 메뉴로 돌아왔을 때 등) 새로 생긴 쪽을 없앤다.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize(SaveManager.Data, _table, true);
        }

        // 저장 데이터와 수치표를 받아서 준비한다. Awake가 부르는 것이 기본이고,
        // 검증 도구처럼 씬 없이 쓰고 싶을 때는 saveOnChange를 false로 해서 직접 부를 수 있다.
        public void Initialize(SaveData data, CurrencyTable table, bool saveOnChange)
        {
            _data = data;
            _table = table;
            _saveOnChange = saveOnChange;

            if (_table == null)
                Debug.LogWarning("[Currency] CurrencyTable이 연결되지 않았습니다. 시작 지급과 판 보상이 0으로 처리됩니다.");

            // 처음 시작하는 저장 데이터라면 시작 재화를 한 번만 지급한다.
            if (!_data.isCurrencyInitialized)
            {
                _data.core = _table != null ? _table.StartCore : 0;
                _data.credit = _table != null ? _table.StartCredit : 0;
                _data.isCurrencyInitialized = true;
                Debug.Log($"[Currency] 새 데이터 시작 지급: 코어 {_data.core}, 크레딧 {_data.credit}");
                SaveIfNeeded();
            }
        }

        public bool CanAffordCore(int amount) => Core >= amount;
        public bool CanAffordCredit(int amount) => Credit >= amount;

        public void AddCore(int amount)
        {
            if (!IsValidAmount(amount, "코어 획득")) return;
            _data.core += amount;
            Debug.Log($"[Currency] 코어 +{amount} → {_data.core}");
            OnCoreChanged?.Invoke(_data.core);
            SaveIfNeeded();
        }

        public void AddCredit(int amount)
        {
            if (!IsValidAmount(amount, "크레딧 획득")) return;
            _data.credit += amount;
            Debug.Log($"[Currency] 크레딧 +{amount} → {_data.credit}");
            OnCreditChanged?.Invoke(_data.credit);
            SaveIfNeeded();
        }

        // 코어를 amount만큼 쓴다. 모자라면 차감하지 않고 OnInsufficient만 발생시키고 false를 돌려준다.
        public bool TrySpendCore(int amount)
        {
            if (!IsValidAmount(amount, "코어 사용")) return false;
            if (_data.core < amount)
            {
                Debug.Log($"[Currency] 코어 부족: 필요 {amount}, 보유 {_data.core}");
                OnInsufficient?.Invoke(CurrencyType.Core, amount, _data.core);
                return false;
            }

            _data.core -= amount;
            Debug.Log($"[Currency] 코어 -{amount} → {_data.core}");
            OnCoreChanged?.Invoke(_data.core);
            SaveIfNeeded();
            return true;
        }

        public bool TrySpendCredit(int amount)
        {
            if (!IsValidAmount(amount, "크레딧 사용")) return false;
            if (_data.credit < amount)
            {
                Debug.Log($"[Currency] 크레딧 부족: 필요 {amount}, 보유 {_data.credit}");
                OnInsufficient?.Invoke(CurrencyType.Credit, amount, _data.credit);
                return false;
            }

            _data.credit -= amount;
            Debug.Log($"[Currency] 크레딧 -{amount} → {_data.credit}");
            OnCreditChanged?.Invoke(_data.credit);
            SaveIfNeeded();
            return true;
        }

        // 실패했을 때 이 시간(초) 미만으로 버티면 보상이 없다. 결과 화면이 "왜 보상이 없는지" 안내할 때 읽는다.
        public float FailMinSurviveSeconds => _table != null ? _table.FailMinSurviveSeconds : 0f;

        // 한 판이 끝났을 때 보상을 지급한다. 클리어면 스테이지별 클리어 보상, 실패면 버틴 시간에 비례한 보상(1분 미만이면 없음).
        // 계산 규칙은 CurrencyTable.CalculateMatchReward에 있다.
        // 지급한 양을 out으로 돌려줘서 결과 화면의 "획득 보상" 패널에 그대로 쓸 수 있다.
        public void GrantMatchReward(bool cleared, int stageNumber, float surviveSeconds, float matchSeconds, out int rewardCore, out int rewardCredit)
        {
            rewardCore = 0;
            rewardCredit = 0;

            if (_table == null)
            {
                Debug.LogWarning("[Currency] CurrencyTable이 없어 판 보상을 지급하지 못했습니다.");
                return;
            }

            _table.CalculateMatchReward(cleared, stageNumber, surviveSeconds, matchSeconds, out rewardCore, out rewardCredit);

            Debug.Log($"[Currency] 판 보상 ({(cleared ? "클리어" : "실패")}, 스테이지 {stageNumber}, 생존 {surviveSeconds:F1}초): 코어 +{rewardCore}, 크레딧 +{rewardCredit}");
            // 금액이 0이면 AddCore/AddCredit이 경고를 남기므로, 받을 것이 있을 때만 지급한다.
            if (rewardCore > 0) AddCore(rewardCore);
            if (rewardCredit > 0) AddCredit(rewardCredit);
        }

        // 0 이하의 금액은 실수(버그)이므로 무시하고 경고를 남긴다.
        private bool IsValidAmount(int amount, string action)
        {
            if (_data == null)
            {
                Debug.LogWarning($"[Currency] 아직 준비되지 않아 {action}을(를) 처리하지 못했습니다.");
                return false;
            }
            if (amount <= 0)
            {
                Debug.LogWarning($"[Currency] {action} 금액은 1 이상이어야 합니다: {amount}");
                return false;
            }
            return true;
        }

        private void SaveIfNeeded()
        {
            if (_saveOnChange) SaveManager.Save();
        }
    }
}
