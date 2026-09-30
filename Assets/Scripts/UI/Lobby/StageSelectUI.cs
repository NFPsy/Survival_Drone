using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 스테이지 선택 화면 (로비 위에 뜨는 팝업 패널). 스테이지 카드를 나란히 보여주고 하나를 고르게 한다.
    //
    // 구조: 이 스크립트가 붙은 패널 아래에
    //   CardContainer (카드들이 가로로 늘어설 자리) / CardTemplate (복제할 카드 견본, 꺼져 있음) / BtnClose / BtnLaunch
    // 가 있어야 한다. StageProgress의 스테이지 개수만큼 견본을 복제해서 카드를 만든다.
    public class StageSelectUI : MonoBehaviour
    {
        // 뒤로 버튼을 눌렀을 때 / 출격 버튼을 눌렀을 때 로비에게 알린다. (씬 이동은 로비가 한 곳에서 처리)
        public event Action OnCloseRequested;
        public event Action OnLaunchRequested;

        [SerializeField] private AudioClip clickSound;

        // 선택된 카드의 테두리 색(시안)과 평소 테두리 색.
        private static readonly Color SelectedOutline = new Color(0.310f, 0.847f, 0.910f, 1f);
        private static readonly Color NormalOutline = new Color(0.300f, 0.360f, 0.460f, 0.9f);

        private class Card
        {
            public int index;
            public Button button;
            public Outline outline;
            public StageCardView view;
        }

        private readonly List<Card> _cards = new List<Card>();
        private Button _launchButton;
        private bool _built;

        private void Awake()
        {
            WireButton("BtnClose", () => { AudioManager.Instance?.PlaySfx(clickSound); OnCloseRequested?.Invoke(); });
            _launchButton = WireButton("BtnLaunch", () => { AudioManager.Instance?.PlaySfx(clickSound); OnLaunchRequested?.Invoke(); });
        }

        private void OnEnable()
        {
            BuildCardsIfNeeded();
            if (StageProgress.Instance != null) StageProgress.Instance.OnSelectionChanged += HandleSelectionChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (StageProgress.Instance != null) StageProgress.Instance.OnSelectionChanged -= HandleSelectionChanged;
        }

        // 패널이 처음 열릴 때 견본을 스테이지 개수만큼 복제해서 카드를 만든다.
        private void BuildCardsIfNeeded()
        {
            if (_built) return;

            var progress = StageProgress.Instance;
            var container = transform.Find("CardContainer");
            var template = transform.Find("CardTemplate");
            if (progress == null || container == null || template == null)
            {
                Debug.LogWarning("[Stage] 스테이지 선택 화면을 만들지 못했습니다. StageProgress, CardContainer, CardTemplate을 확인해주세요.");
                return;
            }

            for (int i = 0; i < progress.StageCount; i++)
            {
                int index = i; // 버튼 클릭 함수 안에서 i 대신 이 값을 쓴다 (반복문 변수가 계속 바뀌는 문제를 피하려고)
                var cardObject = Instantiate(template.gameObject, container);
                cardObject.name = $"Card_{i + 1}";
                cardObject.SetActive(true);

                var card = new Card
                {
                    index = index,
                    button = cardObject.GetComponent<Button>(),
                    outline = cardObject.GetComponent<Outline>(),
                    view = new StageCardView(cardObject.transform),
                };
                if (card.button != null)
                    card.button.onClick.AddListener(() => OnCardClicked(index));
                _cards.Add(card);
            }
            _built = true;
        }

        private void OnCardClicked(int index)
        {
            var progress = StageProgress.Instance;
            if (progress == null) return;

            AudioManager.Instance?.PlaySfx(clickSound);
            // 잠긴 카드는 TrySelect가 거절하므로 아무 일도 일어나지 않는다.
            progress.TrySelect(index);
        }

        private void HandleSelectionChanged(int selectedIndex) => Refresh();

        // 모든 카드의 내용과 선택 테두리를 현재 상태에 맞게 다시 그린다.
        private void Refresh()
        {
            var progress = StageProgress.Instance;
            if (progress == null) return;

            foreach (var card in _cards)
            {
                bool unlocked = progress.IsUnlocked(card.index);
                card.view.Show(progress.GetStage(card.index), progress.GetBestSurvivalSeconds(card.index), unlocked);

                if (card.outline != null)
                {
                    bool selected = card.index == progress.SelectedIndex;
                    card.outline.effectColor = selected ? SelectedOutline : NormalOutline;
                    card.outline.effectDistance = selected ? new Vector2(3f, 3f) : new Vector2(1f, 1f);
                }
            }

            // 선택된 스테이지는 항상 해금된 것이므로 출격 버튼은 켜 둔다.
            if (_launchButton != null) _launchButton.interactable = progress.IsUnlocked(progress.SelectedIndex);
        }

        private Button WireButton(string buttonName, UnityEngine.Events.UnityAction action)
        {
            var child = transform.Find(buttonName);
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogWarning($"[Stage] '{name}/{buttonName}' 버튼을 찾지 못했습니다.");
                return null;
            }
            button.onClick.AddListener(action);
            return button;
        }
    }
}
