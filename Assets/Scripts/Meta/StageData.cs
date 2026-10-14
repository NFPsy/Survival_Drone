using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 스테이지 하나의 정보를 담은 데이터 상자 (ScriptableObject). 적 종류 데이터(EnemyDefinition)와 같은 방식이다.
    // 스테이지마다 "적이 얼마나 단단하고 아픈가"가 달라져서, 뽑기로 강해진 만큼만 넘을 수 있는 벽이 된다.
    //
    // 만드는 법: 프로젝트 창에서 우클릭 → Create → SurvivalDrone → Stage Data
    // 에셋 이름은 StageData_01, StageData_02 처럼 "종류_번호"로 짓는다. (노션 코딩 규칙)
    [CreateAssetMenu(menuName = "SurvivalDrone/Stage Data", fileName = "StageData_01")]
    public class StageData : ScriptableObject
    {
        // 화면에 "STAGE 1"처럼 표시할 번호 (1부터 시작).
        [SerializeField] private int _stageNumber = 1;

        // 스테이지 이름 (예: "외곽 순찰로").
        [SerializeField] private string _displayName = "Stage";

        // 이 스테이지를 도전하기에 알맞은 전투력. 내 전투력과 비교해서 초록/주황/빨강으로 표시할 때 기준이 된다.
        [SerializeField] private int _recommendedPower = 100;

        // 적의 체력과 접촉 피해량에 곱하는 배율. (×1.0이면 원래 밸런스 그대로)
        // 주의: 스폰 속도와 최대 마릿수에는 곱하지 않는다. 적이 줄면 XP 구슬도 줄어 레벨업이 오히려 느려지기 때문이다(9/8 교훈).
        [SerializeField] private float _enemyMultiplier = 1f;

        // ---- 하드 스테이지용 (OBT 2단계) ----
        // 체력 배율과 접촉 피해 배율을 따로 정하고 싶을 때 쓴다. 0이면 위의 _enemyMultiplier를 쓴다(기존 스테이지 1~5는 0이라 그대로).
        // 체력은 화력이 모자라면 피할 방법이 없는 벽이고, 접촉 피해는 컨트롤로 피할 수 있는 벽이라서 하드 스테이지는 둘을 따로 둔다.
        [SerializeField] private float _healthMultiplier = 0f;
        [SerializeField] private float _damageMultiplier = 0f;

        // 하드 스테이지인지. 스테이지 선택 화면의 "하드" 탭과 카드의 HARD 표시에 쓴다.
        [SerializeField] private bool _isHard = false;

        public int StageNumber => _stageNumber;
        public string DisplayName => _displayName;
        public int RecommendedPower => _recommendedPower;
        public float EnemyMultiplier => _enemyMultiplier;

        // 적 체력에 곱할 배율 / 접촉 피해에 곱할 배율. 따로 정하지 않았으면(0) 기존 한 값(_enemyMultiplier)을 둘 다에 쓴다.
        public float HealthMultiplier => _healthMultiplier > 0f ? _healthMultiplier : _enemyMultiplier;
        public float DamageMultiplier => _damageMultiplier > 0f ? _damageMultiplier : _enemyMultiplier;
        public bool IsHard => _isHard;
    }
}
