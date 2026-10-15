using System.IO;
using UnityEditor;
using UnityEngine;
using SurvivalDrone.Drones;

namespace SurvivalDrone.EditorTools
{
    // 근접 드론 이펙트를 "붉은 베기"로 바꿔 주는 에디터 도구.
    // 메뉴 SurvivalDrone → Effects → Build Melee Effect 로 실행한다. 여러 번 눌러도 같은 결과를 다시 만들 뿐이라 안전하다.
    //
    // 하는 일 (모두 MeleeDrone.prefab 안에서):
    //  1) HitRing(맞혔을 때 퍼지는 링): 색을 파란색에서 붉은색으로 바꾼다. 모양·크기·시간·투명도 변화는 그대로.
    //  2) AttackBurst(공격마다 튀는 작은 불꽃): 파란색에서 붉은 주황으로 바꾼다. 개수·속도는 그대로.
    //  3) HitSwirl(새로 추가): 회전하는 붉은 호 3개. 맞혔을 때만 링과 같은 자리·크기로 약 0.28초 재생된다.
    //     그림(melee_swirl.png)은 코드로 그려서 저장하고, 재질 Mat_MeleeSwirl은 겹쳐 밝아지는 합성이다.
    //  4) MeleeDrone의 hitSwirl 칸에 HitSwirl을 연결한다.
    public static class MeleeEffectBuilder
    {
        private const string SwirlTexturePath = "Assets/Textures/Effects/melee_swirl.png";
        private const string TemplateMaterialPath = "Assets/Materials/Mat_ExplosionAimRing.mat";
        private const string SwirlMaterialPath = "Assets/Materials/Mat_MeleeSwirl.mat";
        private const string DronePrefabPath = "Assets/Prefabs/Drones/MeleeDrone.prefab";

        // 링: 밝은 살구빛 → 짙은 빨강으로 변한다.
        private static readonly Color RingStartColor = new Color(1f, 0.85f, 0.6f, 1f);
        private static readonly Color RingEndColor = new Color(1f, 0.15f, 0.1f, 1f);

        // 소용돌이: 붉은색, 투명도 0.7로 은은하게(맞힐 때마다 반복되므로 눈이 피곤하지 않게).
        private static readonly Color SwirlColor = new Color(1f, 0.3f, 0.2f, 0.7f);

        [MenuItem("SurvivalDrone/Effects/Build Melee Effect")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogWarning("[MeleeEffect] 플레이 중이거나 컴파일 중에는 만들 수 없습니다. 멈춘 뒤 다시 실행하세요.");
                return;
            }

            var swirlTexture = CreateSwirlTexture();
            var swirlMaterial = CreateSwirlMaterial(swirlTexture);
            UpdateDronePrefab(swirlMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MeleeEffect] 근접 드론 이펙트를 붉은 베기로 바꾸고 소용돌이를 연결했습니다.");
        }

        // 128x128 소용돌이: 가운데에서 바깥으로 휘어지는 호 3개. 바깥쪽일수록 굵고 안쪽은 가늘며, 가운데와 맨 가장자리는 투명하다.
        private static Texture2D CreateSwirlTexture()
        {
            const int size = 128;
            const float center = (size - 1) * 0.5f;
            const int arms = 3;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    // 반지름이 커질수록 각도가 밀려나게 해서 소용돌이 모양을 만든다. phase는 0~1을 반복하고, 0.5가 호의 한가운데다.
                    float phase = Mathf.Repeat(angle / (2f * Mathf.PI) * arms - r * 0.7f, 1f);
                    float width = Mathf.Lerp(0.05f, 0.16f, Mathf.Clamp01((r - 0.3f) / 0.6f));
                    float arm = Mathf.Exp(-Mathf.Pow((phase - 0.5f) / width, 2f));

                    // 안쪽(0.3 아래)과 바깥 가장자리(0.9 위)로 갈수록 투명해진다.
                    float inner = Smooth(0.3f, 0.5f, r);
                    float outer = 1f - Smooth(0.85f, 1f, r);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, arm * inner * outer));
                }
            }
            tex.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(SwirlTexturePath));
            File.WriteAllBytes(SwirlTexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SwirlTexturePath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(SwirlTexturePath);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SwirlTexturePath);
        }

        // value가 from 이하면 0, to 이상이면 1, 그 사이는 부드럽게 올라가는 값. (Unity의 Mathf.SmoothStep은 쓰는 방식이 달라서 직접 만들었다)
        private static float Smooth(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        // 기존 폭발 조준 링 재질(겹쳐 밝아지는 합성)을 복사해서 그림만 소용돌이로 바꾼 재질.
        private static Material CreateSwirlMaterial(Texture2D texture)
        {
            var template = AssetDatabase.LoadAssetAtPath<Material>(TemplateMaterialPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(SwirlMaterialPath);
            if (material == null)
            {
                material = new Material(template);
                AssetDatabase.CreateAsset(material, SwirlMaterialPath);
            }
            else
            {
                material.CopyPropertiesFromMaterial(template);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void UpdateDronePrefab(Material swirlMaterial)
        {
            var contents = PrefabUtility.LoadPrefabContents(DronePrefabPath);
            try
            {
                var drone = contents.GetComponent<MeleeDrone>();

                // 1) 링: 색 칸만 붉게 바꾸고, 투명도 변화(alpha 키)는 그대로 둔다.
                var ring = contents.transform.Find("HitRing").GetComponent<ParticleSystem>();
                var ringColor = ring.colorOverLifetime;
                var ringGradient = ringColor.color.gradient;
                ringGradient.SetKeys(
                    new[] { new GradientColorKey(RingStartColor, 0f), new GradientColorKey(RingEndColor, 1f) },
                    ringGradient.alphaKeys);
                ringColor.color = ringGradient;

                // 2) 공격마다 튀는 불꽃: 시작 색을 붉은 주황 두 색 사이의 무작위로.
                var burst = contents.transform.Find("AttackBurst").GetComponent<ParticleSystem>();
                var burstMain = burst.main;
                burstMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.3f, 1f), new Color(1f, 0.2f, 0.1f, 1f));

                // 3) 소용돌이: 이미 있으면 지우고 새로 만든다(여러 번 실행해도 하나만 남도록).
                var existing = contents.transform.Find("HitSwirl");
                if (existing != null) Object.DestroyImmediate(existing.gameObject);
                var swirl = MakeSwirl(contents.transform, swirlMaterial);

                // 4) 연결.
                var serialized = new SerializedObject(drone);
                serialized.FindProperty("hitSwirl").objectReferenceValue = swirl;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, DronePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // 바닥에 눕힌 큰 판 한 장(입자 1개)이 0.28초 동안 한 바퀴 돌며 나타났다가 사라진다.
        // 크기는 재생할 때 공격 범위에 맞춰 다시 정해진다.
        private static ParticleSystem MakeSwirl(Transform parent, Material material)
        {
            var go = new GameObject("HitSwirl");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.28f;
            main.startSpeed = 0f;
            main.startSize = 4f; // 재생할 때 공격 범위에 맞춰 다시 정해진다
            main.startRotation = 0f;
            main.startColor = SwirlColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 4;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var shape = ps.shape;
            shape.enabled = false;

            // 0.28초 동안 거의 한 바퀴(1300도/초 ≈ 364도) 돈다.
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1300f * Mathf.Deg2Rad);

            // 시작엔 작게, 끝엔 공격 범위 크기까지 커지면서 퍼져 나간다.
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1f));

            // 빠르게 나타났다가(앞 15%) 점점 투명해진다.
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard; // 바닥에 눕는다
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }
    }
}
