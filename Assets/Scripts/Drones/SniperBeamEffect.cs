using UnityEngine;

namespace SurvivalDrone.Drones
{
    // 저격 드론이 쏠 때 보여 주는 레이저 이펙트: 가는 밝은 빔이 드론에서 적까지 쭉 뻗고 + 맞은 자리에서 불꽃이 튄다.
    // (CBT 3번 테스터의 "공격 이펙트" 의견과 사용자가 가져온 참고 그림 4번을 바탕으로 만들었다. 피해·사거리 같은 게임 수치는 그대로이고 연출만 바뀐다.)
    //
    // 구성 (SniperBeam.prefab, SniperEffectBuilder 메뉴가 만들어 준다):
    //  - Core: 흰빛 노랑의 가는 심(굵기 0.05m). 레이저의 "뜨거운 중심".
    //  - Glow: 붉은 주황의 부드러운 빛(굵기 0.24m, 반투명). 심 둘레로 번지는 빛.
    //  - Sparks: 맞은 자리에서 사방으로 튀는 주황 불꽃 10개.
    //  빔 모양(속이 빈 가는 관)은 이 스크립트가 코드로 만든다. 별도 FBX 파일은 없다.
    //
    // 시간(약 0.22초): 0~0.04초 드론에서 적까지 쭉 뻗고, 0.08초까지 유지, 0.08~0.22초에 가늘어지며 사라진다.
    // 이펙트는 드론의 자식이 아니라 따로(최상위에) 만든다. (드론이 0.5배로 줄어 있어서 자식이면 같이 작아진다)
    public class SniperBeamEffect : MonoBehaviour
    {
        [SerializeField] private MeshRenderer coreRenderer;
        [SerializeField] private MeshRenderer glowRenderer;
        [SerializeField] private ParticleSystem sparks;

        // 심과 빛의 색. 겹쳐서 밝아지는(additive) 재질이라 어두운 바닥에서도 잘 보인다.
        [SerializeField] private Color coreColor = new Color(1f, 0.95f, 0.7f, 1f);
        [SerializeField] private Color glowColor = new Color(1f, 0.3f, 0.12f, 1f);

        // 가장 진할 때의 투명도(심은 거의 불투명, 빛은 반투명).
        [SerializeField] private float coreMaxAlpha = 1f;
        [SerializeField] private float glowMaxAlpha = 0.5f;

        // 빔 굵기(반지름, 미터). 심 0.025 → 굵기 0.05m, 빛 0.12 → 굵기 0.24m.
        [SerializeField] private float coreRadius = 0.025f;
        [SerializeField] private float glowRadius = 0.12f;

        // 시간(초): 뻗는 시간, 유지 시간, 사라지는 시간. 합 = 0.22초.
        [SerializeField] private float extendTime = 0.04f;
        [SerializeField] private float holdTime = 0.04f;
        [SerializeField] private float fadeTime = 0.14f;

        // 사라지는 동안 빔이 가늘어지는 정도(끝에서 몇 배 굵기가 되는지).
        [SerializeField] private float endWidth = 0.3f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock coreBlock;
        private MaterialPropertyBlock glowBlock;
        private Transform source;
        private Vector3 lastStart;
        private Vector3 endPoint;
        private float time = -1f;

        private float TotalSeconds => extendTime + holdTime + fadeTime;

        private void Awake()
        {
            var mesh = BuildTubeMesh();
            SetMesh(coreRenderer, mesh);
            SetMesh(glowRenderer, mesh);
            SetRenderersEnabled(false);
            coreBlock = new MaterialPropertyBlock();
            glowBlock = new MaterialPropertyBlock();
        }

        private static void SetMesh(MeshRenderer renderer, Mesh mesh)
        {
            if (renderer == null) return;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == null) filter.sharedMesh = mesh;
        }

