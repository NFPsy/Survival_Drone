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
    //
    // 하드 스테이지(OBT 2단계, HardStageSettings 스위치가 켜졌을 때만 있음)가 있으면 오른쪽 위에 "노멀 / 하드" 탭이 생기고,
    // 선택한 탭의 스테이지 카드만 보인다. 하드 탭은 스테이지 5를 클리어하기 전에는 잠겨 있다.
    // (스위치가 꺼져 있으면 탭을 만들지 않아서 예전과 똑같이 카드 5장이 보인다)
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
            public bool isHard;
            public Button button;
            public Outline outline;
            public StageCardView view;
        }

        private readonly List<Card> _cards = new List<Card>();
        private Button _launchButton;
        private bool _built;

        // 노멀/하드 탭 (하드 스테이지가 있을 때만 만든다)
        private Button _tabNormal;
        private Button _tabHard;
        private bool _showHard;
        private static readonly Color TabSelectedColor = new Color(0.310f, 0.847f, 0.910f, 1f);
        private static readonly Color TabNormalColor = new Color(0.086f, 0.106f, 0.149f, 1f);
        private static readonly Color TabLockedColor = new Color(0.07f, 0.08f, 0.10f, 1f);

        private void Awake()
        {
            WireButton("BtnClose", () => { AudioManager.Instance?.PlaySfx(clickSound); OnCloseRequested?.Invoke(); });
            _launchButton = WireButton("BtnLaunch", () => { AudioManager.Instance?.PlaySfx(clickSound); OnLaunchRequested?.Invoke(); });
        }

        private void OnEnable()
        {
            BuildCardsIfNeeded();

            // 열 때는 지금 고른 스테이지가 있는 탭을 보여 준다.
            var openProgress = StageProgress.Instance;
            if (openProgress != null) _showHard = openProgress.IsHardStage(openProgress.SelectedIndex);

            if (StageProgress.Instance != null) StageProgress.Instance.OnSelectionChanged += HandleSelectionChanged;
            if (DroneInventory.Instance != null) DroneInventory.Instance.OnInventoryChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (StageProgress.Instance != null) StageProgress.Instance.OnSelectionChanged -= HandleSelectionChanged;
            if (DroneInventory.Instance != null) DroneInventory.Instance.OnInventoryChanged -= Refresh;
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
                    isHard = progress.IsHardStage(index),
                    button = cardObject.GetComponent<Button>(),
                    outline = cardObject.GetComponent<Outline>(),
                    view = new StageCardView(cardObject.transform),
                };
                if (card.button != null)
                    card.button.onClick.AddListener(() => OnCardClicked(index));
                _cards.Add(card);
            }

            BuildTabsIfNeeded(progress);
            _built = true;
        }

        // 하드 스테이지가 있을 때만 "노멀 / 하드" 탭 버튼 두 개를 만든다. 씬에 미리 만들어 두지 않고 코드로 만드는 이유:
        // 하드 스위치가 꺼져 있는 동안은 로비 씬(생성 코드·저장된 씬)을 전혀 건드리지 않기 위해서다.
        // 모양(색·테두리·글꼴)은 "뒤로" 버튼과 제목 글자에서 가져와 다른 버튼들과 똑같이 보이게 한다.
        private void BuildTabsIfNeeded(StageProgress progress)
        {
            if (!progress.HasHardStages) return;

            var closeButton = transform.Find("BtnClose");
            var title = transform.Find("Title");
            var font = title != null && title.GetComponent<Text>() != null ? title.GetComponent<Text>().font : null;
            var outline = closeButton != null ? closeButton.GetComponent<Outline>() : null;

            _tabNormal = MakeTab("BtnTabNormal", "노멀", -290f, font, outline);
            _tabHard = MakeTab("BtnTabHard", "하드", -40f, font, outline);
            _tabNormal.onClick.AddListener(() => ShowTab(false));
            _tabHard.onClick.AddListener(() => ShowTab(true));
        }

        // 탭 버튼 하나를 만든다. 오른쪽 위 모서리에서 offsetX만큼 왼쪽에 놓는다.
        private Button MakeTab(string objectName, string label, float offsetX, Font font, Outline referenceOutline)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(offsetX, -40f);
            rect.sizeDelta = new Vector2(230f, 64f);

            var image = go.GetComponent<Image>();
            image.color = TabNormalColor;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            if (referenceOutline != null)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = referenceOutline.effectColor;
                outline.effectDistance = referenceOutline.effectDistance;
            }

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(go.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.890f, 0.961f, 0.961f, 1f);
            text.text = label;
            text.raycastTarget = false;
            return button;
        }

        // 탭을 눌렀을 때: 그 탭의 스테이지만 보이게 하고, 그 탭에서 해금된 가장 어려운 스테이지를 고른다.
        private void ShowTab(bool hard)
        {
            var progress = StageProgress.Instance;
            if (progress == null) return;

            AudioManager.Instance?.PlaySfx(clickSound);
            _showHard = hard;

            // 고른 스테이지가 이 탭 안에 있게 한다. 안 그러면 출격 버튼은 켜져 있는데 고른 카드가 안 보이는 상태가 된다.
            if (progress.IsHardStage(progress.SelectedIndex) != hard)
            {
                for (int i = progress.StageCount - 1; i >= 0; i--)
                {
                    if (progress.IsHardStage(i) == hard && progress.IsUnlocked(i))
                    {
                        progress.TrySelect(i); // 선택이 바뀌면 HandleSelectionChanged가 다시 그려 준다
                        break;
                    }
                }
            }
            Refresh();
        }

        private void OnCardClicked(int index)
        {
            var progress = StageProgress.Instance;
            if (progress == null) return;

            AudioManager.Instance?.PlaySfx(clickSound);
            // 잠긴 카드는 TrySelect가 거절하므로 아무 일도 일어나지 않는다.
            progress.TrySelect(index);
        }

        private void HandleSelectionChanged(int selectedIndex)
        {
            // 다른 곳(로비의 <  > 버튼 등)에서 선택이 바뀌어도 고른 스테이지가 있는 탭을 보여 준다.
            if (StageProgress.Instance != null) _showHard = StageProgress.Instance.IsHardStage(selectedIndex);
            Refresh();
        }

        // 모든 카드의 내용과 선택 테두리를 현재 상태에 맞게 다시 그린다.
        private void Refresh()
        {
            var progress = StageProgress.Instance;
            if (progress == null) return;

            int myPower = DroneInventory.Instance != null ? DroneInventory.Instance.TotalCombatPower : 0;
            RefreshTabs(progress);

            foreach (var card in _cards)
            {
                // 하드 스테이지가 있으면 선택한 탭의 카드만 보인다. (하드가 없으면 모든 카드가 노멀이고 탭도 없다)
                bool visible = !progress.HasHardStages || card.isHard == _showHard;
                if (card.button != null && card.button.gameObject.activeSelf != visible) card.button.gameObject.SetActive(visible);

                bool unlocked = progress.IsUnlocked(card.index);
                card.view.Show(progress.GetStage(card.index), progress.GetBestSurvivalSeconds(card.index), unlocked, myPower);

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

        // 탭 버튼의 색과 글자를 현재 상태에 맞게 바꾼다. 하드 탭은 첫 하드 스테이지가 잠겨 있으면 눌리지 않는다.
        private void RefreshTabs(StageProgress progress)
        {
            if (_tabNormal == null || _tabHard == null) return;

            bool hardUnlocked = progress.IsUnlocked(progress.FirstHardIndex);
            SetTabLook(_tabNormal, !_showHard, true, "노멀");
            SetTabLook(_tabHard, _showHard, hardUnlocked, hardUnlocked ? "하드" : "하드 (잠김)");
        }

        private static void SetTabLook(Button tab, bool selected, bool unlocked, string label)
        {
            tab.interactable = unlocked;
            var image = tab.GetComponent<Image>();
            if (image != null) image.color = selected ? TabSelectedColor : (unlocked ? TabNormalColor : TabLockedColor);
            var text = tab.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
                text.color = selected ? new Color(0.04f, 0.06f, 0.08f, 1f) : (unlocked ? new Color(0.890f, 0.961f, 0.961f, 1f) : new Color(0.35f, 0.40f, 0.45f, 1f));
            }
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
