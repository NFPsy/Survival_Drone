using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Player;

namespace SurvivalDrone.UI
{
    // 화면 상단의 HUD(체력바, XP바, 타이머, 레벨 표시)를 최신 상태로 갱신해주는 스크립트.
    // 직접 값을 계산하지 않고, Health/PlayerExperience/GameManager가 보내는 이벤트를 받아서
    // 화면 UI만 업데이트하는 역할을 한다.
    public class HUDController : MonoBehaviour
    {
        // 체력 변화를 감지하기 위한 플레이어의 Health 컴포넌트.
        [SerializeField] private Health playerHealth;

        // XP/레벨 변화를 감지하기 위한 플레이어의 경험치 컴포넌트.
        [SerializeField] private PlayerExperience playerExperience;

        // 체력바로 사용할 이미지 (fillAmount 값을 0~1로 조절해서 채워지는 정도를 표현).
        [SerializeField] private Image healthFill;

        // 체력을 "80 / 100"처럼 숫자로도 함께 보여줄 텍스트.
        // 체력바만 있으면 살짝 줄어드는 건 눈에 잘 안 띄기 때문에, 숫자로 확실하게 보여주기 위해 추가.
        [SerializeField] private Text healthText;

        // XP바로 사용할 이미지.
        [SerializeField] private Image xpFill;

        // XP를 "3 / 10"처럼 숫자로도 함께 보여줄 텍스트.
        [SerializeField] private Text xpText;

        // ── 오버드라이브(액티브 스킬) 관련 ──

        // 오버드라이브 게이지를 읽어오기 위한 플레이어의 OverdriveSystem 컴포넌트.
        [SerializeField] private SurvivalDrone.Player.OverdriveSystem overdrive;

        // 오버드라이브 게이지 바로 사용할 이미지.
        [SerializeField] private Image overdriveFill;

        // 게이지 상태를 글자로 알려줄 텍스트 ("충전 중" / "SPACE 발동 가능" / "발동 중").
        [SerializeField] private Text overdriveText;

        // 게이지가 아직 다 안 찼을 때의 바 색상 (어두운 시안).
        [SerializeField] private Color overdriveChargingColor = new Color(0.18f, 0.45f, 0.5f);

        // 가득 차서 쓸 수 있을 때의 바 색상 (밝은 시안 — "지금 누르라"는 신호).
        [SerializeField] private Color overdriveReadyColor = new Color(0.31f, 0.847f, 0.91f);

        // 발동 중일 때의 바 색상 (주황 — 위험을 감수하는 상태라는 뜻).
        [SerializeField] private Color overdriveActiveColor = new Color(1f, 0.62f, 0.25f);

        // 남은 시간을 보여줄 텍스트.
        [SerializeField] private Text timerText;

        // 현재 레벨을 보여줄 텍스트.
        [SerializeField] private Text levelText;

        private void OnEnable()
        {
            // 체력이 바뀔 때마다 HandleHealthChanged가 자동으로 호출되도록 연결.
            if (playerHealth != null) playerHealth.OnHealthChanged += HandleHealthChanged;

            if (playerExperience != null)
            {
                // XP가 바뀔 때, 레벨업이 일어날 때 각각 자동으로 호출되도록 연결.
                playerExperience.OnXPChanged += HandleXPChanged;
                playerExperience.OnLevelUp += HandleLevelUp;
            }

            // 오버드라이브 게이지가 바뀔 때마다 게이지 바를 갱신하도록 연결.
            if (overdrive != null) overdrive.OnGaugeChanged += HandleOverdriveChanged;
        }

        private void OnDisable()
        {
            // 오브젝트가 사라질 때는 반드시 구독을 해제한다(메모리 누수/에러 방지).
            if (playerHealth != null) playerHealth.OnHealthChanged -= HandleHealthChanged;
            if (playerExperience != null)
            {
                playerExperience.OnXPChanged -= HandleXPChanged;
                playerExperience.OnLevelUp -= HandleLevelUp;
            }
            if (overdrive != null) overdrive.OnGaugeChanged -= HandleOverdriveChanged;
        }

        private void Start()
        {
            // 게임 시작 시 레벨 텍스트를 현재 레벨(보통 1)로 초기화.
            if (levelText != null && playerExperience != null) levelText.text = $"Lv. {playerExperience.Level}";
        }

        private void Update()
        {
            // 타이머는 매 프레임 계속 줄어들기 때문에, 이벤트 방식이 아니라 여기서 직접 갱신한다.
            if (timerText != null && GameManager.Instance != null)
            {
                float t = GameManager.Instance.TimeRemaining;

                // 초 단위 시간을 "분:초" 형태로 변환.
                int minutes = Mathf.FloorToInt(t / 60f);
                int seconds = Mathf.FloorToInt(t % 60f);

                // "00:00" 형식으로 자릿수를 맞춰서 표시 (예: 9분 5초 -> "09:05").
                timerText.text = $"{minutes:00}:{seconds:00}";
            }
        }

        // 체력이 바뀔 때 호출되어 체력바를 채워진 비율로 갱신.
        private void HandleHealthChanged(float current, float max)
        {
            if (healthFill != null) healthFill.fillAmount = max > 0f ? current / max : 0f;

            // 정수로 반올림해서 "80 / 100" 형태로 표시 (소수점까지 보여줄 필요는 없으므로).
            if (healthText != null) healthText.text = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
        }

        // XP가 바뀔 때 호출되어 XP바를 채워진 비율로 갱신.
        private void HandleXPChanged(float current, float toNext)
        {
            if (xpFill != null) xpFill.fillAmount = toNext > 0f ? current / toNext : 0f;

            // 체력바와 마찬가지로 "3 / 10" 형태의 숫자도 함께 보여준다.
            if (xpText != null) xpText.text = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(toNext)}";
        }

        // 레벨업이 일어날 때 호출되어 레벨 텍스트를 새 레벨로 갱신.
        private void HandleLevelUp(int newLevel)
        {
            if (levelText != null) levelText.text = $"Lv. {newLevel}";
        }

        // 오버드라이브 게이지가 바뀔 때 호출되어 게이지 바와 안내 문구를 갱신한다.
        // ratio: 0~1 사이의 게이지 비율, isActive: 지금 발동 중인지.
        private void HandleOverdriveChanged(float ratio, bool isActive)
        {
            if (overdriveFill != null)
            {
                overdriveFill.fillAmount = ratio;

                // 상태에 따라 바 색을 바꿔서, 숫자를 안 읽어도 상태를 알 수 있게 한다.
                // 발동 중 = 주황 / 가득 참 = 밝은 시안 / 충전 중 = 어두운 시안
                if (isActive) overdriveFill.color = overdriveActiveColor;
                else if (ratio >= 1f) overdriveFill.color = overdriveReadyColor;
                else overdriveFill.color = overdriveChargingColor;
            }

            if (overdriveText != null)
            {
                if (isActive) overdriveText.text = "OVERDRIVE";
                else if (ratio >= 1f) overdriveText.text = "[SPACE] 오버드라이브";
                else overdriveText.text = $"오버드라이브 {Mathf.FloorToInt(ratio * 100f)}%";
            }
        }
    }
}
