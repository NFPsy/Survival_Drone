using System.IO;
using UnityEditor;
using UnityEngine;
using SurvivalDrone.Drones;

namespace SurvivalDrone.EditorTools
{
    // 회복 드론 이펙트(HealEffect.prefab)를 만들어 주는 에디터 도구.
    // 메뉴 SurvivalDrone → Effects → Build Heal Effect 로 실행한다. 여러 번 눌러도 같은 파일을 다시 만들 뿐이라 안전하다.
    //
    // 만드는 것:
    //  1) "+" 모양 그림(Assets/Textures/Effects/heal_plus.png): 코드로 그린다. (외부 그림 파일 없음)
    //  2) 재질 3개(Mat_HealBeam / Mat_HealPlus / Mat_HealRing): 기존 "폭발 조준 링" 재질을 본떠서(겹쳐서 밝아지는 합성 방식) 만든다.
    //  3) HealEffect.prefab: 빛줄기(원뿔) + "+" 파티클 + 발밑 링 파티클 + 이를 재생하는 HealBeamEffect 스크립트.
    //  4) HealDrone.prefab의 healVisualPrefab 칸에 위 프리팹을 연결한다.
    public static class HealEffectBuilder
    {
        private const string PlusTexturePath = "Assets/Textures/Effects/heal_plus.png";
        private const string RingTexturePath = "Assets/Textures/Effects/ring_aura.png";
        private const string TemplateMaterialPath = "Assets/Materials/Mat_ExplosionAimRing.mat";
        private const string BeamMaterialPath = "Assets/Materials/Mat_HealBeam.mat";
        private const string PlusMaterialPath = "Assets/Materials/Mat_HealPlus.mat";
        private const string RingMaterialPath = "Assets/Materials/Mat_HealRing.mat";
        private const string EffectPrefabPath = "Assets/Prefabs/Misc/HealEffect.prefab";
        private const string DronePrefabPath = "Assets/Prefabs/Drones/HealDrone.prefab";

        // 이펙트 색(밝은 초록).
        private static readonly Color Green = new Color(0.35f, 1f, 0.45f, 1f);

        [MenuItem("SurvivalDrone/Effects/Build Heal Effect")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogWarning("[HealEffect] 플레이 중이거나 컴파일 중에는 만들 수 없습니다. 멈춘 뒤 다시 실행하세요.");
                return;
            }

            var plusTexture = CreatePlusTexture();
            var ringTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RingTexturePath);

            var beamMaterial = CreateMaterial(BeamMaterialPath, null, Green);
            var plusMaterial = CreateMaterial(PlusMaterialPath, plusTexture, Green);
            var ringMaterial = CreateMaterial(RingMaterialPath, ringTexture, Green);

