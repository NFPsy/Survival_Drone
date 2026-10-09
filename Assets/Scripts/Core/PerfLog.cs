using UnityEngine;

namespace SurvivalDrone.Core
{
    // 한 판 동안 "화면이 얼마나 부드러웠는지"를 모아서, 판이 끝날 때 테스트(CBT) 기록에 한 덩어리로 남기는 도구.
    // 웹(WebGL)에서 "에디터에서는 부드러웠는데 웹에서는 끊긴다"는 말을 들어도 숫자가 없으면 원인을 찾기 어려워서 만들었다.
    // 프레임 하나가 걸린 시간(초)을 매 프레임 넣어 두고, 판이 끝날 때 아래 네 가지로 요약한다.
    //   - 평균 FPS            : 초당 평균 몇 장을 그렸는지
    //   - 30fps 미만 프레임 비율: 한 장에 33ms 넘게 걸린 프레임이 전체의 몇 %인지 (꾸준히 느린지 보는 값)
    //   - 100ms 넘는 끊김 횟수 : 한 장에 0.1초 넘게 걸린 횟수 (가끔 뚝 멈추는 "끊김"을 세는 값. 메모리 정리나 메모리 늘리기 때문에 생긴다)
    //   - 최악 프레임         : 가장 오래 걸린 한 장이 몇 ms인지
    // DamageSourceLog·LevelUpPickLog와 같은 방식: static이라 씬을 다시 불러도 값이 남으므로 판이 시작될 때 Reset으로 비운다.
    public static class PerfLog
    {
        // 이 시간(초)보다 오래 걸린 프레임을 "느린 프레임"으로 센다. (1/30초 = 30fps 미만)
        private const float SlowFrameSeconds = 1f / 30f;

        // 이 시간(초)보다 오래 걸린 프레임을 "끊김"으로 센다.
        private const float HitchSeconds = 0.1f;

        // 요약에 쓸 만큼 프레임이 모이지 않았으면(너무 짧은 판) 요약하지 않는다.
        private const int MinFrames = 30;

        private static int frames;
        private static int slowFrames;
        private static int hitches;
        private static float totalSeconds;
        private static float worstSeconds;

        // 새 판이 시작될 때 지난 판의 값을 비운다.
        public static void Reset()
        {
            frames = 0;
            slowFrames = 0;
            hitches = 0;
            totalSeconds = 0f;
            worstSeconds = 0f;
        }

        // 프레임 하나가 걸린 시간(초)을 기록한다. 0 이하는 무시한다.
        public static void Record(float frameSeconds)
        {
            if (frameSeconds <= 0f) return;

            frames++;
            totalSeconds += frameSeconds;
            if (frameSeconds > worstSeconds) worstSeconds = frameSeconds;
            if (frameSeconds > SlowFrameSeconds) slowFrames++;
            if (frameSeconds > HitchSeconds) hitches++;
        }

        // 판 기록 끝에 덧붙일 한 덩어리. 프레임이 거의 없으면 빈 글자(기록에 아무것도 덧붙지 않음).
        // 예) "성능=평균58fps 30fps미만=4% 100ms넘는끊김=2회 최악=180ms"
        public static string BuildSummary()
        {
            if (frames < MinFrames || totalSeconds <= 0f) return "";

            float averageFps = frames / totalSeconds;
            float slowPercent = slowFrames * 100f / frames;
            return $"성능=평균{averageFps:F0}fps 30fps미만={slowPercent:F0}% 100ms넘는끊김={hitches}회 최악={worstSeconds * 1000f:F0}ms";
        }
    }
}
