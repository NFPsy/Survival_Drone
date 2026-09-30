using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalDrone.Meta
{
    // 테스트(CBT) 기록 중에서 "게임을 켰다"와 "어느 화면을 봤다"를 자동으로 남기는 매니저.
    // 뽑기·판 결과·강화 같은 기록은 각 시스템이 직접 남기고, 이 매니저는 화면 이동만 지켜본다.
    // (마지막으로 본 화면이 테스터가 어디서 그만뒀는지 = 이탈 지점을 알려준다)
    //
    // 다른 매니저(CurrencyManager 등)와 같은 방식: 메인 메뉴 씬에 한 번 놓아두면 DontDestroyOnLoad로 씬이 바뀌어도 유지된다.
    public class PlayLogRecorder : MonoBehaviour
    {
        public static PlayLogRecorder Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // 다른 매니저의 Awake가 모두 끝난 뒤(저장 데이터가 확실히 준비된 뒤)에 시작 기록을 남긴다.
        private void Start()
        {
            if (Instance != this) return;

            var data = SaveManager.Data;
            PlayLog.StartSession(data, Application.version, Application.platform.ToString(), $"{Screen.width}x{Screen.height}");
            PlayLog.RecordScreen(data, SceneManager.GetActiveScene().name);
            SaveManager.Save();

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this) SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        // 씬이 새로 열릴 때마다 화면 이름을 기록한다. (첫 화면은 Start에서 이미 남겼다)
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            PlayLog.RecordScreen(SaveManager.Data, scene.name);
            SaveManager.Save();
        }
    }
}