        // from(저격 드론 위치)에서 end(맞는 지점, 월드 좌표)까지 한 번 쏜다.
        // 맞은 적이 곧바로 사라져도 되도록 끝 지점은 위치(좌표)로 받는다.
        public void Fire(Transform from, Vector3 end)
        {
            if (from == null) return;

            source = from;
            lastStart = from.position;
            endPoint = end;
            time = 0f;

            SetRenderersEnabled(true);
            Apply(0f);

            if (sparks != null)
            {
                sparks.transform.position = end;
                sparks.Play();
            }
        }

        private void Update()
        {
            if (time < 0f) return;

            time += Time.deltaTime;
            if (time >= TotalSeconds)
            {
                Stop();
                return;
            }

            if (source != null) lastStart = source.position;
            Apply(time);
        }

        // 검증·미리보기용: 시간 t초 지난 순간의 모습으로 한 번에 맞춘다. (Fire를 한 번 부른 뒤에 쓴다. 에디터 화면은 실시간으로 거의 안 돌아서 정지 장면을 찍을 때 필요하다)
        public void Evaluate(float t)
        {
            SetRenderersEnabled(true);
            Apply(t);
            if (sparks != null) sparks.Simulate(t, true, true);
            time = -1f; // 이어서 Update가 돌아도 지우지 않도록 멈춰 둔다. (정지 장면)
        }

        private void Stop()
        {
            time = -1f;
            SetRenderersEnabled(false);
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (coreRenderer != null) coreRenderer.enabled = enabled;
            if (glowRenderer != null) glowRenderer.enabled = enabled;
        }

        // 시간 t의 빔 길이·굵기·투명도를 정해 두 겹(심, 빛)에 적용한다.
        private void Apply(float t)
        {
            Vector3 direction = endPoint - lastStart;
            float fullLength = direction.magnitude;
            if (fullLength < 0.05f) return;

            // 처음 extendTime 동안 드론에서 끝 지점까지 길이가 쭉 자란다.
            float extend = extendTime <= 0f ? 1f : Mathf.Clamp01(t / extendTime);
            float length = fullLength * extend;

            // 유지가 끝나면 투명해지면서 가늘어진다.
            float fadeStart = extendTime + holdTime;
            float fade = t <= fadeStart ? 0f : Mathf.Clamp01((t - fadeStart) / Mathf.Max(fadeTime, 0.0001f));
            float alpha = 1f - fade;
            float width = Mathf.Lerp(1f, endWidth, fade);

            Quaternion rotation = Quaternion.LookRotation(direction.normalized);
            Place(coreRenderer, rotation, coreRadius * width, length);
            Place(glowRenderer, rotation, glowRadius * width, length);
            SetAlpha(coreRenderer, ref coreBlock, coreColor, coreMaxAlpha * alpha);
            SetAlpha(glowRenderer, ref glowBlock, glowColor, glowMaxAlpha * alpha);
        }

        private void Place(MeshRenderer renderer, Quaternion rotation, float radius, float length)
        {
            if (renderer == null) return;
            renderer.transform.SetPositionAndRotation(lastStart, rotation);
            // 관은 길이 1·반지름 1로 만들어 두었으므로, 반지름(xy)과 길이(z)만 곱해서 맞춘다.
            renderer.transform.localScale = new Vector3(radius, radius, length);
        }

        private static void SetAlpha(MeshRenderer renderer, ref MaterialPropertyBlock block, Color color, float alpha)
        {
            if (renderer == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            color.a = alpha;
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }

        // 길이 1(z=0~1), 반지름 1의 속이 빈 가는 관을 만든다. 양쪽 끝은 막지 않는다(끝이 가늘고 투명해 티가 안 난다).
        // 안쪽도 보이게 재질은 양면 그리기(Cull Off)로 둔다.
        public static Mesh BuildTubeMesh(int segments = 8)
        {
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float y = Mathf.Sin(angle);
                vertices[i] = new Vector3(x, y, 0f);
                vertices[segments + i] = new Vector3(x, y, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int t = i * 6;
                triangles[t] = i;
                triangles[t + 1] = segments + i;
                triangles[t + 2] = next;
                triangles[t + 3] = next;
                triangles[t + 4] = segments + i;
                triangles[t + 5] = segments + next;
            }

            var mesh = new Mesh { name = "SniperBeamTube" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
