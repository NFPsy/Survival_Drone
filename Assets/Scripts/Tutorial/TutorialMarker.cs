using UnityEngine;

namespace SurvivalDrone.Tutorial
{
    // 튜토리얼 "이동" 단계에서 가야 할 자리를 바닥에 보여 주는 하늘색 원.
    // 보스 예고(붉은 원)와 같은 재질을 쓰되 색만 하늘색으로 바꿔서, "위험 표시"와 헷갈리지 않게 한다.
    // 그림 파일 없이 코드로 메쉬를 만들고, 살짝 커졌다 작아졌다 하며 눈에 띄게 한다.
    public class TutorialMarker : MonoBehaviour
    {
        // 보스 예고에 쓰는 재질(Assets/Resources). 웹 빌드에서도 빠지지 않는다.
        private const string FillMaterialName = "BossTelegraphFill";
        private const string EdgeMaterialName = "BossArenaRing";

        private static readonly Color RingColor = new Color(0.31f, 0.847f, 0.91f, 0.95f);
        private static readonly Color FillColor = new Color(0.31f, 0.847f, 0.91f, 0.22f);

        // 원의 반지름(m). 플레이어가 이 안에 들어오면 도착으로 본다.
        public float Radius { get; private set; }

        private Transform pulse;

        // center 자리에 반지름 radius짜리 표시를 만든다.
        public static TutorialMarker Create(Vector3 center, float radius)
        {
            var root = new GameObject("TutorialMarker");
            root.transform.position = new Vector3(center.x, 0f, center.z);
            var marker = root.AddComponent<TutorialMarker>();
            marker.Radius = radius;

            var fill = Resources.Load<Material>(FillMaterialName);
            var edge = Resources.Load<Material>(EdgeMaterialName);
            if (fill == null || edge == null)
                Debug.LogWarning("[Tutorial] Resources에 보스 예고 재질이 없어 이동 표시가 보이지 않습니다. 메뉴 SurvivalDrone → Effects → Build Boss Materials를 실행하세요.");

            AddLayer(root.transform, "Fill", BuildDisc(), fill, FillColor, 0.05f, radius);
            marker.pulse = AddLayer(root.transform, "Ring", BuildRing(), edge, RingColor, 0.07f, radius);
            return marker;
        }

        private void Update()
        {
            // 1초에 한 번쯤 5% 크기로 두근거린다.
            if (pulse == null) return;
            float scale = Radius * (1f + 0.05f * Mathf.Sin(Time.time * 6f));
            pulse.localScale = new Vector3(scale, 1f, scale);
        }

        private static Transform AddLayer(Transform parent, string layerName, Mesh mesh, Material material, Color color, float height, float radius)
        {
            if (material == null) return null;

            var layer = new GameObject(layerName);
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = new Vector3(0f, height, 0f);
            layer.transform.localScale = new Vector3(radius, 1f, radius);
            layer.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = layer.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            // 재질 하나를 여러 곳이 같이 쓰므로, 색은 재질을 고치지 않고 이 렌더러에만 덧씌운다.
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            meshRenderer.SetPropertyBlock(block);
            return layer.transform;
        }

        // 반지름 1인 원판
        private static Mesh BuildDisc()
        {
            const int segments = 48;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            var mesh = new Mesh { name = "TutorialMarkerDisc", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        // 반지름 1 근처의 가는 고리(안쪽 0.93 ~ 바깥 1.0)
        private static Mesh BuildRing()
        {
            const int segments = 64;
            const float inner = 0.93f;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = dir * inner;
                vertices[i * 2 + 1] = dir;
                int next = (i + 1) % segments;
                int a = i * 2, b = i * 2 + 1, c = next * 2, d = next * 2 + 1;
                int t = i * 6;
                triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = c;
                triangles[t + 3] = c; triangles[t + 4] = b; triangles[t + 5] = d;
            }
            var mesh = new Mesh { name = "TutorialMarkerRing", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
