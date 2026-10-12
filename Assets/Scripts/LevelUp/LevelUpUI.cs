using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SurvivalDrone.Drones;
using SurvivalDrone.Player;
using SurvivalDrone.Core;
using SurvivalDrone.UI;

namespace SurvivalDrone.LevelUp
{
    // 플레이어가 레벨업할 때마다 화면에 3개의 선택지를 보여주고,
    // 플레이어가 고른 것을 실제로 적용해주는 UI 스크립트.
    //
    // 카드 한 장의 구성 (위에서 아래로):
    //   종류 라벨(NEW / 강화 / 능력치) → 큰 아이콘 → 이름 → 설명 → "무엇이 어떻게 바뀌는지"(예: Lv 2 → 3)
    // 마우스를 올리면 카드가 밝아지고(LevelUpCardHover), 키보드 1·2·3으로도 고를 수 있다.
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

        // 각 카드의 큰 아이콘들. 드론 선택지는 드론 종류 아이콘, 능력치 선택지는 속도·체력 아이콘을 넣는다.
        // 비워 두면(씬에 아직 안 붙였으면) 아이콘 없이 예전처럼 동작한다.
        [SerializeField] private Image[] optionIcons;

        // 아래 칸들도 모두 비워 두면(옛 씬) 그 부분만 건너뛰고 예전처럼 동작한다.
        // 각 카드 맨 위 작은 라벨 (NEW / 강화 / 능력치)
        [SerializeField] private Text[] optionTags;

        // 각 카드 맨 아래 "무엇이 바뀌는지" (예: Lv 2 → 3)
        [SerializeField] private Text[] optionChanges;

        // 각 카드 모서리의 키 안내 (예: [1])
        [SerializeField] private Text[] optionKeyHints;

        // 화면 위쪽 제목 ("레벨 업!")과 작은 안내 글자
        [SerializeField] private Text headerText;
        [SerializeField] private Text subHeaderText;

        // 레벨업 선택 화면이 뜰 때 재생할 효과음. 사운드 파일이 아직 없다면 비워둬도 안전하다.
        [SerializeField] private AudioClip levelUpSound;

        // 선택지를 고를 때 재생할 효과음.
        [SerializeField] private AudioClip chooseSound;

        // 라벨 색: 신규 드론 = 시안, 강화 = 연한 초록, 능력치 = 회색. (주황은 "잠금" 전용색이라 쓰지 않는다)
        private static readonly Color NewColor = new Color(0.31f, 0.847f, 0.91f);
        private static readonly Color UpgradeColor = new Color(0.55f, 0.95f, 0.6f);
        private static readonly Color StatColor = new Color(0.72f, 0.78f, 0.8f);

        // 지금 화면에 보여지고 있는 선택지 3개를 기억해두는 목록.
        // (버튼을 눌렀을 때 "몇 번째 버튼이 어떤 선택지였는지" 알아야 하기 때문에 필요)
        private readonly List<LevelUpOption> currentOptions = new List<LevelUpOption>();

        private void OnEnable()
        {
            // 플레이어가 레벨업할 때마다 HandleLevelUp 함수가 자동으로 호출되도록 연결.
            if (playerExperience != null) playerExperience.OnLevelUp += HandleLevelUp;

            // 미니 보스를 잡았을 때도 같은 선택 화면을 한 번 보여준다.
            GameEvents.OnMiniBossKilled += HandleMiniBossKilled;
        }

        private void OnDisable()
        {
            // 오브젝트가 사라질 때는 반드시 구독을 해제해야 한다.
            if (playerExperience != null) playerExperience.OnLevelUp -= HandleLevelUp;
            GameEvents.OnMiniBossKilled -= HandleMiniBossKilled;
        }

        private void Start()
        {
            // 게임 시작 시에는 레벨업 화면을 꺼둔다.
            if (panel != null) panel.SetActive(false);

            // 새 판이 시작됐으니 지난 판의 선택 기록(테스트 기록용)을 비운다.
            LevelUpPickLog.Reset();
        }

