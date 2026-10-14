using UnityEngine.SceneManagement;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // "코어 충전" 버튼에서 가상 상점으로 가는 길을 이어 주는 도우미 (OBT 2단계, ShopSettings 스위치가 켜졌을 때만 쓰인다).
    //
    // 왜 필요한가:
    //  뽑기가 코어 부족으로 막힌 바로 그 순간에 상점으로 가는 길이 있어야 "코어가 모자라서 상점에 관심이 생겼는가"를 데이터로 볼 수 있다.
    //  (설계: 노션 "상점 설계" 5절 — pull_blocked 직후 상점으로 가는 경로)
    //  그런데 뽑기 화면(Gacha 씬)과 상점(로비 씬)은 서로 다른 씬이라, "상점을 열어 달라"는 부탁을 씬을 넘어 전달해야 한다.
    //  그래서 이 클래스가 부탁(출처 이름)을 기억해 두고, 로비가 열릴 때 꺼내 가서 상점을 자동으로 열게 한다.
    public static class ShopLauncher
    {
        // 로비 씬 이름.
        private const string LobbySceneName = "Lobby";

        // 상점 화면을 처음 열 때 기록에 남길 경로 이름. 코어 충전 버튼은 이 이름들로 구분한다.
        public const string RouteLobby = "로비";
        public const string RouteTopUpGacha = "코어충전-일반뽑기";
        public const string RouteTopUpLockOn = "코어충전-락온";

        // 로비에 도착하면 상점을 자동으로 열어 달라는 부탁. 비어 있으면 부탁이 없는 것이다.
        private static string _pendingRoute;

        // 가상 상점이 켜져 있는지 (스위치와 상품 목록이 모두 있을 때). 꺼져 있으면 코어 충전 버튼을 아예 만들지 않는다.
        public static bool IsAvailable => ShopSettings.LoadEnabledProducts() != null;

        // 로비로 가서 상점을 연다. route = 어느 화면의 코어 충전 버튼이었는지 (위 상수).
        public static void GoToShop(string route)
        {
            _pendingRoute = route;
            SceneManager.LoadScene(LobbySceneName);
        }

        // 로비가 열렸을 때 부탁이 있었으면 그 경로 이름을 돌려주고 부탁을 지운다. 없으면 false.
        // (한 번 꺼내면 지워지므로 로비에 다시 들어와도 상점이 또 저절로 열리지는 않는다)
        public static bool ConsumePending(out string route)
        {
            route = _pendingRoute;
            _pendingRoute = null;
            return !string.IsNullOrEmpty(route);
        }
    }
}
