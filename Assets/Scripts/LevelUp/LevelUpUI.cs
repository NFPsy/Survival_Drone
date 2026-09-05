using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Drones;
using SurvivalDrone.Player;
using SurvivalDrone.Core;

namespace SurvivalDrone.LevelUp
{
    // 플레이어가 레벨업할 때마다 화면에 3개의 선택지를 보여주고,
    // 플레이어가 고른 것을 실제로 적용해주는 UI 스크립트.
    public class LevelUpUI : MonoBehaviour
    {
        // 레벨업 신호(OnLevelUp)를 받기 위해 연결해두는 플레이어 경험치 스크립트.
        [SerializeField] private PlayerExperience playerExperience;

        // 드론 장착/강화를 실제로 실행시켜줄 드론 매니저.
        [SerializeField] private DroneManager droneManager;

        // 능력치 강화를 실제로 실행시켜줄 플레이어 스탯.
        [SerializeField] private PlayerStats playerStats;

        // 선택지 3개를 담고 있는 UI 패널 전체 (평소엔 꺼져 있다가 레벨업 시 켜짐).
        [SerializeField] private GameObject panel;

        // 선택지 3개에 해당하는 버튼들.
        [SerializeField] private Button[] optionButtons;

        // 각 버튼 위에 표시될 제목 텍스트들.
        [SerializeField] private Text[] optionTitles;

        // 각 버튼 위에 표시될 설명 텍스트들.
        [SerializeField] private Text[] optionDescriptions;

        // 지금 화면에 보여지고 있는 선택지 3개를 기억해두는 목록.
        // (버튼을 눌렀을 때 "몇 번째 버튼이 어떤 선택지였는지" 알아야 하기 때문에 필요)
        private readonly List<LevelUpOption> currentOptions = new List<LevelUpOption>();

        private void OnEnable()
        {
            // 플레이어가 레벨업할 때마다 HandleLevelUp 함수가 자동으로 호출되도록 연결.
            if (playerExperience != null) playerExperience.OnLevelUp += HandleLevelUp;
        }

        private void OnDisable()
        {
            // 오브젝트가 사라질 때는 반드시 구독을 해제해야 한다.
            if (playerExperience != null) playerExperience.OnLevelUp -= HandleLevelUp;
        }

        private void Start()
        {
            // 게임 시작 시에는 레벨업 화면을 꺼둔다.
            if (panel != null) panel.SetActive(false);
        }

        // 레벨업이 일어났을 때 호출되는 함수. 새 레벨 번호는 지금 로직에서는 사용하지 않는다.
        private void HandleLevelUp(int newLevel)
        {
            ShowOptions();
        }

        // 선택지 3개를 화면에 만들어서 보여주는 함수.
        private void ShowOptions()
        {
            currentOptions.Clear();

            // 지금 상황에서 고를 수 있는 모든 선택지 후보를 만든다.
            var pool = BuildOptionPool();

            for (int i = 0; i < optionButtons.Length; i++)
            {
                // 후보가 다 떨어졌으면(선택지가 3개보다 적으면) 남는 버튼은 꺼버린다.
                if (pool.Count == 0)
                {
                    optionButtons[i].gameObject.SetActive(false);
                    continue;
                }

                // 후보 중 하나를 무작위로 뽑고, 중복 선택을 막기 위해 후보 목록에서 제거.
                int index = UnityEngine.Random.Range(0, pool.Count);
                var option = pool[index];
                pool.RemoveAt(index);
                currentOptions.Add(option);

                // 버튼을 켜고, 제목/설명 텍스트를 이 선택지 내용으로 채운다.
                optionButtons[i].gameObject.SetActive(true);
                if (optionTitles.Length > i && optionTitles[i] != null) optionTitles[i].text = option.Title;
                if (optionDescriptions.Length > i && optionDescriptions[i] != null) optionDescriptions[i].text = option.Description;

                // 버튼 클릭 시 실행할 함수를 연결한다.
                // capturedIndex를 따로 만드는 이유: for문의 i를 그대로 쓰면 버튼을 눌렀을 때
                // 항상 마지막 i값을 참조하게 되는 C#의 "클로저" 문제가 생기기 때문.
                int capturedIndex = i;
                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].onClick.AddListener(() => ChooseOption(capturedIndex));
            }

