using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SurvivalDrone.Core
{
    // F2 키를 누르면 화면 왼쪽 위에 FPS(초당 프레임 수)와 가장 느린 프레임을 보여 주는 개발용 표시.
    // "웹에서 화면이 부드럽지 않다"처럼 느낌으로만 말하던 것을 숫자로 보려고 만들었다. 평소(테스터)에게는 보이지 않는다.
    //
    // 웹(WebGL) 빌드에서는 "무엇이 느리게 만드는지"를 빌드를 다시 하지 않고 바로 찾을 수 있도록, 표시가 켜져 있을 때
    // 아래 키로 화면 효과를 하나씩 껐다 켜 볼 수 있다. 끄고 FPS가 확 오르는 것이 범인이다.
    //   숫자 1 = 그림자  /  2 = 후처리 전체  /  3 = 블룸(빛 번짐)만  /  4 = 렌더 크기(1.0 → 0.8 → 0.6 → 0.5)  /  5 = HDR
    // (처음에는 F3~F8을 썼는데, 크롬이 F3(찾기)·F6(주소창)·F7(캐럿 브라우징)을 가로채서 숫자 키로 바꿨다. 숫자 키는 이 게임에서 쓰지 않는다)
    //   숫자 6 = 화면 UI(체력바·글자 등)  /  7 = 맵 아래 배경(BelowWorld·ArenaRim)  /  8 = 바닥 장식(GroundDecor)  ← 전체화면에서만 느린 원인을 찾으려고 추가
    // 에디터에서는 이 효과 설정 에셋을 실행 중에 바꾸면 에셋 파일이 그대로 바뀌어 버리므로, 웹 빌드에서만 동작한다.
    //
    // 씬에 따로 놓지 않아도 게임이 시작되면 스스로 하나 만들어지고(RuntimeInitializeOnLoadMethod), 씬을 옮겨도 사라지지 않는다.
    // 글자는 OnGUI(유니티 기본 즉석 화면 글자)로 그린다. 기본 글꼴을 쓰므로 영어·숫자만 보여 준다.
    public class FpsOverlay : MonoBehaviour
    {
        // 게임이 시작되면(첫 씬이 열린 뒤) 표시 오브젝트를 하나 만든다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("FpsOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<FpsOverlay>();
        }

        // 숫자를 새로 계산해서 글자를 바꾸는 간격(초). 매 프레임 글자를 새로 만들면 그것만으로 메모리 쓰레기가 생기기 때문이다.
        private const float RefreshSeconds = 0.5f;

        // "최악 프레임"이 최근 몇 초 동안의 값인지.
        private const float WorstWindowSeconds = 5f;

        // 숫자 4를 누를 때 순서대로 돌아가는 렌더 크기. (1 = 화면 크기 그대로, 작을수록 흐려지지만 가볍다)
        private static readonly float[] RenderScales = { 1f, 0.8f, 0.6f, 0.5f };

        private bool visible;
        private float accumulatedSeconds;
        private int accumulatedFrames;
        private float windowSeconds;
        private float worstSeconds;
        private int hitchCount;
        private string text = "";
        private GUIStyle style;

        // 화면 효과를 껐다 켠 상태. (처음에는 모두 켜져 있다 = 게임 원래 모습)
        private bool shadowsOn = false;
        private bool postOn = true;
        private bool bloomOn = true;
        private bool hdrOn = true;
        // 지금 렌더 크기의 순번. 웹 품질 설정(Mobile_RPAsset)의 기본값이 0.5라서 처음부터 0.5(= 3번)로 표시한다.
        // (그림자도 웹 설정에서 이미 꺼 두었다. 그래서 1번 키(그림자)는 웹에서 켜도 그림자가 나오지 않는다)
        private int scaleIndex = 3;
        private bool uiOn = true;
        private bool belowWorldOn = true;
        private bool decorOn = true;
        private string effectsLine = "";

        private void Update()
        {
            // F2: 보이기/숨기기. (브라우저가 가로채지 않는 키다)
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame) visible = !visible;

            if (visible) HandleEffectKeys();

            // 일시정지(Time.timeScale = 0) 중에도 흐르는 시간으로 센다.
            float dt = Time.unscaledDeltaTime;
            accumulatedSeconds += dt;
            accumulatedFrames++;

            windowSeconds += dt;
            if (dt > worstSeconds) worstSeconds = dt;
            if (dt > 0.1f) hitchCount++;
            if (windowSeconds >= WorstWindowSeconds)
            {
                windowSeconds = 0f;
                worstSeconds = dt;
            }

            if (accumulatedSeconds < RefreshSeconds) return;

            float fps = accumulatedFrames / accumulatedSeconds;
            float ms = accumulatedSeconds / accumulatedFrames * 1000f;
            accumulatedSeconds = 0f;
            accumulatedFrames = 0;

            // 보이지 않을 때는 글자를 만들지 않는다.
            if (!visible) return;

            text = $"FPS {fps:F0}  ({ms:F1} ms)\n" +
                   $"Worst(5s) {worstSeconds * 1000f:F0} ms   Hitches(>100ms) {hitchCount}\n" +
                   $"Quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}  {Screen.width}x{Screen.height}   [F2] hide\n" +
                   effectsLine;
        }

        // 표시가 켜져 있을 때만 숫자 1~5로 화면 효과를 하나씩 껐다 켠다. (웹 빌드에서만 실제로 바뀐다)
        private void HandleEffectKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool changed = false;
            if (keyboard.digit1Key.wasPressedThisFrame) { shadowsOn = !shadowsOn; ApplyShadows(); changed = true; }
            if (keyboard.digit2Key.wasPressedThisFrame) { postOn = !postOn; ApplyPostProcessing(); changed = true; }
            if (keyboard.digit3Key.wasPressedThisFrame) { bloomOn = !bloomOn; ApplyBloom(); changed = true; }
            if (keyboard.digit4Key.wasPressedThisFrame) { scaleIndex = (scaleIndex + 1) % RenderScales.Length; ApplyRenderScale(); changed = true; }
            if (keyboard.digit5Key.wasPressedThisFrame) { hdrOn = !hdrOn; ApplyHdr(); changed = true; }
            if (keyboard.digit6Key.wasPressedThisFrame) { uiOn = !uiOn; ApplyUi(); changed = true; }
            if (keyboard.digit7Key.wasPressedThisFrame) { belowWorldOn = !belowWorldOn; ApplySceneObjects("BelowWorld", "ArenaRim", belowWorldOn); changed = true; }
            if (keyboard.digit8Key.wasPressedThisFrame) { decorOn = !decorOn; ApplySceneObjects("GroundDecor", null, decorOn); changed = true; }

            if (changed || effectsLine.Length == 0)
            {
                effectsLine = $"[1] Shadow {OnOff(shadowsOn)}  [2] Post {OnOff(postOn)}  [3] Bloom {OnOff(bloomOn)}  " +
                              $"[4] Scale {RenderScales[scaleIndex]:F1}  [5] HDR {OnOff(hdrOn)}\n" +
                              $"[6] UI {OnOff(uiOn)}  [7] BelowWorld {OnOff(belowWorldOn)}  [8] Decor {OnOff(decorOn)}";
            }
        }

        private static string OnOff(bool on) => on ? "ON" : "off";

        // ---- 화면 효과 켜고 끄기 (에디터에서는 에셋이 바뀌지 않도록 웹 빌드에서만 실행) ----

        // 햇빛(방향광)의 그림자. 원래 그림자 종류를 처음 끌 때 기억해 두었다가, 다시 켜면 그 종류로 되돌린다.
