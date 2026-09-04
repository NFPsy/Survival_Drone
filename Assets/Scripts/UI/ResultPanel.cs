using UnityEngine;
using UnityEngine.UI;
using SurvivalDrone.Core;

namespace SurvivalDrone.UI
{
    // 게임이 끝났을 때(승리 또는 패배) 결과 화면을 보여주는 스크립트.
    public class ResultPanel : MonoBehaviour
    {
        // 결과 화면 전체 패널 (평소엔 꺼져 있다가 게임이 끝나면 켜짐).
        [SerializeField] private GameObject panel;

        // "승리" 또는 "패배" 문구를 보여줄 텍스트.
        [SerializeField] private Text resultText;

        // 게임 상태(진행중/승리/패배)가 바뀌는 것을 감지하기 위한 GameManager 연결.
        [SerializeField] private GameManager gameManager;

        private void OnEnable()
        {
            // 게임 상태가 바뀔 때마다 HandleStateChanged가 자동으로 호출되도록 연결.
            if (gameManager != null) gameManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            // 오브젝트가 사라질 때는 반드시 구독을 해제한다.
            if (gameManager != null) gameManager.OnStateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            // 게임 시작 시에는 결과 화면을 꺼둔다.
            if (panel != null) panel.SetActive(false);
        }

        // 게임 상태가 바뀔 때 호출되는 함수.
        private void HandleStateChanged(MatchState state)
        {
            // 아직 "진행 중" 상태면(=게임이 끝난 게 아니면) 아무것도 하지 않는다.
            if (state == MatchState.Playing) return;

            // 게임이 끝났으면(승리 또는 패배) 결과 패널을 켜고 알맞은 문구를 표시한다.
            if (panel != null) panel.SetActive(true);
            if (resultText != null)
            {
                resultText.text = state == MatchState.Won ? "MISSION COMPLETE" : "SYSTEM DOWN";
            }
        }
    }
}
