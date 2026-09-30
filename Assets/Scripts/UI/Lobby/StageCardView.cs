using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Meta;

namespace SurvivalDrone.UI
{
    // 스테이지 "카드" 하나에 정보를 채워 넣는 도우미.
    // 로비의 큰 카드와 스테이지 선택 화면의 작은 카드 3장이 똑같은 글자 칸을 가지고 있어서,
    // 채우는 코드를 여기 한 곳에 모아 두었다.
    //
    // 카드 안에는 이름이 정해진 글자 칸(Text)이 있어야 한다:
    //   StageNumberText, StageNameText, PowerText, MyPowerText, MultiplierText, BestTimeText, LockText
    // 없는 칸은 경고 로그만 남기고 건너뛴다 (화면 하나가 잘못돼도 게임 전체가 멈추지 않게).
    public class StageCardView
    {
        private static readonly Color NormalTitleColor = new Color(0.310f, 0.847f, 0.910f);   // 시안: 성장/주요 강조
        private static readonly Color NormalTextColor = new Color(0.890f, 0.961f, 0.961f);    // 밝은 글자
        private static readonly Color MutedColor = new Color(0.588f, 0.706f, 0.714f);         // 흐린 글자
        private static readonly Color LockedColor = new Color(0.35f, 0.40f, 0.45f);           // 잠긴 카드의 죽은 글자색
        private static readonly Color WarningColor = new Color(1f, 0.62f, 0.2f);              // 주황: 잠금 조건 문구, 전투력이 빠듯할 때
        private static readonly Color EnoughColor = new Color(0.35f, 0.90f, 0.50f);           // 초록: 전투력이 권장 이상
        private static readonly Color DangerColor = new Color(1f, 0.35f, 0.35f);              // 빨강: 전투력이 권장의 70% 미만

        private readonly Text _numberText;
        private readonly Text _nameText;
        private readonly Text _powerText;
        private readonly Text _myPowerText;
        private readonly Text _multiplierText;
        private readonly Text _bestTimeText;
        private readonly Text _lockText;

        public StageCardView(Transform cardRoot)
        {
            _numberText = FindText(cardRoot, "StageNumberText");
            _nameText = FindText(cardRoot, "StageNameText");
            _powerText = FindText(cardRoot, "PowerText");
            _myPowerText = FindText(cardRoot, "MyPowerText");
            _multiplierText = FindText(cardRoot, "MultiplierText");
            _bestTimeText = FindText(cardRoot, "BestTimeText");
            _lockText = FindText(cardRoot, "LockText");
        }

        // 스테이지 정보를 카드에 표시한다.
        //  - bestSeconds: 최고 생존 시간(초), 기록이 없으면 0
        //  - unlocked: 해금 여부. 잠겼으면 흐리게 표시하고 "STAGE n 클리어 필요" 문구를 주황으로 보여준다.
        //  - myPower: 내 전투력(장착한 드론 합계). 권장 전투력과 비교해서 색이 달라진다.
        public void Show(StageData stage, float bestSeconds, bool unlocked, int myPower)
        {
            if (stage == null) return;

            SetText(_numberText, $"STAGE {stage.StageNumber}", unlocked ? NormalTitleColor : LockedColor);
            SetText(_nameText, stage.DisplayName, unlocked ? NormalTextColor : LockedColor);
            SetText(_powerText, $"권장 전투력  {stage.RecommendedPower:N0}", unlocked ? NormalTextColor : LockedColor);
            SetText(_myPowerText, $"내 전투력  {myPower:N0}", unlocked ? GetPowerColor(myPower, stage.RecommendedPower) : LockedColor);
            SetText(_multiplierText, $"적 체력·피해  x{stage.EnemyMultiplier:0.0}", unlocked ? MutedColor : LockedColor);
            SetText(_bestTimeText, $"최고 생존  {FormatTime(bestSeconds)}", unlocked ? MutedColor : LockedColor);

            if (_lockText != null)
            {
                _lockText.gameObject.SetActive(!unlocked);
                _lockText.text = $"STAGE {stage.StageNumber - 1} 클리어 필요";
                _lockText.color = WarningColor;
            }
        }

        // 내 전투력이 권장 전투력의 몇 %인지에 따라 색을 정한다.
        //  100% 이상 = 초록(충분), 70~100% = 주황(빠듯함), 70% 미만 = 빨강(위험)
        public static Color GetPowerColor(int myPower, int recommendedPower)
        {
            if (recommendedPower <= 0) return NormalTextColor;
            float ratio = (float)myPower / recommendedPower;
            if (ratio >= 1f) return EnoughColor;
            if (ratio >= 0.7f) return WarningColor;
            return DangerColor;
        }

        // 초 → "m:ss". 기록이 없으면(0 이하) "--:--".
        public static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return "--:--";
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60}:{total % 60:00}";
        }

        private static Text FindText(Transform root, string name)
        {
            var child = root != null ? root.Find(name) : null;
            var text = child != null ? child.GetComponent<Text>() : null;
            if (text == null)
                Debug.LogWarning($"[Stage] 스테이지 카드 '{(root != null ? root.name : "null")}'에서 '{name}' 글자 칸을 찾지 못했습니다.");
            return text;
        }

        private static void SetText(Text target, string value, Color color)
        {
            if (target == null) return;
            target.text = value;
            target.color = color;
        }
    }
}
