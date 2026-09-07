using UnityEditor;
using UnityEditor.SceneManagement;

namespace SurvivalDrone.EditorTools
{
    // 유니티 에디터 전용 스크립트 (게임을 실제로 빌드했을 때는 포함되지 않는다).
    //
    // 문제 상황: 실제로 빌드한 게임은 Build Settings에 등록된 순서(0번 = MainMenu)대로
    // 항상 메인메뉴부터 시작한다. 하지만 에디터 안에서 Play 버튼을 누르면, 지금 "열려 있는" 씬이
    // 무엇이든 상관없이 그 씬부터 바로 시작해버린다. 그래서 개발 중에 SampleScene(실제 게임 플레이 씬)을
    // 열어놓고 밸런스를 만지다가 Play를 누르면 메인메뉴를 건너뛰고 바로 게임이 시작되는 문제가 있었다.
    //
    // 해결: EditorSceneManager.playModeStartScene에 메인메뉴 씬을 지정해두면,
    // 에디터에서 어떤 씬을 열어놓고 있든 Play를 누를 때마다 항상 메인메뉴부터 시작하게 만들 수 있다.
    //
    // [InitializeOnLoad]는 "에디터가 켜질 때 / 스크립트가 다시 컴파일될 때마다 자동으로 실행"되게 해주는 속성이다.
    // 이 설정 자체는 에디터를 껐다 켜면 초기화되는 값이라서, 매번 자동으로 다시 지정해주기 위해 필요하다.
    [InitializeOnLoad]
    public static class PlayModeStartSceneSetup
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        static PlayModeStartSceneSetup()
        {
            var mainMenuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);
            EditorSceneManager.playModeStartScene = mainMenuScene;
        }
    }
}
