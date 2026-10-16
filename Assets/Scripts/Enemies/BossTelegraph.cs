using UnityEngine;

namespace SurvivalDrone.Enemies
{
    // 보스의 공격 "예고"를 바닥에 보여 주는 표시. 맞기 전에 피할 시간을 주려는 것이다.
    //  - Circle: 내려찍기 예고. 바닥에 붉은 원이 생기고, 속이 가운데에서부터 차오른다. 다 차면 터진다.
    //  - Line  : 돌진 예고. 보스 앞으로 붉은 길쭉한 띠가 생기고, 속이 보스 쪽에서부터 차오른다. 다 차면 돌진한다.
    //
    // 어떻게 만드는가: 그림 파일 없이 코드로 메쉬(면)를 만들어 붉은 반투명 재질을 입힌다.
    //  - 바닥판(옅음) + 차오르는 속(옅음, 겹치면 진해짐) + 테두리(진함) 세 겹이다.
    //  - 재질은 Assets/Resources의 BossTelegraphFill / BossArenaRing를 이름으로 불러온다.
    //    (에디터 메뉴 SurvivalDrone → Effects → Build Boss Materials로 만든다. 웹 빌드에도 빠지지 않게 Resources에 둔다)
    //
    // 이 스크립트는 "그림"만 담당한다. 언제 터지고 누가 얼마나 맞는지는 BossBrain이 정한다.
    public class BossTelegraph : MonoBehaviour
    {
        private const string FillMaterialName = "BossTelegraphFill";
        private const string EdgeMaterialName = "BossArenaRing";

        // 바닥과 겹쳐 깜빡이지 않도록 층마다 살짝 다른 높이로 띄운다.
        private const float BaseHeight = 0.05f;
        private const float GrowHeight = 0.06f;
        private const float EdgeHeight = 0.07f;

        // 같은 모양을 매번 새로 만들지 않도록 한 번 만든 기본 메쉬를 기억해 둔다.
        private static Mesh unitDisc;     // 반지름 1인 원판
        private static Mesh unitAnnulus;  // 반지름 1인 가는 고리(테두리)
        private static Mesh unitQuad;     // x -0.5~0.5, z 0~1인 직사각형(길이 방향이 z)

        private Transform grow;               // 차오르는 속
        private Renderer growRenderer;
        private bool isLine;
        private float lineLength;
        private float lineWidth;
        private float circleRadius;
        private MaterialPropertyBlock block;

        // 바닥에 원 모양 예고를 만든다. center는 바닥 위 중심, radius는 반지름(m).
        public static BossTelegraph Circle(Vector3 center, float radius)
        {
            var root = new GameObject("BossTelegraphCircle");
            root.transform.position = new Vector3(center.x, 0f, center.z);
            var telegraph = root.AddComponent<BossTelegraph>();
            telegraph.circleRadius = radius;

            var fill = LoadMaterial(FillMaterialName);
            var edge = LoadMaterial(EdgeMaterialName);

            telegraph.AddLayer("Base", GetUnitDisc(), fill, BaseHeight, new Vector3(radius, 1f, radius));
            telegraph.grow = telegraph.AddLayer("Grow", GetUnitDisc(), fill, GrowHeight, new Vector3(0.001f, 1f, 0.001f));
            telegraph.AddLayer("Edge", GetUnitAnnulus(), edge, EdgeHeight, new Vector3(radius, 1f, radius));
            telegraph.growRenderer = telegraph.grow != null ? telegraph.grow.GetComponent<Renderer>() : null;
            return telegraph;
        }

        // 바닥에 띠 모양 예고를 만든다. origin에서 direction 방향으로 length(m) 길이, width(m) 폭.
        public static BossTelegraph Line(Vector3 origin, Vector3 direction, float length, float width)
        {
            var root = new GameObject("BossTelegraphLine");
            root.transform.position = new Vector3(origin.x, 0f, origin.z);
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) root.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            var telegraph = root.AddComponent<BossTelegraph>();
            telegraph.isLine = true;
            telegraph.lineLength = length;
            telegraph.lineWidth = width;

            var fill = LoadMaterial(FillMaterialName);
            var edge = LoadMaterial(EdgeMaterialName);

            telegraph.AddLayer("Base", GetUnitQuad(), fill, BaseHeight, new Vector3(width, 1f, length));
            telegraph.grow = telegraph.AddLayer("Grow", GetUnitQuad(), fill, GrowHeight, new Vector3(width, 1f, 0.001f));
            telegraph.AddLayer("Edge", BuildLineOutline(width, length), edge, EdgeHeight, Vector3.one);
            telegraph.growRenderer = telegraph.grow != null ? telegraph.grow.GetComponent<Renderer>() : null;
            return telegraph;
        }

