using UnityEngine;

namespace SurvivalDrone.Drones
{
    // 회복 드론이 체력을 채워 줄 때 보여 주는 이펙트: "드론 → 플레이어" 초록 빛줄기 + 떠오르는 "+" 모양 + 발밑 링.
    // (CBT 3번 테스터의 "공격 이펙트가 밋밋하다"는 의견과 가져온 참고 그림을 바탕으로 만들었다. 게임 수치는 바꾸지 않고 눈에 보이는 연출만 더한다.)
    //
    // 구성 (HealEffect.prefab, HealEffectBuilder 메뉴가 만들어 준다):
    //  - Beam: 드론에서 플레이어를 향해 뻗는 반투명 원뿔. 모양(메시)은 이 스크립트가 코드로 만든다. 별도 FBX 파일은 없다.
    //  - Plus: 초록 "+" 5개가 플레이어 몸 주변에서 위로 떠오르며 사라지는 파티클.
    //  - Ring: 플레이어 발밑에서 초록 원이 퍼지며 사라지는 파티클.
    //
    // 드론은 크기가 0.5배로 줄어 있어서, 이 이펙트는 드론의 자식이 아니라 따로(최상위에) 만들어 크기가 같이 줄지 않게 한다.
    // 한 번 재생에 약 0.9초 걸리고, 재생 중에는 드론과 플레이어가 움직여도 빛줄기가 계속 둘을 이어 준다.
    public class HealBeamEffect : MonoBehaviour
    {
        [SerializeField] private MeshRenderer beamRenderer;
        [SerializeField] private ParticleSystem plusParticles;
        [SerializeField] private ParticleSystem ringParticles;

        // 빛줄기 색(밝은 초록). 겹쳐서 밝아지는(additive) 재질이라 어두운 바탕에서도 잘 보인다.
        [SerializeField] private Color color = new Color(0.35f, 1f, 0.45f, 1f);

        // 빛줄기가 가장 진할 때의 투명도(은은하게: 0.4). 1이면 아주 진하다.
        [SerializeField] private float maxAlpha = 0.4f;

        // 빛줄기가 나타나는 시간, 유지되는 시간, 사라지는 시간(초). 합 = 0.9초.
        [SerializeField] private float fadeIn = 0.1f;
        [SerializeField] private float hold = 0.3f;
        [SerializeField] private float fadeOut = 0.5f;

        // 빛줄기 원뿔: 드론 쪽 끝 반지름과 플레이어 쪽 끝 반지름(미터).
        [SerializeField] private float apexRadius = 0.075f;
        [SerializeField] private float endRadius = 0.7f;

        // 빛줄기가 닿는 높이: 플레이어 발 위치에서 얼마나 위(몸 한가운데)인지. 플레이어 키는 약 1.9m.
        [SerializeField] private float targetHeight = 0.9f;

        // "+" 모양이 나타나는 높이(발 위, 미터).
        [SerializeField] private float plusHeight = 0.4f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Transform beam;
        private MaterialPropertyBlock block;
        private Transform source;
        private Transform target;
        private float time = -1f;

        private float TotalSeconds => fadeIn + hold + fadeOut;

        private void Awake()
        {
            if (beamRenderer != null)
            {
                beam = beamRenderer.transform;
                var filter = beamRenderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh == null) filter.sharedMesh = BuildConeMesh(apexRadius, endRadius);
                beamRenderer.enabled = false;
            }
            block = new MaterialPropertyBlock();
        }

        // from(회복 드론)에서 to(플레이어)로 한 번 재생한다.
        public void Play(Transform from, Transform to)
        {
            if (from == null || to == null) return;

            source = from;
            target = to;
            time = 0f;

            if (beamRenderer != null) beamRenderer.enabled = true;
            Place();
            SetBeamAlpha(0f);

            if (plusParticles != null) plusParticles.Play();
            if (ringParticles != null) ringParticles.Play();
        }

