using System;
using UnityEngine;

namespace SurvivalDrone.Meta
{
    // 가상 상점의 상품 하나. 실제 결제는 구현하지 않고, "어떤 상품에 관심을 보이는지"만 기록하는 데모용 상품이다.
    // 값은 모두 시험용 초안(노션 "상점 설계" 10절)이라 ShopSettings.asset에서 코드 수정 없이 고친다.
    [Serializable]
    public class ShopProduct
    {
        // 로그에 남길 상품 이름과 화면에 보여줄 이름 (예: "코어 대").
        public string displayName = "";

        // 상품 종류 이름 (예: "코어 패키지", "조각 패키지", "스타터 팩", "월정액"). 카드 위쪽에 작게 표시한다.
        public string category = "";

        // 구성품 설명 (예: "코어 7,800 (6,600 + 보너스 1,200)").
        public string contents = "";

        // 가상 가격(원). 실제로 결제되지 않는다. 0 이하면 "무료"로 표시한다.
        public int priceWon = 0;
    }

    // 가상 상점의 "스위치"와 상품 목록을 담은 데이터 상자 (ScriptableObject).
    // 저장 슬롯의 SaveSlotSettings, 하드 스테이지의 HardStageSettings와 같은 방식이다.
    //
    // 꺼짐(기본): 로비에 상점 버튼이 없고 상점 화면도 만들어지지 않는다. (상점이 없던 때와 똑같이 동작한다)
    // 켜짐: 로비 오른쪽 아래에 "상점" 버튼이 생기고, 누르면 아래 상품 목록이 카드로 나열된 데모 상점이 열린다.
    //       구매 버튼을 눌러도 결제는 일어나지 않고 "데모입니다" 안내 창만 뜬다. 열람·클릭·안내 창 확인은 테스트 로그에 남는다.
    //
    // 설계는 노션 "상점 설계" 페이지에 있다. 이 파일은 로비가 시작될 때 읽어야 해서 씬이 아니라 Resources 폴더에 둔다.
    // 위치: Assets/Resources/ShopSettings.asset
    [CreateAssetMenu(menuName = "SurvivalDrone/Shop Settings", fileName = "ShopSettings")]
    public class ShopSettings : ScriptableObject
    {
        private const string ResourceName = "ShopSettings";

        [Header("사용 여부 (꺼 두면 로비에 상점 버튼이 없다)")]
        [SerializeField] private bool _enabled = false;

        [Header("상품 목록 (앞에서부터 위 줄 5개, 아래 줄 5개 순서로 카드가 놓인다)")]
        [SerializeField] private ShopProduct[] _products;

        public bool Enabled => _enabled;
        public ShopProduct[] Products => _products;

        // 스위치가 켜져 있고 상품이 있으면 상품 목록을, 아니면 null을 돌려준다. 로비가 시작될 때 이 값으로 상점을 만들지 정한다.
        public static ShopProduct[] LoadEnabledProducts()
        {
            var settings = Resources.Load<ShopSettings>(ResourceName);
            if (settings == null || !settings.Enabled || settings._products == null || settings._products.Length == 0) return null;
            return settings._products;
        }
    }
}
