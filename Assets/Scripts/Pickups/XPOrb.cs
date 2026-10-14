using UnityEngine;
using SurvivalDrone.Player;
using SurvivalDrone.Core;

namespace SurvivalDrone.Pickups
{
    // 적을 처치하면 생성되는 "경험치 구슬" 오브젝트에 붙는 스크립트.
    // 플레이어가 일정 거리 안으로 들어오면 자석처럼 끌려가고, 닿으면 XP를 지급한 뒤 사라진다.
    // 경험치 구슬의 "값 구간별 색" 한 칸. minValue 이상인 구슬은 이 칸의 재질(코어 + 빛 번짐)로 그려진다.
    [System.Serializable]
    public class XPOrbTier
    {
        // 이 값 이상이면 이 색을 쓴다. (표는 minValue가 작은 것부터 순서대로 적는다)
        public float minValue;

        // 구슬 알맹이(Core)와 뒤의 빛 번짐(Glow)에 입힐 재질.
        public Material coreMaterial;
        public Material glowMaterial;
    }

    public class XPOrb : MonoBehaviour
    {
        // 값 구간별 색 표. 비워 두면 프리팹에 칠해진 기본 색(시안) 그대로 쓴다.
        // 구슬이 후반에 수백 개 쌓이기 때문에 구슬마다 색을 따로 칠하지 않고, 미리 만든 재질로 바꿔 끼우는 방식이다.
        // (재질을 같은 것끼리 공유해야 웹에서도 한꺼번에 묶어 그려서 가볍다)
        [SerializeField] private XPOrbTier[] tiers;

        // 재질을 바꿔 끼울 대상: 구슬 알맹이와 빛 번짐 면의 렌더러.
        [SerializeField] private Renderer coreRenderer;
        [SerializeField] private Renderer glowRenderer;

        // 주울 때 재생할 효과음.
        [SerializeField] private AudioClip pickupSound;

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
            ApplyTierColor();
        }

        // 값에 맞는 칸(색 표에서 minValue 이하 중 가장 큰 칸)의 번호를 돌려준다. 어느 칸에도 안 맞으면 -1.
        // 표는 minValue가 작은 것부터 적혀 있다고 가정한다. 화면과 무관한 계산이라 따로 떼어 두어 검증 도구가 바로 부를 수 있다.
        public static int TierIndexFor(XPOrbTier[] table, float value)
        {
            int found = -1;
            if (table == null) return found;

            for (int i = 0; i < table.Length; i++)
            {
                if (table[i] != null && value >= table[i].minValue) found = i;
            }
            return found;
        }

        // 지급할 XP 값에 맞는 색 재질로 바꿔 끼운다. 표가 비었거나 칸의 재질이 비었으면 프리팹의 기본 색을 그대로 둔다.
        private void ApplyTierColor()
        {
            int index = TierIndexFor(tiers, xpValue);
            if (index < 0) return;

            var tier = tiers[index];
            if (coreRenderer != null && tier.coreMaterial != null) coreRenderer.sharedMaterial = tier.coreMaterial;
            if (glowRenderer != null && tier.glowMaterial != null) glowRenderer.sharedMaterial = tier.glowMaterial;
        }

        // 적이 죽을 때(EnemyAI에서) 쫓아갈 플레이어를 직접 알려준다.
        // 구슬이 첫 프레임마다 "Player" 태그로 씬 전체를 뒤져서 찾지 않아도 되게 하려는 것이다. (적이 많을수록 낭비가 컸다)
        public void SetTarget(Transform player)
        {
            if (player == null) return;
            target = player;
            playerXP = player.GetComponent<PlayerExperience>();
        }

        private void Update()
        {
            // 아직 플레이어를 모른다면(SetTarget으로 받지 못한 경우) "Player" 태그를 가진 오브젝트를 찾아서 저장.
            // (매 프레임 찾지 않고 한 번만 찾도록 캐싱하는 방식. 안전장치로 남겨 둔 것이다)
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
                AudioManager.Instance?.PlaySfx(pickupSound, 0.6f);
                Destroy(gameObject);
            }
        }
    }
}
