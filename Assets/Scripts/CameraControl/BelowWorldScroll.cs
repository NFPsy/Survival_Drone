using UnityEngine;

namespace SurvivalDrone.CameraControl
{
    // 맵 아래에 깔아둔 "행성 표면" 평면이 플레이어를 따라다니면서
    // 천천히 흘러가는 것처럼 보이게 만드는 스크립트.
    // 평면 자체는 플레이어를 따라 움직이고(맵 끝에서 평면 끝이 보이지 않도록),
    // 그림(텍스처)은 플레이어가 움직인 만큼 조금만 밀려서 "아주 먼 아래쪽 세계"라는 높이감을 준다.
    public class BelowWorldScroll : MonoBehaviour
    {
        // 따라다닐 대상(플레이어). 비워두면 "Player" 태그 오브젝트를 자동으로 찾는다.
        [SerializeField] private Transform target;

        // 플레이어가 1m 움직일 때 그림이 얼마나(UV 단위) 밀릴지. 작을수록 더 멀리 있는 것처럼 보임.
        [SerializeField] private float parallax = 0.0006f;

        // 플레이어가 가만히 있어도 구름이 천천히 흘러가는 속도(UV/초).
        [SerializeField] private Vector2 drift = new Vector2(0.002f, 0.001f);

        // 재질에서 텍스처 위치를 바꿀 때 쓰는 속성 이름 (URP Unlit 셰이더의 기본 텍스처 이름).
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private Renderer rend;
        private float height; // 평면의 높이(y)는 처음 값 그대로 유지한다.

        private void Awake()
        {
            rend = GetComponent<Renderer>();
            height = transform.position.y;
        }

        private void Start()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
        }

        // 카메라가 움직인 뒤에 위치를 맞춰야 떨림이 없으므로 LateUpdate에서 처리한다.
        private void LateUpdate()
        {
            if (target == null) return;

            // 1) 평면을 플레이어의 x,z 위치로 옮긴다 (높이는 그대로).
            transform.position = new Vector3(target.position.x, height, target.position.z);

            // 2) 평면이 플레이어를 따라 움직이므로 그림도 같이 끌려온다.
            //    그림의 위치(offset)를 플레이어 위치에 아주 조금만 비례해서 바꿔주면
            //    "대부분은 따라오지만 조금씩만 뒤로 흘러가는" 아주 멀리 있는 배경처럼 보인다.
            Vector2 offset = new Vector2(target.position.x, target.position.z) * parallax
                             + drift * Time.time;
            // sharedMaterial이 아니라 material을 쓰면 이 오브젝트 전용 복사본이 만들어져서
            // 에셋 파일(Mat_BelowWorld.mat)이 플레이 중에 바뀌지 않는다.
            rend.material.SetTextureOffset(BaseMapId, offset);
        }
    }
}
