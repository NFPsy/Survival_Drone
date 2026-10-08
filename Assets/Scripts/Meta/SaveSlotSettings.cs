using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 저장 슬롯 기능의 "스위치"와 슬롯 개수를 담은 데이터 상자 (ScriptableObject).
    // 락온 뽑기의 LockOnTable.Enabled와 같은 방식이다.
    //
    // 켜짐(Enabled 체크): 메인 메뉴의 "게임 시작"을 누르면 슬롯 선택 화면이 열리고, 슬롯마다 따로 저장한다. (save_slot1.json ~)
    // 꺼짐: 예전처럼 save.json 하나만 쓰고 슬롯 화면도 나오지 않는다. (슬롯 기능이 없던 때와 똑같이 동작한다)
    //
    // 이 파일은 SaveManager가 게임이 시작될 때 가장 먼저 읽어야 해서 씬이 아니라 Resources 폴더에 둔다.
    // (Resources.Load는 씬에 연결하지 않고도 이름만으로 에셋을 불러올 수 있다) 위치: Assets/Resources/SaveSlotSettings.asset
    [CreateAssetMenu(menuName = "SurvivalDrone/Save Slot Settings", fileName = "SaveSlotSettings")]
    public class SaveSlotSettings : ScriptableObject
    {
        [Header("사용 여부 (꺼 두면 예전처럼 save.json 하나만 쓴다)")]
        [SerializeField] private bool _enabled = false;

        [Header("슬롯 개수")]
        [Range(1, 5)]
        [SerializeField] private int _slotCount = 3;

        public bool Enabled => _enabled;
        public int SlotCount => Mathf.Clamp(_slotCount, 1, 5);
    }
}
