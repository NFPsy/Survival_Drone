using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 재화(코어·크레딧)와 관련된 수치를 모아둔 데이터 상자 (ScriptableObject).
    // GachaTable과 같은 이유로, 수치는 코드가 아니라 이 파일(.asset) 한 곳에서만 관리한다.
    //
    // 만드는 법: 프로젝트 창에서 우클릭 → Create → SurvivalDrone → Currency Table
    // (지금은 Assets/Data/Meta/CurrencyTable.asset 에 이미 만들어져 있다)
    [CreateAssetMenu(menuName = "SurvivalDrone/Currency Table", fileName = "CurrencyTable")]
    public class CurrencyTable : ScriptableObject
    {
        // ---- 새로 시작할 때 주는 재화 ----
        // 저장 파일이 없을 때(처음 실행) 이 값으로 시작한다.
        // 코어 2,700 = 10연 뽑기 1번 가격. 처음 켜자마자 첫 10연을 해볼 수 있게 한 값이다.
        [Header("처음 시작할 때 지급")]
        [SerializeField] private int _startCore = 2700;
        [SerializeField] private int _startCredit = 0;

        // ---- 한 판이 끝났을 때의 보상 ----
        // 클리어 보상: 코어(100 / 150 / 200)와 크레딧(200 / 300 / 400) 모두 스테이지가 오를수록 늘어난다.
        // 실패 보상은 따로 숫자를 두지 않고, 클리어 보상에 "얼마나 오래 버텼는지"를 곱해서 계산한다(아래 CalculateMatchReward).
        [Header("판 보상 (클리어)")]
        // 아래 두 배열은 스테이지 1, 2, 3 순서. 배열 칸이 모자란 스테이지는 마지막 칸 값을 쓴다.
        [SerializeField] private int[] _clearCoreByStage = { 100, 150, 200 };
        [SerializeField] private int[] _clearCreditByStage = { 200, 300, 400 };

        // 실패했을 때: 이 시간(초) 미만으로 버티면 보상이 없다. 일부러 금방 죽어서 재화를 모으는 것을 막기 위한 값.
        [Header("판 보상 (실패)")]
        [SerializeField] private float _failMinSurviveSeconds = 60f;

        public int StartCore => _startCore;
        public int StartCredit => _startCredit;
        public float FailMinSurviveSeconds => _failMinSurviveSeconds;

        // 해당 스테이지(1부터 시작)를 클리어했을 때 받는 코어.
        public int GetClearCore(int stageNumber) => GetByStage(_clearCoreByStage, stageNumber);

        // 해당 스테이지(1부터 시작)를 클리어했을 때 받는 크레딧.
        public int GetClearCredit(int stageNumber) => GetByStage(_clearCreditByStage, stageNumber);

        // 스테이지 번호(1부터)에 맞는 배열 칸의 값을 돌려준다. 번호가 배열보다 크면 마지막 칸, 배열이 비었으면 0.
        private static int GetByStage(int[] values, int stageNumber)
        {
            if (values == null || values.Length == 0) return 0;
            int index = Mathf.Clamp(stageNumber - 1, 0, values.Length - 1);
            return values[index];
        }

        // 한 판의 보상을 계산한다 (재화를 실제로 주지는 않고 숫자만 돌려준다).
        //  - 클리어: 클리어 보상 전부.
        //  - 실패: 생존 시간이 _failMinSurviveSeconds(60초) 미만이면 0.
        //          그 이후에는 (생존 시간 - 60초) ÷ (판 길이 - 60초) 만큼의 비율로 클리어 보상을 받는다.
        //          예) 판 길이 360초, 210초 생존 → (210-60) ÷ (360-60) = 50% → 스테이지 1이면 코어 50, 크레딧 100.
        //          (판 길이 = 적이 나오는 시간. 그 뒤 남은 적을 잡는 시간에 죽어도 100%를 넘지 않는다.)
        public void CalculateMatchReward(bool cleared, int stageNumber, float surviveSeconds, float matchSeconds, out int core, out int credit)
        {
            int fullCore = GetClearCore(stageNumber);
            int fullCredit = GetClearCredit(stageNumber);

            if (cleared)
            {
                core = fullCore;
                credit = fullCredit;
                return;
            }

            if (surviveSeconds < _failMinSurviveSeconds)
            {
                core = 0;
                credit = 0;
                return;
            }

            float span = matchSeconds - _failMinSurviveSeconds;
            float ratio = span > 0f ? Mathf.Clamp01((surviveSeconds - _failMinSurviveSeconds) / span) : 1f;
            core = Mathf.RoundToInt(fullCore * ratio);
            credit = Mathf.RoundToInt(fullCredit * ratio);
        }
    }
}