        // 차오른 정도(0~1)를 정한다. 0이면 비어 있고 1이면 가득 찬다.
        public void SetProgress(float progress)
        {
            if (grow == null) return;
            float t = Mathf.Clamp01(progress);
            if (isLine)
                grow.localScale = new Vector3(lineWidth, 1f, Mathf.Max(0.001f, lineLength * t));
            else
                grow.localScale = new Vector3(Mathf.Max(0.001f, circleRadius * t), 1f, Mathf.Max(0.001f, circleRadius * t));
        }

        // 터지는 순간 속을 밝고 진하게 번쩍이게 한다.
        public void Flash()
        {
            if (growRenderer == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            growRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", new Color(1f, 0.85f, 0.7f, 0.9f));
            growRenderer.SetPropertyBlock(block);
            SetProgress(1f);
        }

        // 한 층(메쉬 + 재질)을 자식으로 만든다. 재질이 없으면 층을 만들지 않고 null을 돌려준다.
        private Transform AddLayer(string layerName, Mesh mesh, Material material, float height, Vector3 scale)
        {
            if (mesh == null || material == null) return null;

            var layer = new GameObject(layerName);
            layer.transform.SetParent(transform, false);
            layer.transform.localPosition = new Vector3(0f, height, 0f);
            layer.transform.localScale = scale;
            layer.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = layer.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return layer.transform;
        }

        private static Material LoadMaterial(string materialName)
        {
            var material = Resources.Load<Material>(materialName);
            if (material == null)
                Debug.LogWarning($"[BossTelegraph] Resources에 '{materialName}' 재질이 없어 공격 예고가 보이지 않습니다. 메뉴 SurvivalDrone → Effects → Build Boss Materials를 실행하세요.");
            return material;
        }

        // ── 기본 메쉬들 ──

        // 반지름 1인 원판(부채꼴 48조각).
        private static Mesh GetUnitDisc()
        {
            if (unitDisc != null) return unitDisc;

            const int segments = 48;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                int next = (i + 1) % segments + 1;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = next;
            }
            unitDisc = new Mesh { name = "BossTelegraphDisc", vertices = vertices, triangles = triangles };
            unitDisc.RecalculateBounds();
            return unitDisc;
        }

        // 반지름 1 근처의 가는 고리(안쪽 0.94 ~ 바깥 1.0). 크기를 키워도 두께가 같은 비율로 유지된다.
        private static Mesh GetUnitAnnulus()
        {
            if (unitAnnulus != null) return unitAnnulus;

            const int segments = 64;
            const float inner = 0.94f;
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
            unitAnnulus = new Mesh { name = "BossTelegraphAnnulus", vertices = vertices, triangles = triangles };
            unitAnnulus.RecalculateBounds();
            return unitAnnulus;
        }

        // x -0.5~0.5, z 0~1인 직사각형. 길이 방향(z)과 폭 방향(x)을 스케일로 늘려 쓴다.
        private static Mesh GetUnitQuad()
        {
            if (unitQuad != null) return unitQuad;

            var vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f),
            };
            var triangles = new[] { 0, 2, 1, 1, 2, 3 };
            unitQuad = new Mesh { name = "BossTelegraphQuad", vertices = vertices, triangles = triangles };
            unitQuad.RecalculateBounds();
            return unitQuad;
        }

        // 띠의 테두리: 네 변을 굵기 0.12m의 가는 직사각형 4개로 만든다. (폭·길이가 정해지면 크기를 바꿀 수 없어서 따로 만든다)
        private static Mesh BuildLineOutline(float width, float length)
        {
            const float t = 0.12f;
            float hw = width * 0.5f;
            var rects = new[]
            {
                new Rect(-hw, 0f, t, length),              // 왼쪽 변
                new Rect(hw - t, 0f, t, length),           // 오른쪽 변
                new Rect(-hw, 0f, width, t),               // 시작 변(보스 쪽)
                new Rect(-hw, length - t, width, t),       // 끝 변
            };

            var vertices = new Vector3[rects.Length * 4];
            var triangles = new int[rects.Length * 6];
            for (int i = 0; i < rects.Length; i++)
            {
                var r = rects[i];
                int v = i * 4;
                vertices[v] = new Vector3(r.x, 0f, r.y);
                vertices[v + 1] = new Vector3(r.x + r.width, 0f, r.y);
                vertices[v + 2] = new Vector3(r.x, 0f, r.y + r.height);
                vertices[v + 3] = new Vector3(r.x + r.width, 0f, r.y + r.height);
                int tri = i * 6;
                triangles[tri] = v; triangles[tri + 1] = v + 2; triangles[tri + 2] = v + 1;
                triangles[tri + 3] = v + 1; triangles[tri + 4] = v + 2; triangles[tri + 5] = v + 3;
            }
            var mesh = new Mesh { name = "BossTelegraphLineOutline", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
