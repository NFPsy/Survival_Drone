using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 로비의 일일 퀘스트 창. 퀘스트 4개(출석하기 / 3분 버티기 / 6분 버티기 / 클리어하기)의 진행 상태를 보여주고,
    // 달성한 퀘스트의 코어를 "받기" 버튼으로 받게 해준다. 달성은 판(GameManager)과 로비 입장(LobbyUI)에서 자동으로 기록된다.
    //
    // 구조: 이 스크립트가 붙은 팝업 뿌리 아래에 Box/Row0~Row3 (각각 TitleText, RewardText, BtnClaim)와 Box/BtnClose 가 있어야 한다.
    // 위에서 i번째 줄(Row i)에는 DailyQuests.DisplayOrder[i]번 퀘스트가 온다.
    // (LobbySceneBuilder가 만든다). 못 찾아도 경고만 남기고 나머지는 계속 동작한다.
    public class DailyQuestPopup : MonoBehaviour
    {
        [SerializeField] private AudioClip clickSound;

        private static readonly Color CyanColor = new Color(0.310f, 0.847f, 0.910f);
        private static readonly Color DarkTextColor = new Color(0.043f, 0.055f, 0.078f);
        private static readonly Color LightTextColor = new Color(0.890f, 0.961f, 0.961f);
        private static readonly Color MutedTextColor = new Color(0.588f, 0.706f, 0.714f);
        private static readonly Color ButtonFill = new Color(0.086f, 0.106f, 0.149f);

        private Button[] _claimButtons;
        private Text[] _claimLabels;
        private Image[] _claimImages;
        private Text[] _titleTexts;

        private void Awake()
        {
            int count = DailyQuests.Count;
            _claimButtons = new Button[count];
            _claimLabels = new Text[count];
            _claimImages = new Image[count];
            _titleTexts = new Text[count];

            for (int i = 0; i < count; i++)
            {
                var row = transform.Find($"Box/Row{i}");
                if (row == null)
                {
                    Debug.LogWarning($"[Quest] 일일 퀘스트 창에서 'Box/Row{i}'를 찾지 못했습니다.");
                    continue;
                }

                var title = row.Find("TitleText");
                if (title != null) _titleTexts[i] = title.GetComponent<Text>();

                // 위에서 i번째 줄에는 DisplayOrder[i]번 퀘스트가 온다. 아래 눌림·상태 표시는 모두 이 퀘스트 번호로 처리한다.
                int quest = DailyQuests.DisplayOrder[i];

                var reward = row.Find("RewardText");
                if (reward != null) reward.GetComponent<Text>().text = $"코어 +{DailyQuests.Cores[quest]}";

                var claim = row.Find("BtnClaim");
                if (claim == null) continue;
                _claimButtons[i] = claim.GetComponent<Button>();
                _claimImages[i] = claim.GetComponent<Image>();
                _claimLabels[i] = claim.Find("Text") != null ? claim.Find("Text").GetComponent<Text>() : null;

                if (_claimButtons[i] != null) _claimButtons[i].onClick.AddListener(() => Claim(quest));
            }

            var close = transform.Find("Box/BtnClose");
            var closeButton = close != null ? close.GetComponent<Button>() : null;
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            else Debug.LogWarning("[Quest] 일일 퀘스트 창에서 'Box/BtnClose' 버튼을 찾지 못했습니다.");
        }

        // 창이 열릴 때마다 오늘 기준의 최신 상태로 다시 그린다. (날짜가 바뀌었으면 기록이 새로 시작된다)
        private void OnEnable() => Refresh();

        public void Open() => gameObject.SetActive(true);

        private void Close()
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            gameObject.SetActive(false);
        }

        private void Claim(int index)
        {
            AudioManager.Instance?.PlaySfx(clickSound);
            if (CurrencyManager.Instance == null)
            {
                Debug.LogWarning("[Quest] CurrencyManager가 없어 퀘스트 보상을 지급하지 못했습니다. 메인 메뉴 씬부터 시작했는지 확인해주세요.");
                return;
            }

            CurrencyManager.Instance.TryClaimDailyQuest(index);
            Refresh();
        }

        // 퀘스트마다 버튼 상태를 바꾼다: 진행 중(눌러도 안 됨) → 받기(눌러서 받음) → 받음(끝).
        private void Refresh()
        {
            var data = SaveManager.Data;
            for (int i = 0; i < DailyQuests.Count; i++)
            {
                int quest = DailyQuests.DisplayOrder[i];
                bool done = DailyQuests.IsDone(data, quest);
                bool claimed = DailyQuests.IsClaimed(data, quest);

                if (_titleTexts[i] != null) _titleTexts[i].color = claimed ? MutedTextColor : LightTextColor;
                if (_claimButtons[i] != null) _claimButtons[i].interactable = done && !claimed;
                if (_claimLabels[i] != null)
                {
                    _claimLabels[i].text = claimed ? "받음" : done ? "받기" : "진행 중";
                    _claimLabels[i].color = done && !claimed ? DarkTextColor : MutedTextColor;
                }
                // 받을 수 있을 때만 시안색으로 눈에 띄게 한다.
                if (_claimImages[i] != null) _claimImages[i].color = done && !claimed ? CyanColor : ButtonFill;
            }
        }
    }
}
