using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 반복 재생해도 끊기지 않는 동작(루프 동작)을 만들어 주는 에디터 전용 도구.
    //
    // 왜 필요한가:
    //  VARCO에서 받은 달리기 동작은 "처음 자세"와 "마지막 자세"가 달라서, 반복 재생하면 한 바퀴가 끝나고
    //  다시 시작할 때 다리가 순간적으로 튄다(측정: 왼쪽 다리가 약 67도). VARCO에는 루프 옵션이 없었다.
    //
    // 어떻게 해결하는가:
    //  1) 원본 동작에서 "서로 가장 비슷한 두 자세 사이"의 한 주기(발걸음 한 쌍)만 잘라 낸다.
    //  2) 주기의 맨 앞 몇 프레임을 "한 주기 뒤의 자세"와 서서히 섞는다(크로스페이드).
    //     그러면 마지막 프레임 바로 다음 자세가 첫 프레임과 정확히 이어져서 이음새가 사라진다.
    //  3) 결과를 새 .anim 파일로 저장한다. 원본 FBX는 건드리지 않는다.
    //
    // 되돌리는 법: 컨트롤러의 해당 상태가 가리키는 동작을 원래 FBX 안의 동작으로 되돌리면 된다.
    // 게임 빌드에는 포함되지 않는다(에디터 전용).
    public static class LoopClipMaker
    {
        // 주인공 달리기 동작에 쓴 값. (측정으로 찾은 값: 시작 4프레임, 한 주기 19프레임)
        private const string PlayerRunSourceFbx = "Assets/Models/Player/Player_Run.fbx";
        private const string PlayerRunLoopPath = "Assets/Animations/Player/Player_Run_Loop.anim";
        private const string PlayerControllerPath = "Assets/Animations/Player/PlayerAnimator.controller";
        private const int PlayerRunStartFrame = 4;
        private const int PlayerRunPeriodFrames = 19;
        private const int PlayerRunBlendFrames = 6;

        [MenuItem("SurvivalDrone/Animation/Make Player Run Loop Clip")]
        private static void MakePlayerRunLoop()
        {
            AnimationClip source = FindClip(PlayerRunSourceFbx);
            if (source == null)
            {
                Debug.LogError("[LoopClip] 원본 달리기 동작을 찾지 못했습니다: " + PlayerRunSourceFbx);
                return;
            }

            AnimationClip loop = Make(source, PlayerRunStartFrame, PlayerRunPeriodFrames, PlayerRunBlendFrames, PlayerRunLoopPath);
            if (loop == null) return;

            // 컨트롤러의 "Run" 상태가 새 동작을 쓰도록 바꿔 준다.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            foreach (var state in controller.layers[0].stateMachine.states)
            {
                if (state.state.name == "Run") state.state.motion = loop;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[LoopClip] 주인공 달리기 루프 동작을 만들고 컨트롤러에 연결했습니다: " + PlayerRunLoopPath);
        }

        // FBX 안에 들어 있는 동작(미리보기용 복사본 제외)을 하나 찾는다.
        public static AnimationClip FindClip(string fbxPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                var clip = asset as AnimationClip;
                if (clip != null && !clip.name.StartsWith("__preview__")) return clip;
            }
            return null;
        }

        // 원본 동작의 [startFrame, startFrame + periodFrames) 한 주기를 루프 동작으로 만들어 outPath에 저장한다.
        // blendFrames: 주기 앞쪽 몇 프레임을 "한 주기 뒤 자세"와 섞을지.
        public static AnimationClip Make(AnimationClip source, int startFrame, int periodFrames, int blendFrames, string outPath)
        {
            float fps = source.frameRate;
            int totalFrames = Mathf.RoundToInt(source.length * fps);

            // 섞을 때 "한 주기 뒤" 프레임까지 읽으므로, 원본이 그만큼 길어야 한다.
            if (startFrame + periodFrames + blendFrames > totalFrames)
            {
                Debug.LogError("[LoopClip] 원본이 너무 짧습니다. 필요 프레임 " + (startFrame + periodFrames + blendFrames) + " / 실제 " + totalFrames);
                return null;
            }

            var bindings = AnimationUtility.GetCurveBindings(source);
            var curves = new Dictionary<EditorCurveBinding, AnimationCurve>();
            foreach (var b in bindings) curves[b] = AnimationUtility.GetEditorCurve(source, b);

            // 회전은 x,y,z,w 네 값이 한 묶음이다. 경로(뼈 이름)별로 묶어서 "구면 보간(Slerp)"으로 섞는다.
            var rotationGroups = new Dictionary<string, EditorCurveBinding[]>();
            foreach (var b in bindings)
            {
                if (b.type != typeof(Transform) || b.propertyName != "m_LocalRotation.x") continue;
                rotationGroups[b.path] = new[]
                {
                    b,
                    new EditorCurveBinding { path = b.path, type = b.type, propertyName = "m_LocalRotation.y" },
                    new EditorCurveBinding { path = b.path, type = b.type, propertyName = "m_LocalRotation.z" },
                    new EditorCurveBinding { path = b.path, type = b.type, propertyName = "m_LocalRotation.w" },
                };
            }

            // 결과 동작: 프레임 0 ~ periodFrames (마지막 키는 첫 키와 같은 값이라 반복 때 이음새가 없다).
            AnimationClip result = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
            if (result == null) result = new AnimationClip();
            else result.ClearCurves();
            result.frameRate = fps;

            var outCurves = new Dictionary<EditorCurveBinding, List<Keyframe>>();
            foreach (var b in bindings) outCurves[b] = new List<Keyframe>();

            // 회전이 아닌 값(위치, 크기)은 그냥 선형으로 섞는다.
            foreach (var b in bindings)
            {
                if (b.type == typeof(Transform) && b.propertyName.StartsWith("m_LocalRotation.")) continue;
                for (int i = 0; i <= periodFrames; i++)
                {
                    float value = BlendedValue(curves[b], i, startFrame, periodFrames, blendFrames, fps);
                    outCurves[b].Add(new Keyframe(i / fps, value));
                }
            }

            // 회전은 뼈 하나씩 네 값을 한꺼번에 계산한다.
            foreach (var group in rotationGroups)
            {
                EditorCurveBinding[] bs = group.Value;
                Quaternion previous = Quaternion.identity;
                for (int i = 0; i <= periodFrames; i++)
                {
                    // 마지막 키(i == periodFrames)는 첫 키와 같게 만든다 → 반복할 때 튀지 않는다.
                    Quaternion q = i == periodFrames
                        ? BlendedRotation(curves, bs, 0, startFrame, periodFrames, blendFrames, fps)
                        : BlendedRotation(curves, bs, i, startFrame, periodFrames, blendFrames, fps);

                    // 쿼터니언은 q와 -q가 같은 회전이라, 앞 프레임과 부호를 맞춰야 곡선이 튀지 않는다.
                    if (i > 0 && Quaternion.Dot(previous, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    previous = q;

                    outCurves[bs[0]].Add(new Keyframe(i / fps, q.x));
                    outCurves[bs[1]].Add(new Keyframe(i / fps, q.y));
                    outCurves[bs[2]].Add(new Keyframe(i / fps, q.z));
                    outCurves[bs[3]].Add(new Keyframe(i / fps, q.w));
                }
            }

            foreach (var b in bindings)
            {
                var curve = new AnimationCurve(outCurves[b].ToArray());
                // 프레임 사이를 직선으로 잇는다(프레임마다 키가 있어서 충분하다).
                for (int k = 0; k < curve.length; k++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(result, b, curve);
            }

            // 반복 재생을 켠다.
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(result);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(result, settings);
            result.name = System.IO.Path.GetFileNameWithoutExtension(outPath);

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath) == null) AssetDatabase.CreateAsset(result, outPath);
            EditorUtility.SetDirty(result);
            AssetDatabase.SaveAssets();
            return result;
        }

        // 결과 동작의 i번째 프레임에 해당하는 "섞인 시간 값"의 가중치(0이면 한 주기 뒤 자세, 1이면 원래 자세).
        // 처음(i = 0)에는 한 주기 뒤 자세 100%라서 마지막 프레임에서 자연스럽게 이어지고,
        // blendFrames 프레임에 걸쳐 원래 자세로 부드럽게 돌아온다.
        private static float OriginalWeight(int i, int blendFrames)
        {
            if (i >= blendFrames) return 1f;
            float t = (float)i / blendFrames;
            return t * t * (3f - 2f * t); // 천천히 시작하고 천천히 끝나는 곡선(smoothstep)
        }

        private static float BlendedValue(AnimationCurve curve, int i, int startFrame, int periodFrames, int blendFrames, float fps)
        {
            float original = curve.Evaluate((startFrame + i) / fps);
            if (i >= blendFrames) return original;
            float later = curve.Evaluate((startFrame + i + periodFrames) / fps);
            return Mathf.Lerp(later, original, OriginalWeight(i, blendFrames));
        }

        private static Quaternion BlendedRotation(Dictionary<EditorCurveBinding, AnimationCurve> curves, EditorCurveBinding[] bs,
            int i, int startFrame, int periodFrames, int blendFrames, float fps)
        {
            Quaternion original = ReadRotation(curves, bs, (startFrame + i) / fps);
            if (i >= blendFrames) return original;
            Quaternion later = ReadRotation(curves, bs, (startFrame + i + periodFrames) / fps);
            return Quaternion.Slerp(later, original, OriginalWeight(i, blendFrames));
        }

        private static Quaternion ReadRotation(Dictionary<EditorCurveBinding, AnimationCurve> curves, EditorCurveBinding[] bs, float time)
        {
            var q = new Quaternion(curves[bs[0]].Evaluate(time), curves[bs[1]].Evaluate(time),
                                   curves[bs[2]].Evaluate(time), curves[bs[3]].Evaluate(time));
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return length > 0.0001f ? new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length) : Quaternion.identity;
        }
    }
}