#if UNITY_WEBGL && !UNITY_EDITOR
        private LightShadows originalShadows = LightShadows.Soft;
        private bool shadowsRemembered;
#endif

        private void ApplyShadows()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                if (!shadowsRemembered && light.shadows != LightShadows.None)
                {
                    originalShadows = light.shadows;
                    shadowsRemembered = true;
                }
                light.shadows = shadowsOn ? originalShadows : LightShadows.None;
            }
#endif
        }

        // 후처리 전체(블룸·비네트·톤매핑 같은 화면 보정)를 카메라에서 끈다.
        private void ApplyPostProcessing()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var cam = Camera.main;
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = postOn;
#endif
        }

        // 후처리 중 블룸(밝은 곳이 번지는 효과)만 끈다. 장면의 Volume 프로필 복사본을 바꾸므로 원본 에셋은 그대로다.
        private void ApplyBloom()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            foreach (var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (volume.profile != null && volume.profile.TryGet(out Bloom bloom)) bloom.active = bloomOn;
            }
#endif
        }

        // 렌더 크기(화면보다 작게 그린 뒤 늘리기). 작을수록 흐려지지만 그리는 픽셀이 줄어 가벼워진다.
        private void ApplyRenderScale()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null) asset.renderScale = RenderScales[scaleIndex];
#endif
        }

        // HDR(밝기를 넓은 범위로 계산하는 방식). 끄면 화면 버퍼가 가벼워진다.
        private void ApplyHdr()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null) asset.supportsHDR = hdrOn;
#endif
        }

        // 화면 UI(체력바·타이머·글자 등)를 통째로 숨기거나 보이게 한다. UI가 전체화면에서 느린 원인인지 보려는 것이다.
        private void ApplyUi()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.isRootCanvas) canvas.enabled = uiOn;
            }
#endif
        }

        // 이름으로 찾은 씬 오브젝트(최대 두 개)를 켜거나 끈다. 한 번 끄면 이름으로 다시 찾을 수 없으므로 찾은 것을 기억해 둔다.
        private readonly System.Collections.Generic.Dictionary<string, GameObject> foundObjects = new System.Collections.Generic.Dictionary<string, GameObject>();

        private void ApplySceneObjects(string firstName, string secondName, bool on)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ToggleByName(firstName, on);
            if (secondName != null) ToggleByName(secondName, on);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void ToggleByName(string objectName, bool on)
        {
            if (!foundObjects.TryGetValue(objectName, out var go) || go == null)
            {
                go = GameObject.Find(objectName);
                if (go == null) return;
                foundObjects[objectName] = go;
            }
            go.SetActive(on);
        }
#endif

        private void OnGUI()
        {
            if (!visible) return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            }

            // 어두운 배경 위에 그려야 어떤 화면에서도 읽힌다.
            var box = new Rect(8f, 8f, 760f, 136f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(box.x + 8f, box.y + 4f, box.width - 16f, box.height - 8f), text, style);
        }
    }
}
