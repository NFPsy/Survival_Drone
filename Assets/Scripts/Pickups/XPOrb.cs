using UnityEngine;
using SurvivalDrone.Player;

namespace SurvivalDrone.Pickups
{
    // 적을 처치하면 생성되는 "경험치 구슬" 오브젝트에 붙는 스크립트.
    // 플레이어가 일정 거리 안으로 들어오면 자석처럼 끌려가고, 닿으면 XP를 지급한 뒤 사라진다.
    public class XPOrb : MonoBehaviour
    {
        // 수집 드론이 없을 때도 기본으로 적용되는 자석 반경(이 거리 안에 들어오면 끌려감).
        [SerializeField] private float baseMagnetRadius = 2.5f;

        // 플레이어 쪽으로 끌려가는 속도.
        [SerializeField] private float moveSpeed = 10f;

        // 이 거리보다 가까워지면 "먹은 것"으로 처리한다.
        [SerializeField] private float pickupDistance = 0.6f;

        // 이 오브가 지급할 XP 양. 적마다 다르게 설정되므로 SetValue로 외부에서 지정받는다.
        private float xpValue = 1f;

        // 쫓아갈 대상(플레이어)의 Transform. 처음엔 비어있다가 첫 프레임에 찾아서 저장해둔다.
        private Transform target;

        // XP를 실제로 지급할 때 사용할 플레이어의 경험치 스크립트.
        private PlayerExperience playerXP;

        // 적이 죽을 때(EnemyAI에서) 이 함수를 호출해서 얼마짜리 XP인지 알려준다.
        public void SetValue(float value)
        {
            xpValue = value;
        }

        private void Update()
        {
            // 아직 플레이어를 찾지 못했다면 "Player" 태그를 가진 오브젝트를 찾아서 저장.
            // (매 프레임 찾지 않고 한 번만 찾도록 캐싱하는 방식)
            if (target == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj == null) return;
                target = playerObj.transform;
                playerXP = playerObj.GetComponent<PlayerExperience>();
            }

            // 플레이어와의 현재 거리를 계산.
            float distance = Vector3.Distance(transform.position, target.position);

            // 실제 자석 범위 = 기본 범위 + 수집 드론이 추가로 늘려준 범위.
            float magnetRadius = baseMagnetRadius + MagnetField.ExtraRadius;

            // 자석 범위 안에 들어왔으면 플레이어 쪽으로 조금씩 이동.
            if (distance <= magnetRadius)
            {
                transform.position = Vector3.MoveTowards(transform.position, target.position, moveSpeed * Time.deltaTime);
            }

            // 아주 가까워지면(거의 닿으면) XP를 지급하고 자기 자신을 파괴(사라짐).
            if (distance <= pickupDistance)
            {
                playerXP?.AddXP(xpValue);
                Destroy(gameObject);
            }
        }
    }
}
