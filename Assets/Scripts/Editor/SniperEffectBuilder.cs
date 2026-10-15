using System.IO;
using UnityEditor;
using UnityEngine;
using SurvivalDrone.Drones;

namespace SurvivalDrone.EditorTools
{
    // 저격 드론 레이저 이펙트(SniperBeam.prefab)를 만들어 주는 에디터 도구.
    // 메뉴 SurvivalDrone → Effects → Build Sniper Effect 로 실행한다. 여러 번 눌러도 같은 파일을 다시 만들 뿐이라 안전하다.
    //
    // 만드는 것:
    //  1) 재질 2개(Mat_SniperBeamCore / Mat_SniperBeamGlow): 기존 "폭발 조준 링" 재질을 본떠서(겹쳐서 밝아지는 합성 방식) 만든다.
    //  2) SniperBeam.prefab: 빔 심(Core) + 빔 빛(Glow) + 맞은 자리 불꽃(Sparks) + 이를 재생하는 SniperBeamEffect 스크립트.
    //  3) SniperDrone.prefab의 beamPrefab 칸에 위 프리팹을 연결한다.
    public static class SniperEffectBuilder
    {
        private const string TemplateMaterialPath = "Assets/Materials/Mat_ExplosionAimRing.mat";
        private const string SparkMaterialPath = "Assets/Materials/Mat_Particle_Spark.mat";
        private const string CoreMaterialPath = "Assets/Materials/Mat_SniperBeamCore.mat";
        private const string GlowMaterialPath = "Assets/Materials/Mat_SniperBeamGlow.mat";
        private const string EffectPrefabPath = "Assets/Prefabs/Misc/SniperBeam.prefab";
        private const string DronePrefabPath = "Assets/Prefabs/Drones/SniperDrone.prefab";

        private static readonly Color CoreColor = new Color(1f, 0.95f, 0.7f, 1f);
        private static readonly Color GlowColor = new Color(1f, 0.3f, 0.12f, 1f);

        [MenuItem("SurvivalDrone/Effects/Build Sniper Effect")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogWarning("[SniperEffect] 플레이 중이거나 컴파일 중에는 만들 수 없습니다. 멈춘 뒤 다시 실행하세요.");
                return;
            }

            var coreMaterial = CreateMaterial(CoreMaterialPath, CoreColor);
            var glowMaterial = CreateMaterial(GlowMaterialPath, GlowColor);
            var sparkMaterial = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);

            var effectPrefab = BuildPrefab(coreMaterial, glowMaterial, sparkMaterial);
            WireToDrone(effectPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SniperEffect] 저격 레이저 이펙트를 만들고 SniperDrone에 연결했습니다.");
        }

        // 기존 폭발 조준 링 재질을 복사해서(겹쳐 밝아지는 투명 합성 설정이 그대로 따라온다) 그림은 없애고 색만 바꾼 재질을 만든다.
        private static Material CreateMaterial(string path, Color color)
        {
            var template = AssetDatabase.LoadAssetAtPath<Material>(TemplateMaterialPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(template);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.CopyPropertiesFromMaterial(template);
            }

            material.SetTexture("_BaseMap", null); // 그림 없이 순수한 색 빔
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static SniperBeamEffect BuildPrefab(Material coreMaterial, Material glowMaterial, Material sparkMaterial)
        {
            var root = new GameObject("SniperBeam");
            var effect = root.AddComponent<SniperBeamEffect>();

            var glow = MakeBeamPart(root.transform, "Glow", glowMaterial);
            var core = MakeBeamPart(root.transform, "Core", coreMaterial); // 심을 나중에 그려 빛 위에 올린다
            var sparks = MakeSparks(root.transform, sparkMaterial);

            var serialized = new SerializedObject(effect);
            serialized.FindProperty("coreRenderer").objectReferenceValue = core;
            serialized.FindProperty("glowRenderer").objectReferenceValue = glow;
            serialized.FindProperty("sparks").objectReferenceValue = sparks;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(EffectPrefabPath));
            var saved = PrefabUtility.SaveAsPrefabAsset(root, EffectPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<SniperBeamEffect>();
        }

        // 빔 한 겹: 모양(관)은 SniperBeamEffect가 게임 시작 때 코드로 채운다.
        private static MeshRenderer MakeBeamPart(Transform parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        // 맞은 자리에서 주황 불꽃 10개가 사방으로 튀며 0.25초 안에 사라진다. 속도 방향으로 길쭉하게 늘어난 모양.
        private static ParticleSystem MakeSparks(Transform parent, Material material)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            main.startColor = new Color(1f, 0.55f, 0.2f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.gravityModifier = 0f;
            main.maxParticles = 16;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            // 아주 작은 구에서 바깥 방향으로 퍼진다.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            // 밝은 노랑 주황에서 붉은색으로 변하며 투명해진다.
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.5f;
            renderer.velocityScale = 0.05f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // 저격 드론 프리팹에 이펙트 프리팹을 연결한다.
        private static void WireToDrone(SniperBeamEffect effectPrefab)
        {
            var contents = PrefabUtility.LoadPrefabContents(DronePrefabPath);
            try
            {
                var drone = contents.GetComponent<SniperDrone>();
                var serialized = new SerializedObject(drone);
                serialized.FindProperty("beamPrefab").objectReferenceValue = effectPrefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, DronePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
