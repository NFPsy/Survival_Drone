namespace SurvivalDrone.LevelUp
{
    // 이번 판에서 레벨업 선택지를 뭘 골랐는지 세어 두는 기록용 도우미.
    // 판이 끝나거나 중간에 나갈 때 테스트(CBT) 기록 한 줄에 "이동속도를 몇 번 골랐는지" 등을 덧붙이려고 만들었다.
    // (콘솔에만 찍히던 선택 이력이 웹 빌드 로그에는 없어서, 초반에 죽은 판이 이동속도를 안 골라서인지 알 수 없었다)
    //
    // static(어디서든 바로 부르는 방식)으로 만든 이유: 기록을 남기는 곳(결과 화면, 일시정지 메뉴)과
    // 선택을 처리하는 곳(LevelUpUI)이 서로 다른 스크립트라, 인스펙터 연결 없이 값을 주고받기 위해서다.
    // 판이 시작될 때마다 LevelUpUI가 Reset()을 불러서 이전 판의 숫자가 남지 않는다.
    public static class LevelUpPickLog
    {
        private static int moveSpeedPicks;
        private static int healthPicks;
        private static int newDronePicks;
        private static int upgradeDronePicks;

        // 이동속도를 처음 고른 게임 시간(초). 한 번도 안 골랐으면 -1.
        private static float firstMoveSpeedSeconds = -1f;

        // 새 판이 시작될 때 모든 숫자를 0으로 되돌린다.
        public static void Reset()
        {
            moveSpeedPicks = 0;
            healthPicks = 0;
            newDronePicks = 0;
            upgradeDronePicks = 0;
            firstMoveSpeedSeconds = -1f;
        }

        // 플레이어가 선택지 하나를 골랐을 때 부른다. elapsedSeconds = 그 순간의 게임 시간.
        public static void Record(LevelUpOption option, float elapsedSeconds)
        {
            if (option == null) return;

            switch (option.Kind)
            {
                case LevelUpOptionKind.NewDrone:
                    newDronePicks++;
                    break;
                case LevelUpOptionKind.UpgradeDrone:
                    upgradeDronePicks++;
                    break;
                case LevelUpOptionKind.StatBoost:
                    if (option.StatBoost == StatBoostKind.MoveSpeed)
                    {
                        moveSpeedPicks++;
                        if (firstMoveSpeedSeconds < 0f) firstMoveSpeedSeconds = elapsedSeconds;
                    }
                    else
                    {
                        healthPicks++;
                    }
                    break;
            }
        }

        // 판 기록 끝에 덧붙일 문장을 만든다. 예: "레벨업선택=이동속도3/체력1/신규드론1/드론강화4 이동속도첫선택=45초"
        // 아직 아무것도 고르지 않았으면 빈 글자를 돌려줘서, 기록에 아무것도 덧붙지 않게 한다.
        public static string BuildSummary()
        {
            if (moveSpeedPicks + healthPicks + newDronePicks + upgradeDronePicks == 0) return "";

            string first = firstMoveSpeedSeconds >= 0f ? $"{firstMoveSpeedSeconds:F0}초" : "없음";
            return $"레벨업선택=이동속도{moveSpeedPicks}/체력{healthPicks}/신규드론{newDronePicks}/드론강화{upgradeDronePicks} 이동속도첫선택={first}";
        }
    }
}
