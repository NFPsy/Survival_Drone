using UnityEngine;

namespace SurvivalDrone.Pickups
{
    // 경험치 구슬의 "겉모습"을 살아 있는 것처럼 움직이게 하는 스크립트.
    //  - 바닥에서 살짝 떠서 천천히 위아래로 둥실거린다.
    //  - 크기가 은은하게 커졌다 작아지며 맥동한다.
    //  - 구슬 뒤의 빛 번짐 면(Glow)이 카메라를 정면으로 보게 맞춘다.
    //
    // 이 스크립트는 구슬 프리팹의 "Visual"(겉모습을 담은 자식)에 붙인다.
    // 실제 위치·줍기 판정은 부모(XPOrb)가 그대로 맡으므로, 이 스크립트는 화면에 보이는 모습만 바꾼다.
    // (겉모습만 띄워 놓았기 때문에 줍는 거리·자석 범위는 예전과 같다)
    public class XPOrbVisual : MonoBehaviour
    {
        // 바닥에서 떠 있는 기본 높이(월드 미터).
        [SerializeField] private float baseHeight = 0.35f;

        // 위아래로 둥실거리는 폭(미터)과 속도.
        [SerializeField] private float bobAmount = 0.07f;
        [SerializeField] private float bobSpeed = 2.2f;

        // 크기가 맥동하는 정도(0.12 = 위아래로 12%)와 속도.
        [SerializeField] private float pulseAmount = 0.12f;
        [SerializeField] private float pulseSpeed = 3.2f;

        // 구슬 뒤의 빛 번짐 면. 카메라를 향하도록 맞춘다. 비워 두면 번짐 없이 구슬만 움직인다.
        [SerializeField] private Transform glow;

        // 구슬마다 움직이는 박자가 달라지도록 시작 위상을 무작위로 정한다. (여러 개가 한꺼번에 같은 박자로 움직이지 않게)
        private float phase;

        private Vector3 baseScale;
        private Transform parent;

        private void Awake()
        {
            phase = Random.Range(0f, Mathf.PI * 2f);
            baseScale = transform.localScale;
            parent = transform.parent;

            // 이 게임의 카메라는 회전하지 않고 평행 이동만 하므로, 빛 번짐 면을 한 번만 카메라 방향으로 맞춰 두면 된다.
            if (glow != null && Camera.main != null)
                glow.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up);
        }

        private void Update()
        {
            float t = Time.time;

            // 부모(구슬 루트)가 작게 줄여져 있으므로, 월드 미터로 정한 높이를 부모 크기로 나눠서 로컬 좌표로 바꾼다.
            float parentScale = parent != null ? Mathf.Max(0.0001f, parent.lossyScale.y) : 1f;
            float height = baseHeight + Mathf.Sin(t * bobSpeed + phase) * bobAmount;
            transform.localPosition = new Vector3(0f, height / parentScale, 0f);

            float pulse = 1f + Mathf.Sin(t * pulseSpeed + phase * 1.3f) * pulseAmount;
            transform.localScale = baseScale * pulse;
        }
    }
}
