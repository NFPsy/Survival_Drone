using System.IO;
using UnityEditor;
using UnityEngine;
using SurvivalDrone.Drones;

namespace SurvivalDrone.EditorTools
{
    // 폭발 드론 추가 이펙트(ExplosionAfter.prefab: 연기 + 검은 파편 + 불씨)를 만들어 주는 에디터 도구.
    // 메뉴 SurvivalDrone → Effects → Build Explosion Effect 로 실행한다. 여러 번 눌러도 같은 파일을 다시 만들 뿐이라 안전하다.
    //
    // 만드는 것:
    //  1) 그림 2장(코드로 그림): explosion_smoke.png(부드러운 연기 덩어리), explosion_debris.png(각진 파편 조각)
    //  2) 재질 2개(Mat_ExplosionSmoke / Mat_ExplosionDebris): 일반 반투명 합성. 어두운 연기가 밝아지지 않고 진하게 보이게 하려는 것이다.
    //     (불씨는 기존 불꽃 재질 Mat_Particle_Spark를 그대로 쓴다.)
    //  3) ExplosionAfter.prefab: 연기·파편·불씨 파티클 + 이를 재생하는 ExplosionAfterEffect 스크립트
    //  4) ExplosionDrone.prefab의 afterEffectPrefab 칸에 위 프리팹을 연결한다.
    public static class ExplosionEffectBuilder
    {
        private const string SmokeTexturePath = "Assets/Textures/Effects/explosion_smoke.png";
        private const string DebrisTexturePath = "Assets/Textures/Effects/explosion_debris.png";
        private const string TemplateMaterialPath = "Assets/Materials/Mat_ExplosionAimRing.mat";
        private const string SparkMaterialPath = "Assets/Materials/Mat_Particle_Spark.mat";
        private const string SmokeMaterialPath = "Assets/Materials/Mat_ExplosionSmoke.mat";
        private const string DebrisMaterialPath = "Assets/Materials/Mat_ExplosionDebris.mat";
        private const string EffectPrefabPath = "Assets/Prefabs/Misc/ExplosionAfter.prefab";
        private const string DronePrefabPath = "Assets/Prefabs/Drones/ExplosionDrone.prefab";

        [MenuItem("SurvivalDrone/Effects/Build Explosion Effect")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogWarning("[ExplosionEffect] 플레이 중이거나 컴파일 중에는 만들 수 없습니다. 멈춘 뒤 다시 실행하세요.");
                return;
            }

            var smokeTexture = CreateSmokeTexture();
            var debrisTexture = CreateDebrisTexture();
            var smokeMaterial = CreateAlphaMaterial(SmokeMaterialPath, smokeTexture);
            var debrisMaterial = CreateAlphaMaterial(DebrisMaterialPath, debrisTexture);
            var sparkMaterial = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);

