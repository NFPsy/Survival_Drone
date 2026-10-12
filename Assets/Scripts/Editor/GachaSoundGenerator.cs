using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 뽑기 연출에 쓸 효과음 4개를 "코드로 합성해서" WAV 파일로 만들어 주는 에디터 도구.
    // 기존 효과음(짧은 모노 클립)과 같은 방식이라 외부 파일을 내려받지 않고, 라이선스 걱정이 없다.
    // 소리를 바꾸고 싶으면 아래 합성 함수의 음높이·길이·세기만 고쳐서 다시 만들면 된다.
    //
    // 만드는 소리:
    //  SFX_GachaN      : N 공개 - 낮고 짧은 "톡"
    //  SFX_GachaR      : R 공개 - 맑은 종소리 한 번
    //  SFX_GachaSR     : SR 공개 - 두 음이 올라가는 차임
    //  SFX_GachaSSR    : SSR 공개 - 네 음이 올라가는 아르페지오 + 반짝이는 꼬리
    //
    // 소리가 만들어지는 원리(초보자용 설명):
    //  소리는 1초에 44,100번 "공기가 얼마나 눌렸는가"를 적은 숫자 목록이다. 이 목록을 코드로 직접 계산해서 만든다.
    //  - 사인파(sin): 가장 깨끗한 "삐—" 소리. 주파수(Hz)가 높을수록 높은 음이다. (예: 440Hz = 라)
    //  - 종소리: 기본 음 위에 "배음"(기본 음의 2배, 2.76배... 주파수의 소리)을 여러 겹 쌓고, 시간이 지나며 점점 작아지게 한다.
    //
    // 메뉴: SurvivalDrone/Audio/Generate Gacha Sounds  /  Apply Gacha Sounds To Scene
    public static class GachaSoundGenerator
    {
        private const int SampleRate = 44100;
        private const string Folder = "Assets/Audio/SFX";

        [MenuItem("SurvivalDrone/Audio/Generate Gacha Sounds")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Folder);
            Write("SFX_GachaN", NormalReveal());
            Write("SFX_GachaR", RareReveal());
            Write("SFX_GachaSR", SuperRareReveal());
            Write("SFX_GachaSSR", UltraRareReveal());
            AssetDatabase.Refresh();
            Debug.Log("[GachaSoundGenerator] 뽑기 효과음 4개를 " + Folder + "에 만들었습니다.");
        }

        // ───────────────────────── 합성 ─────────────────────────

        // N 공개: 낮고 짧은 "톡". (0.2초)
        private static float[] NormalReveal()
        {
            float length = 0.2f;
            var buffer = new float[(int)(length * SampleRate)];
            var rng = new System.Random(3);
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i / (float)SampleRate;
                float body = Mathf.Sin(2f * Mathf.PI * 190f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * 380f * t);
                float click = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t / 0.002f) * 0.5f; // 맨 앞의 "딱" 하는 소리
                buffer[i] = body * Mathf.Exp(-t / 0.045f) + click;
            }
            return Normalize(buffer, 0.75f);
        }

        // R 공개: 맑은 종소리 한 번. (0.8초)
        private static float[] RareReveal()
        {
            var buffer = new float[(int)(0.8f * SampleRate)];
            AddBell(buffer, 0f, 659.25f, 0.55f, 1f); // 미(E5)
            return Normalize(buffer, 0.8f);
        }

        // SR 공개: 라(A5) → 높은 미(E6) 두 음이 올라가는 차임. (1.1초)
        private static float[] SuperRareReveal()
        {
            var buffer = new float[(int)(1.1f * SampleRate)];
            AddBell(buffer, 0f, 880f, 0.5f, 0.8f);
            AddBell(buffer, 0.14f, 1318.5f, 0.75f, 1f);
            return Normalize(buffer, 0.85f);
        }

        // SSR 공개: 도·미·솔·높은 도 네 음이 차례로 올라가고 마지막에 화음이 길게 울리며 반짝이는 소리가 흩어진다. (2.0초)
        private static float[] UltraRareReveal()
        {
            var buffer = new float[(int)(2.0f * SampleRate)];
            AddBell(buffer, 0.00f, 523.25f, 0.8f, 0.7f);   // 도(C5)
            AddBell(buffer, 0.11f, 659.25f, 0.8f, 0.75f);  // 미(E5)
            AddBell(buffer, 0.22f, 783.99f, 0.9f, 0.85f);  // 솔(G5)
            AddBell(buffer, 0.33f, 1046.5f, 1.4f, 1.0f);   // 높은 도(C6)
            AddBell(buffer, 0.33f, 1318.5f, 1.4f, 0.55f);  // 높은 미(E6)로 마지막 화음
            AddBell(buffer, 0.33f, 1568f, 1.3f, 0.4f);     // 높은 솔(G6)

            // 반짝이는 소리: 아주 높고 짧은 삑 소리를 여섯 번 흩뿌린다.
            var rng = new System.Random(11);
            for (int k = 0; k < 6; k++)
            {
                float start = 0.4f + (float)rng.NextDouble() * 1.1f;
                float freq = 3000f + (float)rng.NextDouble() * 3000f;
                AddTone(buffer, start, freq, 0.05f, 0.07f);
            }
            return Normalize(buffer, 0.9f);
        }

        // 종소리 한 번을 buffer의 startSeconds 위치에 더한다.
        // 기본 음(freq) 위에 종소리 특유의 배음(2배, 2.76배, 5.4배)을 쌓고, 높은 배음일수록 빨리 사라지게 한다.
        private static void AddBell(float[] buffer, float startSeconds, float freq, float duration, float amplitude)
        {
            float[] ratios = { 1f, 2f, 2.76f, 5.4f };
            float[] gains = { 1f, 0.4f, 0.22f, 0.1f };
            float[] decays = { 1f, 0.6f, 0.4f, 0.22f }; // 기본 음 길이에 대한 비율

            int start = (int)(startSeconds * SampleRate);
            int count = Mathf.Min(buffer.Length - start, (int)(duration * 2f * SampleRate));
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float sample = 0f;
                for (int p = 0; p < ratios.Length; p++)
                    sample += gains[p] * Mathf.Sin(2f * Mathf.PI * freq * ratios[p] * t) * Mathf.Exp(-t / (duration * 0.35f * decays[p]));
                float attack = Mathf.Clamp01(t / 0.003f); // 시작 순간 "딱" 하는 잡음이 생기지 않게 아주 짧게 키운다
                buffer[start + i] += sample * attack * amplitude;
            }
        }

        // 짧고 높은 단음 하나를 더한다. (반짝임용)
        private static void AddTone(float[] buffer, float startSeconds, float freq, float duration, float amplitude)
        {
            int start = (int)(startSeconds * SampleRate);
            int count = Mathf.Min(buffer.Length - start, (int)(duration * SampleRate));
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Sin(Mathf.PI * (t / duration)); // 부드럽게 커졌다 작아지는 모양
                buffer[start + i] += Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * amplitude;
            }
        }

        // 가장 큰 소리가 peak(0~1) 크기가 되도록 전체를 같은 비율로 키우거나 줄이고, 끝을 부드럽게 줄여서 "딱" 소리를 막는다.
        private static float[] Normalize(float[] buffer, float peak)
        {
            float max = 0f;
            foreach (float v in buffer) max = Mathf.Max(max, Mathf.Abs(v));
            float scale = max > 0.0001f ? peak / max : 1f;

            int fade = Mathf.Min(buffer.Length, (int)(0.01f * SampleRate));
            for (int i = 0; i < buffer.Length; i++)
            {
                float v = buffer[i] * scale;
                int fromEnd = buffer.Length - 1 - i;
                if (fromEnd < fade) v *= fromEnd / (float)fade;
                buffer[i] = v;
            }
            return buffer;
        }

        // ───────────────────────── WAV 파일 쓰기 ─────────────────────────

        // 16비트 모노 WAV 파일로 저장한다. (기존 효과음과 같은 형식)
        private static void Write(string name, float[] samples)
        {
            string path = $"{Folder}/{name}.wav";
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                int dataSize = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);                  // fmt 블록 크기
                writer.Write((short)1);            // PCM
                writer.Write((short)1);            // 모노
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);      // 초당 바이트 수
                writer.Write((short)2);            // 한 샘플의 바이트 수
                writer.Write((short)16);           // 비트 수
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                foreach (float s in samples)
                    writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
            }
        }

        // ───────────────────────── 씬에 연결 ─────────────────────────

        // 만든 소리를 Gacha 씬의 GachaResultUI 칸에 연결한다. (여러 번 실행해도 안전)
        [MenuItem("SurvivalDrone/Audio/Apply Gacha Sounds To Scene")]
        public static void ApplyToScene()
        {
            if (EditorApplication.isPlaying) return;
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Gacha.unity", OpenSceneMode.Additive);
            int assigned = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var comp in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (comp == null || comp.GetType().Name != "GachaResultUI") continue;
                    var so = new SerializedObject(comp);
                    assigned += Assign(so, "revealSoundN", "SFX_GachaN");
                    assigned += Assign(so, "revealSoundR", "SFX_GachaR");
                    assigned += Assign(so, "revealSoundSR", "SFX_GachaSR");
                    assigned += Assign(so, "revealSoundSSR", "SFX_GachaSSR");
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(comp);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[GachaSoundGenerator] Gacha 씬에 소리 {assigned}개를 연결했습니다.");
        }

        private static int Assign(SerializedObject so, string property, string clipName)
        {
            var prop = so.FindProperty(property);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Folder}/{clipName}.wav");
            if (prop == null || clip == null)
            {
                Debug.LogWarning($"[GachaSoundGenerator] 연결 실패: {property} ← {clipName}");
                return 0;
            }
            prop.objectReferenceValue = clip;
            return 1;
        }
    }
}