        // 선택 화면이 떠 있는 동안 키보드 1·2·3으로도 고를 수 있다. (게임이 멈춰 있어도 Update는 계속 돈다)
        private void Update()
        {
            if (panel == null || !panel.activeSelf || optionButtons == null) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) ChooseByKey(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) ChooseByKey(1);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) ChooseByKey(2);
        }

        // 그 번호의 카드가 화면에 켜져 있을 때만 고른다.
        private void ChooseByKey(int index)
        {
            if (index >= optionButtons.Length || optionButtons[index] == null || !optionButtons[index].gameObject.activeSelf) return;
            ChooseOption(index);
        }

        // 레벨업이 일어났을 때 호출되는 함수. 새 레벨 번호는 지금 로직에서는 사용하지 않는다.
        private void HandleLevelUp(int newLevel)
        {
            ShowOptions(false);
        }

        // 미니 보스 보상으로 받을 선택 화면이 밀려 있는 횟수. 이미 선택 화면이 떠 있을 때 미니 보스를 잡으면 하나 쌓아두고,
        // 지금 화면의 선택이 끝난 뒤에 이어서 보여준다. (화면이 겹쳐서 앞의 선택이 사라지는 것을 막기 위함)
        private int pendingBonusChoices;

        // 미니 보스를 처치했을 때 호출된다. 선택 화면이 비어 있으면 바로 보여주고, 떠 있으면 다음으로 미룬다.
        private void HandleMiniBossKilled()
        {
            if (panel != null && panel.activeSelf) pendingBonusChoices++;
            else ShowOptions(true);
        }

        // 선택지 3개를 화면에 만들어서 보여주는 함수. bonus: 미니 보스 처치 보상으로 뜬 화면인지 (제목 글자만 달라진다).
        private void ShowOptions(bool bonus = false)
        {
            currentOptions.Clear();

            // 지금 상황에서 고를 수 있는 모든 선택지 후보를 만든다.
            var pool = BuildOptionPool();

            // 화면 위쪽 제목
            if (headerText != null) headerText.text = bonus ? "미니 보스 처치 보상!" : "레벨 업!";
            if (subHeaderText != null) subHeaderText.text = "하나를 골라 주세요   (키보드 1 · 2 · 3)";

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
                SetText(optionTitles, i, option.Title);
                SetText(optionDescriptions, i, option.Description);
                SetText(optionChanges, i, option.ChangeText);
                SetText(optionKeyHints, i, $"[{i + 1}]");

                // 종류 라벨: 종류마다 색을 달리한다.
                SetText(optionTags, i, option.TagText);
                if (optionTags != null && i < optionTags.Length && optionTags[i] != null)
                {
                    optionTags[i].color = option.Kind == LevelUpOptionKind.NewDrone ? NewColor
                                        : option.Kind == LevelUpOptionKind.UpgradeDrone ? UpgradeColor
                                        : StatColor;
                }
                if (optionChanges != null && i < optionChanges.Length && optionChanges[i] != null)
                    optionChanges[i].color = option.Kind == LevelUpOptionKind.UpgradeDrone ? UpgradeColor : NewColor;

                // 큰 아이콘: 드론 선택지는 그 드론의 종류 아이콘, 능력치 선택지는 속도·체력 아이콘.
                if (optionIcons != null && optionIcons.Length > i && optionIcons[i] != null)
                {
                    if (option.Kind == LevelUpOptionKind.StatBoost)
                        UiIcons.SetStatIcon(optionIcons[i], option.StatBoost == StatBoostKind.MoveSpeed);
                    else UiIcons.SetDroneIcon(optionIcons[i], option.DroneType);
                }

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
            AudioManager.Instance?.PlaySfx(levelUpSound);
        }

        private static void SetText(Text[] texts, int index, string value)
        {
            if (texts != null && index < texts.Length && texts[index] != null) texts[index].text = value;
        }

        // 신규 드론 카드에 쓰는 한 줄 설명: 그 드론이 무엇을 하는지.
        private static string NewDroneDescription(DroneType type)
        {
            switch (type)
            {
                case DroneType.Melee: return "플레이어 주위를 돌며\n가까운 적을 자동으로 베어요.";
                case DroneType.Sniper: return "가장 가까운 적을\n멀리서 쏴요.";
                case DroneType.Collector: return "경험치 구슬을\n더 멀리서 끌어와요.";
                case DroneType.Explosion: return "몇 초마다 주변의 적을\n한꺼번에 터뜨려요.";
                case DroneType.Heal: return "공격은 안 해요.\n체력을 조금씩 채워 줘요.";
                default: return "새로운 드론을 장착해요.";
            }
        }

        // 강화 카드에 쓰는 한 줄 설명: 강화하면 무엇이 좋아지는지.
        private static string UpgradeDescription(DroneType type)
        {
            switch (type)
            {
                case DroneType.Melee: return "회전 속도와\n공격 범위가 늘어요.";
                case DroneType.Sniper: return "공격력과\n공격 속도가 늘어요.";
                case DroneType.Collector: return "구슬을 끌어당기는\n범위가 늘어요.";
                case DroneType.Explosion: return "폭발 범위와\n피해가 늘어요.";
                case DroneType.Heal: return "회복량이 늘고\n더 자주 회복해요.";
                default: return "드론의 레벨을 올려요.";
            }
        }

        // 지금 고를 수 있는 모든 선택지 후보를 만들어서 리스트로 반환하는 함수.
        private List<LevelUpOption> BuildOptionPool()
        {
            var pool = new List<LevelUpOption>();

            // droneManager 연결이 인스펙터에서 빠져있으면(실수로 끊겼거나 프리팹이 바뀐 경우)
            // 드론 관련 선택지는 만들지 않고 능력치 강화만 남긴다. 여기서 막지 않으면
            // 바로 아래에서 NullReferenceException이 나서 레벨업 화면 자체가 멈춰버린다.
            if (droneManager != null)
            {
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
                            Title = DroneNames.Get(type),
                            Description = NewDroneDescription(type),
                            TagText = "NEW",
                            ChangeText = "Lv 1 로 시작"
                        });
                    }
                    // 이미 있고 아직 최대 레벨이 아니면 "강화" 선택지를 추가.
                    else if (droneManager.CanUpgradeDrone(type))
                    {
                        var owned = droneManager.Owned[type];
                        int next = owned.Level + 1;
                        bool becomesMax = next >= owned.MaxLevel;
                        pool.Add(new LevelUpOption
                        {
                            Kind = LevelUpOptionKind.UpgradeDrone,
                            DroneType = type,
                            Title = DroneNames.Get(type),
                            Description = UpgradeDescription(type) + (becomesMax ? "\n최대 레벨: 변신해서 더 강해져요!" : ""),
                            TagText = "강화",
                            ChangeText = becomesMax ? $"Lv {owned.Level} → {next}  변신!" : $"Lv {owned.Level} → {next}"
                        });
                    }
                    // 둘 다 아니면(이미 최대 레벨) 이 드론에 대한 선택지는 만들지 않는다.
                }
            }

            // 능력치 강화 선택지 두 가지는 항상 후보에 포함시킨다.
            // 맨 아래 줄에는 "지금 값 → 고른 뒤 값"을 보여준다. (playerStats 연결이 없으면 비워 둔다)
            pool.Add(new LevelUpOption
            {
                Kind = LevelUpOptionKind.StatBoost,
                StatBoost = StatBoostKind.MoveSpeed,
                Title = "이동 속도 +6%",
                Description = "더 빠르게 움직여서\n적을 피하기 쉬워져요.",
                TagText = "능력치",
                ChangeText = playerStats != null ? $"{playerStats.MoveSpeed:0.0} → {playerStats.MoveSpeed * 1.06f:0.0}" : ""
            });
            pool.Add(new LevelUpOption
            {
                Kind = LevelUpOptionKind.StatBoost,
                StatBoost = StatBoostKind.MaxHealth,
                Title = "최대 체력 +12",
                Description = "더 오래 버틸 수 있어요.\n늘어난 만큼 체력도 채워져요.",
                TagText = "능력치",
                ChangeText = playerStats != null ? $"{playerStats.MaxHealth:0} → {playerStats.MaxHealth + 12f:0}" : ""
            });

            return pool;
        }

        // 플레이어가 선택지 버튼 중 하나를 눌렀을 때 호출되는 함수.
        private void ChooseOption(int index)
        {
            if (index < 0 || index >= currentOptions.Count) return;

            var option = currentOptions[index];

            // 선택지 종류에 따라 실제로 적용할 내용을 분기 처리.
            // droneManager/playerStats 연결이 빠져있으면 조용히 건너뛴다(적용은 안 되지만
            // 최소한 게임이 멈추지는 않는다).
            switch (option.Kind)
            {
                case LevelUpOptionKind.NewDrone:
                    droneManager?.AddDrone(option.DroneType);
                    break;
                case LevelUpOptionKind.UpgradeDrone:
                    droneManager?.UpgradeDrone(option.DroneType);
                    break;
                case LevelUpOptionKind.StatBoost:
                    if (playerStats != null)
                    {
                        if (option.StatBoost == StatBoostKind.MoveSpeed)
                        {
                            // 현재 이동속도의 6%만큼을 더해준다(고정값이 아니라 비율 증가).
                            playerStats.AddMoveSpeed(playerStats.MoveSpeed * 0.06f);
                        }
                        else
                        {
                            playerStats.AddMaxHealth(12f);
                        }
                    }
                    break;
            }

            // 나중에 콘솔에서 "몇 초에 어떤 선택을 골랐는지" 순서대로 복기할 수 있도록 기록해둔다.
            // (기록 형식은 예전과 같게 유지한다: 테스트 기록의 선택 요약이 이 이름을 읽는다)
            float elapsed = GameManager.Instance != null ? GameManager.Instance.ElapsedTime : 0f;
            Debug.Log($"[LevelUp] {elapsed:F0}초 - \"{LogTitle(option)}\" 선택");

            // 웹 빌드의 테스트 기록에도 남도록, 어떤 종류를 골랐는지 센다. (판 결과 기록 끝에 요약이 붙는다)
            LevelUpPickLog.Record(option, elapsed);

            AudioManager.Instance?.PlaySfx(chooseSound);

            // 선택이 끝났으니 패널을 끄고, 멈춰뒀던 시간을 다시 흐르게 한다.
            if (panel != null) panel.SetActive(false);
            Time.timeScale = 1f;

            // 미니 보스 보상 선택이 밀려 있었다면 이어서 하나 더 보여준다.
            if (pendingBonusChoices > 0)
            {
                pendingBonusChoices--;
                ShowOptions(true);
            }
        }

        // 콘솔 기록용 이름. 화면 제목은 이제 "근접 드론"처럼 종류 표시가 빠졌으므로, 기록에는 예전처럼 종류를 붙여 남긴다.
        private static string LogTitle(LevelUpOption option)
        {
            switch (option.Kind)
            {
                case LevelUpOptionKind.NewDrone: return $"신규 드론: {option.DroneType}";
                case LevelUpOptionKind.UpgradeDrone: return $"강화: {option.DroneType}";
                default: return option.Title;
            }
        }
    }
}
