using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 로비의 마일스톤 창. 위쪽에서 스테이지를 고르고, 그 스테이지의 3분 / 6분 / 처음 클리어 세 칸을 보여준다.
    // 칸을 누르면 아래에 보상과 달성 조건이 나오고, 달성한 칸에서 "획득"을 누르면 보상을 받는다.
    // 마일스톤은 스테이지마다 따로 한 번씩 받는다. 달성은 판에서 자동으로 기록된다(GameManager).
    // 노멀 스테이지는 코어를, 하드 스테이지는 만능 조각을 준다. 하드 스테이지가 있으면(스위치가 켜졌으면) 오른쪽 위에 "노멀 / 하드" 전환 버튼이 생긴다.
    // (탕탕특공대의 "챕터 보물상자" 화면처럼: 단계 칸 선택 → 보상 확인 → 획득)
    //
    // 구조: 이 스크립트가 붙은 팝업 뿌리 아래에 Box/StageTab0~, Box/Tab0~Tab2 (각각 Text, StatusText), Box/RewardText,
    // Box/ConditionText, Box/BtnClaim, Box/BtnClose 가 있어야 한다. (LobbySceneBuilder가 만든다).
    // 못 찾아도 경고만 남기고 나머지는 계속 동작한다.
    public class MilestonePopup : MonoBehaviour
    {
        [SerializeField] private AudioClip clickSound;

        private static readonly Color CyanColor = new Color(0.310f, 0.847f, 0.910f);
        private static readonly Color DarkTextColor = new Color(0.043f, 0.055f, 0.078f);
        private static readonly Color LightTextColor = new Color(0.890f, 0.961f, 0.961f);
        private static readonly Color MutedTextColor = new Color(0.588f, 0.706f, 0.714f);
        private static readonly Color ButtonFill = new Color(0.086f, 0.106f, 0.149f);

        private const string NormalHint = "스테이지마다 한 번씩 받을 수 있어요. 판에서 달성하면 여기서 코어를 받아요.";
        private const string HardHint = "하드는 코어 대신 만능 조각을 받아요. 조각은 다음 강화에 가장 가까운 드론에 자동으로 들어가요.";

        // 스테이지 고르는 칸 (위쪽 한 줄).
        private Image[] _stageImages;
        private Text[] _stageLabels;

        // 마일스톤 칸 (3분 / 6분 / 클리어).
        private Image[] _tabImages;
        private Text[] _tabLabels;
        private Text[] _tabStatuses;

        private Text _rewardText;
        private Text _conditionText;
        private Text _hintText;
        private Button _claimButton;
        private Image _claimImage;
        private Text _claimLabel;

        // 노멀 / 하드 전환 버튼 ([0] 노멀, [1] 하드). 하드 스테이지가 있을 때만 만든다. 스테이지 칸 모양을 복사해서 만든다.
        private Image[] _modeImages;
        private Text[] _modeLabels;

        // 지금 보고 있는 스테이지(전체 목록 위치, 0부터)와 마일스톤 칸 번호.
        private int _stage;
        private int _selected;

        // 스테이지 칸 묶음의 시작 위치. 노멀 = 0, 하드 = 노멀 개수. 스테이지 칸 s는 실제 스테이지 (_base + s)를 가리킨다.
        private int _base;

        private void Awake()
        {
            // 스테이지 칸: Box/StageTab0, StageTab1, ... 이름이 있는 만큼 찾는다.
            int stageCount = 0;
            while (stageCount < 16 && transform.Find($"Box/StageTab{stageCount}") != null) stageCount++;
            _stageImages = new Image[stageCount];
            _stageLabels = new Text[stageCount];
            for (int i = 0; i < stageCount; i++)
            {
                var tab = transform.Find($"Box/StageTab{i}");
                _stageImages[i] = tab.GetComponent<Image>();
                var label = tab.Find("Text");
                if (label != null) _stageLabels[i] = label.GetComponent<Text>();

                var button = tab.GetComponent<Button>();
                int captured = i;
                if (button != null) button.onClick.AddListener(() => SelectStage(captured));
            }
            if (stageCount == 0) Debug.LogWarning("[Milestone] 마일스톤 창에서 'Box/StageTab0'을 찾지 못했습니다.");

            int count = MatchMilestones.Count;
            _tabImages = new Image[count];
            _tabLabels = new Text[count];
            _tabStatuses = new Text[count];

            for (int i = 0; i < count; i++)
            {
                var tab = transform.Find($"Box/Tab{i}");
                if (tab == null)
                {
                    Debug.LogWarning($"[Milestone] 마일스톤 창에서 'Box/Tab{i}'를 찾지 못했습니다.");
                    continue;
                }

                _tabImages[i] = tab.GetComponent<Image>();
                var label = tab.Find("Text");
                if (label != null) _tabLabels[i] = label.GetComponent<Text>();
                var status = tab.Find("StatusText");
                if (status != null) _tabStatuses[i] = status.GetComponent<Text>();

                var button = tab.GetComponent<Button>();
                int captured = i;
                if (button != null) button.onClick.AddListener(() => Select(captured));
            }

            var reward = transform.Find("Box/RewardText");
            if (reward != null) _rewardText = reward.GetComponent<Text>();
            var condition = transform.Find("Box/ConditionText");
            if (condition != null) _conditionText = condition.GetComponent<Text>();
            var hint = transform.Find("Box/HintText");
            if (hint != null) _hintText = hint.GetComponent<Text>();

            var claim = transform.Find("Box/BtnClaim");
            if (claim != null)
            {
                _claimButton = claim.GetComponent<Button>();
                _claimImage = claim.GetComponent<Image>();
                var claimText = claim.Find("Text");
                if (claimText != null) _claimLabel = claimText.GetComponent<Text>();
                if (_claimButton != null) _claimButton.onClick.AddListener(Claim);
            }
            else Debug.LogWarning("[Milestone] 마일스톤 창에서 'Box/BtnClaim' 버튼을 찾지 못했습니다.");

            var close = transform.Find("Box/BtnClose");
            var closeButton = close != null ? close.GetComponent<Button>() : null;
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            else Debug.LogWarning("[Milestone] 마일스톤 창에서 'Box/BtnClose' 버튼을 찾지 못했습니다.");
        }

        // 창이 열릴 때마다 최신 상태로 다시 그린다.
        // 받을 수 있는 칸이 있는 스테이지를 먼저 보여주고, 없으면 지금 고른 스테이지를 보여준다.
        private void OnEnable()
        {
            EnsureModeButtons();
            _stage = FindStageToShow();
            _base = _stage >= HardBase ? HardBase : 0;
            _selected = FindFirstClaimable(_stage);
            Refresh();
        }

        public void Open() => gameObject.SetActive(true);

        private void Close()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            gameObject.SetActive(false);
        }

        // 스테이지 칸의 개수 (한 묶음).
        private int StageCount => _stageImages != null ? _stageImages.Length : 0;

        // 하드 스테이지 묶음이 시작되는 위치 (= 노멀 스테이지 개수).
        private static int HardBase => MatchMilestones.NormalCount;

        // 하드 스테이지가 있는지. (HardStageSettings 스위치가 켜져 있을 때만 StageProgress에 하드가 붙는다)
        private static bool HasHard => StageProgress.Instance != null && StageProgress.Instance.HasHardStages;

        // 보여 줄 수 있는 스테이지 전체 개수 (노멀 묶음 + 있으면 하드 묶음).
        private int TotalStages => HasHard ? HardBase + StageCount : StageCount;

        // 노멀 / 하드 전환 버튼을 (처음 한 번) 만든다. 하드 스테이지가 없으면 만들지 않는다.
        private void EnsureModeButtons()
        {
            if (_modeImages != null || !HasHard || StageCount == 0) return;
            var template = transform.Find("Box/StageTab0");
            if (template == null) return;

            _modeImages = new Image[2];
            _modeLabels = new Text[2];
            string[] names = { "노멀", "하드" };
            for (int m = 0; m < 2; m++)
            {
                // 스테이지 칸을 복사해서 같은 색·글꼴·테두리를 쓴다. 클릭 연결은 복사본에서 지우고 새로 단다.
                var copy = Instantiate(template.gameObject, template.parent);
                copy.name = $"ModeTab{m}";
                var button = copy.GetComponent<Button>();
                button.onClick.RemoveAllListeners();
                bool hard = m == 1;
                button.onClick.AddListener(() => SetMode(hard));

                var rect = (RectTransform)copy.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(150f, 60f);
                rect.anchoredPosition = new Vector2(-30f - (1 - m) * 160f, -30f);

                _modeImages[m] = copy.GetComponent<Image>();
                var label = copy.transform.Find("Text");
                if (label != null)
                {
                    _modeLabels[m] = label.GetComponent<Text>();
                    _modeLabels[m].text = names[m];
                }
            }
        }

        // 달성했지만 아직 안 받은 마일스톤이 있는 첫 스테이지. 없으면 지금 고른(플레이 중인) 스테이지.
        private int FindStageToShow()
        {
            var data = SaveManager.Data;
            for (int s = 0; s < TotalStages; s++)
                if (HasClaimable(data, s)) return s;

            int current = StageProgress.Instance != null ? StageProgress.Instance.SelectedIndex : 0;
            return Mathf.Clamp(current, 0, Mathf.Max(0, TotalStages - 1));
        }

        private static bool HasClaimable(SaveData data, int stage)
        {
            for (int i = 0; i < MatchMilestones.Count; i++)
                if (MatchMilestones.IsDone(data, stage, i) && !MatchMilestones.IsClaimed(data, stage, i)) return true;
            return false;
        }

        // 노멀 묶음 / 하드 묶음 안에 받을 수 있는 칸이 있는 스테이지가 하나라도 있는지. (전환 버튼 옆의 ● 표시용)
        private bool GroupHasClaimable(SaveData data, int groupBase)
        {
            for (int s = 0; s < StageCount; s++)
                if (HasClaimable(data, groupBase + s)) return true;
            return false;
        }

        // 달성했지만 아직 안 받은 첫 칸. 없으면 아직 안 받은 첫 칸, 그것도 없으면 0번.
        private static int FindFirstClaimable(int stage)
        {
            var data = SaveManager.Data;
            for (int i = 0; i < MatchMilestones.Count; i++)
                if (MatchMilestones.IsDone(data, stage, i) && !MatchMilestones.IsClaimed(data, stage, i)) return i;
            for (int i = 0; i < MatchMilestones.Count; i++)
                if (!MatchMilestones.IsClaimed(data, stage, i)) return i;
            return 0;
        }

        // 스테이지 칸 s를 눌렀을 때. 지금 묶음 안의 s번째 스테이지로 간다.
        private void SelectStage(int tab)
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            _stage = _base + tab;
            _selected = FindFirstClaimable(_stage);
            Refresh();
        }

        // 노멀 / 하드 묶음을 바꾼다. 같은 위치의 스테이지 칸(노멀 3 ↔ 하드 3)으로 옮겨 간다.
        private void SetMode(bool hard)
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            int newBase = hard ? HardBase : 0;
            if (newBase == _base) return;

            _stage = newBase + (_stage - _base);
            _base = newBase;
            _selected = FindFirstClaimable(_stage);
            Refresh();
        }

        private void Select(int index)
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            _selected = index;
            Refresh();
        }

        private void Claim()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            if (CurrencyManager.Instance == null)
            {
                Debug.LogWarning("[Milestone] CurrencyManager가 없어 마일스톤 보상을 지급하지 못했습니다. 메인 메뉴 씬부터 시작했는지 확인해주세요.");
                return;
            }

            CurrencyManager.Instance.TryClaimMilestone(_stage, _selected);
            Refresh();
        }

        // 스테이지 칸과 마일스톤 칸의 상태를 보여주고(미달성 / 받기 / 받음), 고른 칸의 보상과 조건, 획득 버튼 상태를 그린다.
        private void Refresh()
        {
            var data = SaveManager.Data;
            bool hardMode = _base >= HardBase && HasHard;

            // 노멀 / 하드 전환 버튼: 보고 있는 쪽은 시안색, 반대쪽에 받을 수 있는 보상이 있으면 이름 옆에 점을 붙인다.
            if (_modeImages != null)
            {
                for (int m = 0; m < 2; m++)
                {
                    bool active = (m == 1) == hardMode;
                    if (_modeImages[m] != null) _modeImages[m].color = active ? CyanColor : ButtonFill;
                    if (_modeLabels[m] != null)
                    {
                        string name = m == 1 ? "하드" : "노멀";
                        _modeLabels[m].text = GroupHasClaimable(data, m == 1 ? HardBase : 0) ? $"{name} ●" : name;
                        _modeLabels[m].color = active ? DarkTextColor : LightTextColor;
                    }
                }
            }

            if (_hintText != null) _hintText.text = hardMode ? HardHint : NormalHint;

            // 스테이지 칸: 고른 스테이지는 시안색, 받을 수 있는 보상이 있는 스테이지는 이름 옆에 점을 붙인다.
            for (int s = 0; s < StageCount; s++)
            {
                int stage = _base + s;
                bool selectedStage = stage == _stage;
                if (_stageImages[s] != null) _stageImages[s].color = selectedStage ? CyanColor : ButtonFill;
                if (_stageLabels[s] != null)
                {
                    string label = MatchMilestones.StageLabel(stage);
                    _stageLabels[s].text = HasClaimable(data, stage) ? $"{label} ●" : label;
                    _stageLabels[s].color = selectedStage ? DarkTextColor : LightTextColor;
                }
            }

            for (int i = 0; i < MatchMilestones.Count; i++)
            {
                bool done = MatchMilestones.IsDone(data, _stage, i);
                bool claimed = MatchMilestones.IsClaimed(data, _stage, i);
                bool selected = i == _selected;

                if (_tabImages[i] != null) _tabImages[i].color = selected ? CyanColor : ButtonFill;
                if (_tabLabels[i] != null) _tabLabels[i].color = selected ? DarkTextColor : LightTextColor;
                if (_tabStatuses[i] != null)
                {
                    _tabStatuses[i].text = claimed ? "받음" : done ? "받기" : "미달성";
                    _tabStatuses[i].color = selected ? DarkTextColor : (done && !claimed ? CyanColor : MutedTextColor);
                }
            }

            bool selectedDone = MatchMilestones.IsDone(data, _stage, _selected);
            bool selectedClaimed = MatchMilestones.IsClaimed(data, _stage, _selected);

            if (_rewardText != null)
            {
                bool shardReward = MatchMilestones.IsHardStage(_stage);
                _rewardText.text = shardReward
                    ? $"만능 조각 +{MatchMilestones.GetShards(_stage, _selected)}"
                    : $"코어 +{MatchMilestones.GetCore(_stage, _selected)}";

                // 글자 옆의 코어 아이콘은 코어 보상일 때만 보인다. (조각 보상에 코어 아이콘이 붙으면 헷갈린다)
                var rewardIcon = _rewardText.transform.Find("Icon");
                if (rewardIcon != null) rewardIcon.gameObject.SetActive(!shardReward);
            }
            if (_conditionText != null) _conditionText.text = $"{MatchMilestones.StageLabel(_stage)} · {MatchMilestones.Conditions[_selected]}";

            bool canClaim = selectedDone && !selectedClaimed;
            if (_claimButton != null) _claimButton.interactable = canClaim;
            if (_claimImage != null) _claimImage.color = canClaim ? CyanColor : ButtonFill;
            if (_claimLabel != null)
            {
                _claimLabel.text = selectedClaimed ? "수령 완료" : selectedDone ? "획득" : "아직 달성 못함";
                _claimLabel.color = canClaim ? DarkTextColor : MutedTextColor;
            }
        }
    }
}
