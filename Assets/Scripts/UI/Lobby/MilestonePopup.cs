using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 로비의 마일스톤 창. 위쪽에서 스테이지를 고르고, 그 스테이지의 3분 / 6분 / 처음 클리어 세 칸을 보여준다.
    // 칸을 누르면 아래에 보상과 달성 조건이 나오고, 달성한 칸에서 "획득"을 누르면 코어를 받는다.
    // 마일스톤은 스테이지마다 따로 한 번씩 받는다. 달성은 판에서 자동으로 기록된다(GameManager).
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

        // 스테이지 고르는 칸 (위쪽 한 줄).
        private Image[] _stageImages;
        private Text[] _stageLabels;

        // 마일스톤 칸 (3분 / 6분 / 클리어).
        private Image[] _tabImages;
        private Text[] _tabLabels;
        private Text[] _tabStatuses;

        private Text _rewardText;
        private Text _conditionText;
        private Button _claimButton;
        private Image _claimImage;
        private Text _claimLabel;

        // 지금 보고 있는 스테이지(0부터)와 마일스톤 칸 번호.
        private int _stage;
        private int _selected;

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
            _stage = FindStageToShow();
            _selected = FindFirstClaimable(_stage);
            Refresh();
        }

        public void Open() => gameObject.SetActive(true);

        private void Close()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            gameObject.SetActive(false);
        }

        private int StageCount => _stageImages != null ? _stageImages.Length : 0;

        // 달성했지만 아직 안 받은 마일스톤이 있는 첫 스테이지. 없으면 지금 고른(플레이 중인) 스테이지.
        private int FindStageToShow()
        {
            var data = SaveManager.Data;
            for (int s = 0; s < StageCount; s++)
                if (HasClaimable(data, s)) return s;

            int current = StageProgress.Instance != null ? StageProgress.Instance.SelectedIndex : 0;
            return Mathf.Clamp(current, 0, Mathf.Max(0, StageCount - 1));
        }

        private static bool HasClaimable(SaveData data, int stage)
        {
            for (int i = 0; i < MatchMilestones.Count; i++)
                if (MatchMilestones.IsDone(data, stage, i) && !MatchMilestones.IsClaimed(data, stage, i)) return true;
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

        private void SelectStage(int stage)
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            _stage = stage;
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

            // 스테이지 칸: 고른 스테이지는 시안색, 받을 수 있는 코어가 있는 스테이지는 이름 옆에 점을 붙인다.
            for (int s = 0; s < StageCount; s++)
            {
                bool selectedStage = s == _stage;
                if (_stageImages[s] != null) _stageImages[s].color = selectedStage ? CyanColor : ButtonFill;
                if (_stageLabels[s] != null)
                {
                    _stageLabels[s].text = HasClaimable(data, s) ? $"스테이지 {s + 1} ●" : $"스테이지 {s + 1}";
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

            if (_rewardText != null) _rewardText.text = $"코어 +{MatchMilestones.GetCore(_stage, _selected)}";
            if (_conditionText != null) _conditionText.text = $"스테이지 {_stage + 1} · {MatchMilestones.Conditions[_selected]}";

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
