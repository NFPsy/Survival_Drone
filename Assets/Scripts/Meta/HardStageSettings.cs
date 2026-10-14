using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 하드 스테이지(스테이지 6~8)의 "스위치"와 하드 스테이지 목록을 담은 데이터 상자 (ScriptableObject).
    // 저장 슬롯의 SaveSlotSettings, 락온 뽑기의 LockOnTable.Enabled와 같은 방식이다.
    //
    // 꺼짐(기본): 하드 스테이지가 전혀 없는 것처럼 동작한다. 스테이지는 노멀 5개뿐이고 "하드" 탭도 보이지 않는다.
    //             (CBT·OBT 1단계처럼 하드 스테이지를 내보내지 않는 빌드는 꺼 둔 채로 낸다)
    // 켜짐: 노멀 5개 뒤에 아래 하드 스테이지들이 6번, 7번, 8번으로 붙고, 스테이지 선택 화면에 "노멀 / 하드" 탭이 생긴다.
    //       하드는 스테이지 5를 처음 클리어해야 열린다(기존 "앞 스테이지를 깨면 다음이 열린다" 규칙 그대로).
    //
    // 설계는 노션 "하드 스테이지 설계" 페이지에 있다. 값(권장 전투력, 체력·피해 배율, 이름)은 StageData_06~08 에셋에서 고친다.
    //
    // 이 파일은 StageProgress가 게임이 시작될 때 읽어야 해서 씬이 아니라 Resources 폴더에 둔다.
    // (Resources.Load는 씬에 연결하지 않고도 이름만으로 에셋을 불러올 수 있다) 위치: Assets/Resources/HardStageSettings.asset
    [CreateAssetMenu(menuName = "SurvivalDrone/Hard Stage Settings", fileName = "HardStageSettings")]
    public class HardStageSettings : ScriptableObject
    {
        private const string ResourceName = "HardStageSettings";

        [Header("사용 여부 (꺼 두면 하드 스테이지가 없는 것처럼 동작한다)")]
        [SerializeField] private bool _enabled = false;

        [Header("하드 스테이지 목록 (순서대로 6번, 7번, 8번 스테이지)")]
        [SerializeField] private StageData[] _stages;

        public bool Enabled => _enabled;
        public StageData[] Stages => _stages;

        // 스위치가 켜져 있으면 하드 스테이지 목록을, 꺼져 있거나 에셋이 없으면 null을 돌려준다.
        // StageProgress가 게임을 시작할 때 이 값을 받아 노멀 스테이지 뒤에 이어 붙인다.
        public static StageData[] LoadEnabledStages()
        {
            var settings = Resources.Load<HardStageSettings>(ResourceName);
            if (settings == null || !settings.Enabled || settings._stages == null || settings._stages.Length == 0) return null;
            return settings._stages;
        }
    }
}