        // 재생 도중 매 프레임 빛줄기를 드론과 플레이어 사이에 맞춘다.
        private void Update()
        {
            if (time < 0f) return;

            time += Time.deltaTime;
            if (source == null || target == null || time >= TotalSeconds)
            {
                Stop();
                return;
            }

            Place();
            SetBeamAlpha(AlphaAt(time));
        }

        // 검증·미리보기용: 시간 t초 지난 순간의 모습으로 한 번에 맞춘다. (Play를 한 번 부른 뒤에 쓴다. 에디터 화면은 실시간으로 거의 안 돌아서 정지 장면을 찍을 때 필요하다)
        public void Evaluate(float t)
        {
            if (source == null || target == null) return;
            time = t;
            if (beamRenderer != null) beamRenderer.enabled = true;
            Place();
            SetBeamAlpha(AlphaAt(t));
            if (plusParticles != null) plusParticles.Simulate(t, true, true);
            if (ringParticles != null) ringParticles.Simulate(t, true, true);
            time = -1f; // 이어서 Update가 돌아도 지우지 않도록 멈춰 둔다. (정지 장면)
        }

        // 시간에 따른 빛줄기 투명도 배율(0~1): 빠르게 나타나고, 잠깐 머물고, 천천히 사라진다.
        public float AlphaAt(float t)
        {
            if (t < fadeIn) return fadeIn <= 0f ? 1f : t / fadeIn;
            if (t < fadeIn + hold) return 1f;
            return fadeOut <= 0f ? 0f : Mathf.Clamp01(1f - (t - fadeIn - hold) / fadeOut);
        }

        private void Stop()
        {
            time = -1f;
            if (beamRenderer != null) beamRenderer.enabled = false;
        }

        // 빛줄기와 파티클을 이번 프레임의 드론·플레이어 위치에 놓는다.
        private void Place()
        {
            if (source == null || target == null) return;

            Vector3 start = source.position;
            Vector3 end = target.position + Vector3.up * targetHeight;
            Vector3 direction = end - start;

            // 드론이 플레이어와 거의 겹쳐 있어도 빛줄기가 너무 짧아지지 않게 최소 길이를 둔다.
            float length = Mathf.Max(direction.magnitude, 0.8f);
            Vector3 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.down;

            if (beam != null)
            {
                beam.SetPositionAndRotation(start, Quaternion.LookRotation(forward));
                beam.localScale = new Vector3(1f, 1f, length); // 원뿔은 길이(z)만 늘이고 폭은 그대로
            }
            if (plusParticles != null) plusParticles.transform.position = target.position + Vector3.up * plusHeight;
            if (ringParticles != null) ringParticles.transform.position = target.position + Vector3.up * 0.05f;
        }

        private void SetBeamAlpha(float factor)
        {
            if (beamRenderer == null) return;
            var c = color;
            c.a = maxAlpha * factor;
            beamRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            beamRenderer.SetPropertyBlock(block);
        }

        // 한쪽 끝(z=0, 드론)이 좁고 반대쪽 끝(z=1, 플레이어)이 넓은 속이 빈 원뿔을 만든다.
        // 꼭짓점 색의 투명도로 "드론 쪽은 진하고 플레이어 쪽은 연하게" 번지는 느낌을 낸다.
        // 안쪽도 보이게 재질은 양면 그리기(Cull Off)로 둔다.
        public static Mesh BuildConeMesh(float apexRadius, float endRadius, int segments = 20)
        {
            var vertices = new Vector3[segments * 2];
            var colors = new Color[segments * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i] = new Vector3(cos * apexRadius, sin * apexRadius, 0f);
                colors[i] = new Color(1f, 1f, 1f, 1f);

                vertices[segments + i] = new Vector3(cos * endRadius, sin * endRadius, 1f);
                colors[segments + i] = new Color(1f, 1f, 1f, 0.12f);
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

            var mesh = new Mesh { name = "HealBeamCone" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
