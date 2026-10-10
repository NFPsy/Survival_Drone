using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 저장 슬롯에서 "처음으로 판을 시작할 때" 화면 위쪽 알림 칸(HUDNotice)에 짧은 조작 안내를 띄우는 스크립트.
    //
    // 왜 필요한가:
    //  메인 메뉴의 "게임 설명"·"조작법" 버튼을 안 누르고 바로 게임을 시작한 사람은 아무 설명 없이 판에 들어온다.
    //  그래서 "이동만 하면 되고 공격은 드론이 한다", "스페이스바가 오버드라이브"를 첫 판 안에서 한 번 알려준다.
    //
    // 언제 나오는가:
    //  그 저장 슬롯에서 판을 한 번도 안 했을 때만 나온다. (끝낸 판·도중에 나간 판·시작만 한 판이 모두 0일 때)
    //  첫 판을 시작하는 순간 "판 시작" 기록이 저장에 남기 때문에, 두 번째 판부터는 자동으로 나오지 않는다.
    //  새 슬롯으로 시작하면 그 슬롯에서는 다시 나온다. 별도의 "봤음" 저장 칸은 만들지 않았다(옛 저장과의 호환 걱정이 없다).
    //
    // 어떻게 동작하는가:
    //  씬에 직접 넣어 두는 스크립트가 아니라, 게임이 켜질 때 스스로 "씬이 열릴 때마다 확인하겠다"고 등록해 둔다.
    //  InGame 씬이 열리면 그 순간(아직 판 시작 기록이 남기 전) 첫 판인지 판단하고, 첫 판이면 안내용 오브젝트를 하나 만든다.
    public class FirstMatchHint : MonoBehaviour
    {
        // 안내를 띄울 씬 이름.
        private const string InGameSceneName = "InGame";

        // 게임이 켜질 때 한 번 불려서, 씬이 열릴 때마다 OnSceneLoaded가 불리도록 등록한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // 씬이 열릴 때마다 불린다. sceneLoaded는 씬의 Awake가 끝난 뒤, 첫 Start가 불리기 "전"에 온다.
        // 그래서 GameManager.Start()가 판 시작 기록을 남기기 전에 첫 판인지 정확히 확인할 수 있다.
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != InGameSceneName) return;
            if (!IsFirstMatchOfSlot()) return;

            var go = new GameObject("FirstMatchHint");
            go.AddComponent<FirstMatchHint>();
            // 새 오브젝트가 InGame 씬에 들어가게 해서, 씬을 나갈 때 같이 사라지게 한다.
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        // 지금 불러온 저장 슬롯에서 판을 한 번도 안 했는지 확인한다.
        private static bool IsFirstMatchOfSlot()
        {
            // 로비를 거치지 않고 InGame만 단독 실행했을 때(저장 데이터 없음)는 안내하지 않는다.
            if (CurrencyManager.Instance == null) return false;

            var data = SaveManager.Data;
            if (data == null || data.logStats == null) return false;

            // 끝낸 판이나 도중에 나간 판이 하나라도 있으면 첫 판이 아니다.
            if (data.logStats.matches > 0 || data.logStats.abandons > 0) return false;

            // 시작만 하고 탭을 닫은 판("match_start" 줄만 있는 판)도 한 번 한 것으로 본다.
            foreach (string line in data.playLog)
            {
                if (line.Contains("| match_start |")) return false;
            }

            return true;
        }

        // 판이 시작되면 안내를 두 번 띄운다. WaitForSeconds는 게임 시간 기준이라
        // 레벨업 선택 화면처럼 시간이 멈춘 동안에는 같이 멈춘다.
        private IEnumerator Start()
        {
            yield return new WaitForSeconds(0.5f);
            HUDNotice.Instance?.Show("WASD로 이동하세요. 공격은 드론이 알아서 합니다.", 5f);

            yield return new WaitForSeconds(8f);
            HUDNotice.Instance?.Show("오버드라이브 게이지가 차면 스페이스바! (5초간 공격 2.5배, 받는 피해도 2배)", 6f);

            Destroy(gameObject);
        }
    }
}