            // 패널을 켜서 화면에 보여주고, 시간을 멈춰서 게임을 일시정지시킨다(선택하는 동안 적이 움직이지 않도록).
            if (panel != null) panel.SetActive(true);
            Time.timeScale = 0f;
        }

        // 지금 고를 수 있는 모든 선택지 후보를 만들어서 리스트로 반환하는 함수.
        private List<LevelUpOption> BuildOptionPool()
        {
            var pool = new List<LevelUpOption>();

            // 드론 종류(Melee, Sniper, Collector)마다 "신규 장착" 또는 "강화" 선택지를 하나씩 만든다.
            foreach (DroneType type in Enum.GetValues(typeof(DroneType)))
            {
                // 아직 없는 드론이면 "신규 드론" 선택지를 추가.
                if (droneManager.CanAddDrone(type))
                {
                    pool.Add(new LevelUpOption
                    {
                        Kind = LevelUpOptionKind.NewDrone,
                        DroneType = type,
                        Title = $"신규 드론: {type}",
                        Description = "새로운 드론을 장착합니다."
                    });
                }
                // 이미 있고 아직 최대 레벨이 아니면 "강화" 선택지를 추가.
                else if (droneManager.CanUpgradeDrone(type))
                {
                    pool.Add(new LevelUpOption
                    {
                        Kind = LevelUpOptionKind.UpgradeDrone,
                        DroneType = type,
                        Title = $"강화: {type}",
                        Description = "보유한 드론의 레벨을 올립니다."
                    });
                }
                // 둘 다 아니면(이미 최대 레벨) 이 드론에 대한 선택지는 만들지 않는다.
            }

            // 능력치 강화 선택지 두 가지는 항상 후보에 포함시킨다.
            pool.Add(new LevelUpOption
            {
                Kind = LevelUpOptionKind.StatBoost,
                StatBoost = StatBoostKind.MoveSpeed,
                Title = "이동 속도 +6%",
                Description = "이동 속도를 증가시킵니다."
            });
            pool.Add(new LevelUpOption
            {
                Kind = LevelUpOptionKind.StatBoost,
                StatBoost = StatBoostKind.MaxHealth,
                Title = "최대 체력 +12",
                Description = "최대 체력을 증가시킵니다."
            });

            return pool;
        }

        // 플레이어가 선택지 버튼 중 하나를 눌렀을 때 호출되는 함수.
        private void ChooseOption(int index)
        {
            if (index < 0 || index >= currentOptions.Count) return;

            var option = currentOptions[index];

            // 선택지 종류에 따라 실제로 적용할 내용을 분기 처리.
            switch (option.Kind)
            {
                case LevelUpOptionKind.NewDrone:
                    droneManager.AddDrone(option.DroneType);
                    break;
                case LevelUpOptionKind.UpgradeDrone:
                    droneManager.UpgradeDrone(option.DroneType);
                    break;
                case LevelUpOptionKind.StatBoost:
                    if (option.StatBoost == StatBoostKind.MoveSpeed)
                    {
                        // 현재 이동속도의 6%만큼을 더해준다(고정값이 아니라 비율 증가).
                        playerStats.AddMoveSpeed(playerStats.MoveSpeed * 0.06f);
                    }
                    else
                    {
                        playerStats.AddMaxHealth(12f);
                    }
                    break;
            }

            float elapsed = GameManager.Instance != null ? GameManager.Instance.ElapsedTime : 0f;
            Debug.Log($"[LevelUp] {elapsed:F0}초 - \"{option.Title}\" 선택");

            // 선택이 끝났으니 패널을 끄고, 멈춰뒀던 시간을 다시 흐르게 한다.
            if (panel != null) panel.SetActive(false);
            Time.timeScale = 1f;
        }
    }
}
