using UnityEngine;

namespace SurvivalDrone.UI
{
    // 실행 중에 코드가 골라 써야 하는 아이콘(드론 종류·재화)을 한 곳에 모아 둔 데이터 파일.
    // Resources 폴더의 "UiIconSet" 에셋이 이 클래스의 실제 내용이다. (에디터 메뉴 "Apply Icons To Scenes"가 만들고 채운다)
    //
    // 왜 따로 모으나:
    //  로비 버튼 아이콘처럼 씬에 고정된 아이콘은 씬에 직접 붙이면 되지만,
    //  "이 줄은 근접 드론이니까 근접 아이콘" 처럼 종류에 따라 바뀌는 아이콘은 코드가 이름으로 찾아야 한다.
    //  WebGL 빌드에서도 안전하게 찾으려고 Resources 폴더에 넣어 둔다.
    [CreateAssetMenu(fileName = "UiIconSet", menuName = "SurvivalDrone/UI Icon Set")]
    public class UiIconSet : ScriptableObject
    {
        public Sprite core;
        public Sprite credit;
        public Sprite shard;

        public Sprite droneMelee;
        public Sprite droneSniper;
        public Sprite droneExplosion;
        public Sprite droneHeal;
        public Sprite droneCollector;
    }
}
