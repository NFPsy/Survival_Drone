using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Player;

namespace SurvivalDrone.UI
{
    // 오버드라이브(액티브 스킬)가 켜져 있는 동안 화면 가장자리를 주황색으로 은은하게 맥동시키는 연출.
    //
    // 왜 필요한가: 오버드라이브는 5초밖에 안 되는데다, 효과(공격 속도 2.5배)가
    // 드론 쪽에서 일어나기 때문에 플레이어가 "지금 켜져 있나?"를 헷갈리기 쉽다.
    // 화면 전체에 색을 깔아두면 HUD를 안 봐도 상태를 알 수 있다.
    //
    // 색을 주황으로 쓴 이유: 게임의 기본 강조색인 시안색은 "안전/성장"에 쓰고 있어서,
    // "지금 피해를 2배로 받는 위험한 상태"라는 걸 색으로도 구분해주기 위해서다.
    // (체력 경고의 빨강과도 구분된다 — 빨강=죽기 직전, 주황=내가 선택한 위험)
    public class OverdriveOverlay : MonoBehaviour
    {
        // 게이지 상태를 구독할 플레이어의 OverdriveSystem.
        [SerializeField] private OverdriveSystem overdrive;

        // 화면 전체를 덮는 오버레이 이미지 (평소엔 완전히 투명).
        [SerializeField] private Image overlay;

        // 맥동(깜빡임) 속도. 저체력 경고(2.2)보다 조금 빠르게 해서 "긴박함"을 준다.
        [SerializeField] private float pulseSpeed = 5f;

        // 가장 진할 때의 투명도. 화면을 가리지 않도록 낮게 유지.
        [SerializeField] private float maxAlpha = 0.16f;

        // 지금 오버드라이브가 켜져 있는지.
        private bool isActive;

        private void OnEnable()
        {
            if (overdrive != null)
            {
                overdrive.OnOverdriveStarted += HandleStarted;
                overdrive.OnOverdriveEnded += HandleEnded;
            }
        }

        private void OnDisable()
        {
            // 오브젝트가 사라질 때는 구독을 반드시 해제한다(메모리 누수 방지).
            if (overdrive != null)
            {
                overdrive.OnOverdriveStarted -= HandleStarted;
                overdrive.OnOverdriveEnded -= HandleEnded;
            }
        }

        private void Start()
        {
            // 게임 시작 시에는 완전히 투명하게 꺼둔다.
            SetAlpha(0f);
        }

        private void HandleStarted()
        {
            isActive = true;
        }

        private void HandleEnded()
        {
            isActive = false;
            SetAlpha(0f);
        }

        private void Update()
        {
            if (!isActive || overlay == null) return;

            // 사인 함수로 0~1 사이를 부드럽게 오가는 값을 만들어 맥동 효과를 낸다.
            // Time.unscaledTime을 쓰지 않고 Time.time을 쓰는 이유: 레벨업 화면 등으로
            // 게임이 멈췄을 때(timeScale=0)는 연출도 같이 멈추는 게 자연스럽기 때문.
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            SetAlpha(pulse * maxAlpha);
        }

        private void SetAlpha(float alpha)
        {
            if (overlay == null) return;
            Color c = overlay.color;
            c.a = alpha;
            overlay.color = c;
        }
    }
}
