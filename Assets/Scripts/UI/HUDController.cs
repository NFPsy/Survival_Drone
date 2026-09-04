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

        // XP바로 사용할 이미지.
        [SerializeField] private Image xpFill;

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
        }

        // XP가 바뀔 때 호출되어 XP바를 채워진 비율로 갱신.
        private void HandleXPChanged(float current, float toNext)
        {
            if (xpFill != null) xpFill.fillAmount = toNext > 0f ? current / toNext : 0f;
        }

        // 레벨업이 일어날 때 호출되어 레벨 텍스트를 새 레벨로 갱신.
        private void HandleLevelUp(int newLevel)
        {
            if (levelText != null) levelText.text = $"Lv. {newLevel}";
        }
    }
}
