using System;

namespace SurvivalDrone.Meta
{
    // 저장 파일(JSON)에 들어가는 내용을 담는 "저장용 상자".
    // [Serializable]을 붙이면 유니티의 JsonUtility가 이 클래스를 글자(JSON)로 바꾸거나 다시 되돌릴 수 있다.
    //
    // 지금은 재화(코어·크레딧)만 들어 있다.
    // 보유 드론, 천장 카운트, 스테이지 해금 같은 것은 해당 시스템(DroneInventory, GachaSystem 연결, StageProgress)을
    // 만들 때 여기에 필드를 하나씩 추가한다. 예전 저장 파일에 없는 필드는 0/기본값으로 읽히므로 그대로 이어서 쓸 수 있다.
    [Serializable]
    public class SaveData
    {
        // 저장 형식의 버전. 나중에 저장 구조가 크게 바뀌었을 때 옛 파일을 구분하기 위한 번호.
        public int saveVersion = 1;

        // 재화의 "처음 지급"을 이미 받았는지. false인 새 데이터일 때만 CurrencyTable의 시작 재화를 넣어준다.
        public bool isCurrencyInitialized;

        // 코어(하드 재화)의 현재 보유량.
        public int core;

        // 크레딧(소프트 재화)의 현재 보유량.
        public int credit;
    }
}