            var effectPrefab = BuildPrefab(smokeMaterial, debrisMaterial, sparkMaterial);
            WireToDrone(effectPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ExplosionEffect] 폭발 추가 이펙트를 만들고 ExplosionDrone에 연결했습니다.");
        }

        // 64x64 부드러운 연기 덩어리: 가운데가 진하고 가장자리로 갈수록 투명해지며, 약간 울퉁불퉁한 모양.
        private static Texture2D CreateSmokeTexture()
        {
            const int size = 64;
            const float center = (size - 1) * 0.5f;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float angle = Mathf.Atan2(dy, dx);

                    // 가장자리가 각도에 따라 조금씩 들쭉날쭉하도록 반지름을 살짝 흔든다.
                    float wobble = 1f + 0.12f * Mathf.Sin(angle * 3f + 1f) + 0.08f * Mathf.Sin(angle * 5f + 2.3f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / wobble;

                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return SaveTexture(tex, SmokeTexturePath);
        }

        // 32x32 각진 파편: 꼭짓점 5개짜리 불규칙한 다각형을 흰색으로 채운다. (색은 파티클 색으로 어둡게 칠한다)
        private static Texture2D CreateDebrisTexture()
        {
            const int size = 32;
            var polygon = new[]
            {
                new Vector2(5f, 11f), new Vector2(17f, 3f), new Vector2(28f, 12f), new Vector2(23f, 27f), new Vector2(8f, 24f),
            };

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = PointInPolygon(new Vector2(x, y), polygon);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, inside ? 1f : 0f));
                }
            }
            tex.Apply();
            return SaveTexture(tex, DebrisTexturePath);
        }

        // 점이 다각형 안에 있는지: 점에서 오른쪽으로 쏜 반직선이 변을 홀수 번 지나면 안쪽이다.
        private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                bool crosses = (polygon[i].y > point.y) != (polygon[j].y > point.y)
                               && point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private static Texture2D SaveTexture(Texture2D tex, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // 기존 폭발 조준 링 재질(겹쳐 밝아지는 합성)을 복사한 뒤 "일반 반투명 합성"(뒤가 비쳐 보이는 보통 투명)으로 바꾼다.
        // 연기와 파편은 어두운 색이라 밝아지는 합성으로는 안 보이기 때문이다.
        private static Material CreateAlphaMaterial(string path, Texture2D texture)
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

            material.SetFloat("_Blend", 0f);          // 0 = 일반 투명(Alpha), 2 = 겹쳐 밝아지기(Additive)
            material.SetFloat("_SrcBlend", 5f);       // 소스 투명도
            material.SetFloat("_DstBlend", 10f);      // 1 - 소스 투명도
            material.SetFloat("_SrcBlendAlpha", 1f);
            material.SetFloat("_DstBlendAlpha", 10f);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ExplosionAfterEffect BuildPrefab(Material smokeMaterial, Material debrisMaterial, Material sparkMaterial)
        {
            var root = new GameObject("ExplosionAfter");
            var effect = root.AddComponent<ExplosionAfterEffect>();

            var smoke = MakeSmoke(root.transform, smokeMaterial);
            var debris = MakeDebris(root.transform, debrisMaterial);
            var embers = MakeEmbers(root.transform, sparkMaterial);

            var serialized = new SerializedObject(effect);
            serialized.FindProperty("smoke").objectReferenceValue = smoke;
            serialized.FindProperty("debris").objectReferenceValue = debris;
            serialized.FindProperty("embers").objectReferenceValue = embers;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(EffectPrefabPath));
            var saved = PrefabUtility.SaveAsPrefabAsset(root, EffectPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<ExplosionAfterEffect>();
        }

        // 공통 설정: 한 번만 재생, 월드 기준, 처음엔 멈춰 둠.
        private static ParticleSystem NewParticleSystem(Transform parent, string name, float duration)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = duration;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            return ps;
        }

        private static void SetupRenderer(ParticleSystem ps, Material material)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // 회색 연기 덩어리 5개가 (어두운 바닥에서도 보이도록 너무 짙게 하지 않았다) 천천히 위로 올라가며 커지고 사라진다. 크기와 퍼지는 범위는 재생할 때 폭발 반경에 맞춘다.
        private static ParticleSystem MakeSmoke(Transform parent, Material material)
        {
            var ps = NewParticleSystem(parent, "Smoke", 1f);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 1.8f); // 재생할 때 폭발 반경에 맞춰 다시 정해진다
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.42f, 0.4f, 0.38f, 0.55f);
            main.maxParticles = 8;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });

            // 수평 원판 안에서 나타난다(반지름은 재생할 때 정해진다).
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.5f;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            // 초속 0.8m로 천천히 오른다.
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.8f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            // 폭발 불꽃이 먼저 보이도록 처음 10%에 걸쳐 서서히 나타나고, 40%까지 머문 뒤 사라진다.
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = AlphaGradient(0.1f, 0.4f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 1.6f));

            SetupRenderer(ps, material);
            return ps;
        }

        // 어두운 각진 파편 9개가 위쪽 반구 방향으로 날아가다 중력으로 떨어지며 빙글 돈다.
        private static ParticleSystem MakeDebris(Transform parent, Material material)
        {
            var ps = NewParticleSystem(parent, "Debris", 0.6f);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.15f, 0.14f, 0.14f, 1f);
            main.gravityModifier = 1.5f;
            main.maxParticles = 12;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 9) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 반구가 위쪽을 향하게

            // 날아가는 동안 빙글 돈다(초당 -360도~360도, 단위는 라디안).
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-Mathf.PI * 2f, Mathf.PI * 2f);

            // 앞 70%는 선명하게, 뒤 30%에 걸쳐 사라진다.
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = AlphaGradient(0f, 0.7f);

            SetupRenderer(ps, material);
            return ps;
        }

        // 작은 주황 불씨 8개가 위로 떠오르며 사라진다. 기존 불꽃 재질(겹쳐 밝아짐)을 쓴다.
        private static ParticleSystem MakeEmbers(Transform parent, Material material)
        {
            var ps = NewParticleSystem(parent, "Embers", 0.7f);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.1f);
            main.startColor = new Color(1f, 0.6f, 0.2f, 1f);
            main.gravityModifier = -0.15f; // 아주 약하게 위로 뜬다
            main.maxParticles = 12;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

            // 수평 원판에서 위쪽으로 퍼진다(반지름은 재생할 때 정해진다).
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.2f;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = AlphaGradient(0f, 0.5f);

            SetupRenderer(ps, material);
            return ps;
        }

        // 흰색(곱해도 색이 안 바뀜)에 투명도만 바뀌는 그라데이션: fadeInEnd까지 켜지고, holdEnd부터 1까지 꺼진다.
        private static Gradient AlphaGradient(float fadeInEnd, float holdEnd)
        {
            var gradient = new Gradient();
            var alphaKeys = fadeInEnd <= 0f
                ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdEnd), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeInEnd), new GradientAlphaKey(1f, holdEnd), new GradientAlphaKey(0f, 1f) };
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys);
            return gradient;
        }

        // 폭발 드론 프리팹에 이펙트 프리팹을 연결한다.
        private static void WireToDrone(ExplosionAfterEffect effectPrefab)
        {
            var contents = PrefabUtility.LoadPrefabContents(DronePrefabPath);
            try
            {
                var drone = contents.GetComponent<ExplosionDrone>();
                var serialized = new SerializedObject(drone);
                serialized.FindProperty("afterEffectPrefab").objectReferenceValue = effectPrefab;
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
