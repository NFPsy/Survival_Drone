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
        // 코어: 시뮬레이터 가정값(클리어 40 / 실패 15). 크레딧: 확정된 초안(클리어 200 / 실패 80).
        [Header("판 보상 (코어)")]
        [SerializeField] private int _clearCore = 40;
        [SerializeField] private int _failCore = 15;

        [Header("판 보상 (크레딧)")]
        [SerializeField] private int _clearCredit = 200;
        [SerializeField] private int _failCredit = 80;

        public int StartCore => _startCore;
        public int StartCredit => _startCredit;
        public int ClearCore => _clearCore;
        public int FailCore => _failCore;
        public int ClearCredit => _clearCredit;
        public int FailCredit => _failCredit;
    }
}