            var effectPrefab = BuildPrefab(beamMaterial, plusMaterial, ringMaterial);
            WireToDrone(effectPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HealEffect] 회복 이펙트를 만들고 HealDrone에 연결했습니다.");
        }

        // 64x64 크기의 둥근 "+" 모양을 코드로 그려서 PNG로 저장한다. 가장자리는 부드럽게(반투명) 처리한다.
        private static Texture2D CreatePlusTexture()
        {
            const int size = 64;
            const float center = (size - 1) * 0.5f;
            const float halfLength = 21f; // 막대 중심선의 반 길이
            const float thickness = 7f;   // 막대 반지름(둥근 끝)

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);
                    float dy = Mathf.Abs(y - center);

                    // 가로 막대와 세로 막대 중심선까지의 거리 중 가까운 쪽(둥근 끝을 위해 선분까지의 거리).
                    float horizontal = Mathf.Sqrt(Mathf.Max(dx - halfLength, 0f) * Mathf.Max(dx - halfLength, 0f) + dy * dy);
                    float vertical = Mathf.Sqrt(dx * dx + Mathf.Max(dy - halfLength, 0f) * Mathf.Max(dy - halfLength, 0f));
                    float distance = Mathf.Min(horizontal, vertical) - thickness;

                    float alpha = Mathf.Clamp01(0.5f - distance / 2f); // 경계에서 2칸 폭으로 부드럽게 사라짐
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(PlusTexturePath));
            File.WriteAllBytes(PlusTexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(PlusTexturePath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(PlusTexturePath);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(PlusTexturePath);
        }

        // 기존 폭발 조준 링 재질을 복사해서(겹쳐 밝아지는 투명 합성 설정이 그대로 따라온다) 그림과 색만 바꾼 재질을 만든다.
        private static Material CreateMaterial(string path, Texture2D texture, Color color)
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

            material.SetTexture("_BaseMap", texture); // 그림이 없으면(null) 흰색 한 장으로 취급돼 순수한 색 빛줄기가 된다
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static HealBeamEffect BuildPrefab(Material beamMaterial, Material plusMaterial, Material ringMaterial)
        {
            var root = new GameObject("HealEffect");
            var effect = root.AddComponent<HealBeamEffect>();

            // 빛줄기: 모양(메시)은 HealBeamEffect가 게임 시작 때 코드로 채운다.
            var beamObject = new GameObject("Beam");
            beamObject.transform.SetParent(root.transform, false);
            beamObject.AddComponent<MeshFilter>();
            var beamRenderer = beamObject.AddComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = beamMaterial;
            beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;

            var plus = MakePlusParticles(root.transform, plusMaterial);
            var ring = MakeRingParticles(root.transform, ringMaterial);

            var serialized = new SerializedObject(effect);
            serialized.FindProperty("beamRenderer").objectReferenceValue = beamRenderer;
            serialized.FindProperty("plusParticles").objectReferenceValue = plus;
            serialized.FindProperty("ringParticles").objectReferenceValue = ring;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(EffectPrefabPath));
            var saved = PrefabUtility.SaveAsPrefabAsset(root, EffectPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<HealBeamEffect>();
        }

        // 초록 "+" 5개가 조금씩 시차를 두고 나타나 위로 떠오르며 사라진다. 월드 기준이라 드론이 움직여도 끌려오지 않는다.
        private static ParticleSystem MakePlusParticles(Transform parent, Material material)
        {
            var go = new GameObject("Plus");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.9f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.55f);
            main.startColor = new Color(Green.r, Green.g, Green.b, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 8;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.00f, 1),
                new ParticleSystem.Burst(0.06f, 1),
                new ParticleSystem.Burst(0.12f, 1),
                new ParticleSystem.Burst(0.18f, 1),
                new ParticleSystem.Burst(0.24f, 1),
            });

            // 플레이어를 둘러싼 수평 원(반지름 0.6m)의 가장자리 근처에서 나타난다.
            // (원판 안쪽 전체에서 나오면 대부분 플레이어 몸에 가려 안 보인다. 몸 옆으로 비켜서 나오게 한다)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.6f;
            shape.radiusThickness = 0.12f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            // 위로 초속 1.3m로 떠오른다(0.9초 동안 약 1.2m).
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            // 나타날 때 빠르게 켜지고(0~15%), 오래 머물다가(~70%), 사라진다.
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeGradient(0.15f, 0.7f);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.75f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // 플레이어 발밑 바닥에서 초록 원이 지름 1m에서 3m로 퍼지며 사라진다. (바닥에 눕혀서 그린다)
        private static ParticleSystem MakeRingParticles(Transform parent, Material material)
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.7f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.7f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startColor = new Color(Green.r, Green.g, Green.b, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 2;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var shape = ps.shape;
            shape.enabled = false;

            // 처음 지름 1m가 끝에는 3m가 되게 커진다. 처음엔 빠르게, 끝에서 천천히 퍼지도록 곡선으로.
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var growth = new AnimationCurve(new Keyframe(0f, 1f, 0f, 4f), new Keyframe(1f, 3f, 0.5f, 0f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, growth);

            // 시작 투명도(0.6)에서 끝까지 서서히 사라진다.
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeGradient(0f, 0.15f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // 흰색(곱해도 색이 안 바뀜)에 투명도만 바뀌는 그라데이션: 0에서 fadeInEnd까지 켜지고, holdEnd부터 1까지 꺼진다.
        private static Gradient FadeGradient(float fadeInEnd, float holdEnd)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(fadeInEnd <= 0f ? 1f : 0f, 0f),
                    new GradientAlphaKey(1f, fadeInEnd),
                    new GradientAlphaKey(1f, holdEnd),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        // 회복 드론 프리팹에 이펙트 프리팹을 연결한다.
        private static void WireToDrone(HealBeamEffect effectPrefab)
        {
            var contents = PrefabUtility.LoadPrefabContents(DronePrefabPath);
            try
            {
                var drone = contents.GetComponent<HealDrone>();
                var serialized = new SerializedObject(drone);
                serialized.FindProperty("healVisualPrefab").objectReferenceValue = effectPrefab;
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
