using UnityEngine;
using SurvivalDrone.Drones;

namespace SurvivalDrone.UI
{
    // 화면 글자 옆에 붙이는 아이콘을 찾아 주는 도우미.
    // 드론 종류에 맞는 아이콘 그림과 색을 한 곳에서 정해서, 격납고·뽑기 결과·레벨업 선택이 같은 모양을 쓰게 한다.
    public static class UiIcons
    {
        // 용도별 색. (흰색 아이콘을 이 색으로 칠해서 쓴다)
        public static readonly Color Cyan = new Color(0.31f, 0.847f, 0.91f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.30f);
        public static readonly Color Purple = new Color(0.72f, 0.55f, 1f);
        public static readonly Color Dark = new Color(0.043f, 0.055f, 0.078f);

        private static UiIconSet cached;

        // Resources 폴더의 아이콘 모음을 한 번만 불러와 기억해 둔다. 없으면 null (아이콘 없이 예전처럼 동작).
        private static UiIconSet Set
        {
            get
            {
                if (cached == null) cached = Resources.Load<UiIconSet>("UiIconSet");
                return cached;
            }
        }

        public static Sprite Core => Set != null ? Set.core : null;
        public static Sprite Credit => Set != null ? Set.credit : null;
        public static Sprite Shard => Set != null ? Set.shard : null;

        // 능력치 강화 아이콘 (이동 속도 / 최대 체력). 아이콘 모음에 없으면 null.
        public static Sprite StatSpeed => Set != null ? Set.statSpeed : null;
        public static Sprite StatHealth => Set != null ? Set.statHealth : null;

        // 드론 종류에 맞는 아이콘 그림.
        public static Sprite Drone(DroneType type)
        {
            if (Set == null) return null;
            switch (type)
            {
                case DroneType.Melee: return Set.droneMelee;
                case DroneType.Sniper: return Set.droneSniper;
                case DroneType.Explosion: return Set.droneExplosion;
                case DroneType.Heal: return Set.droneHeal;
                case DroneType.Collector: return Set.droneCollector;
                default: return null;
            }
        }

        // 드론 종류에 맞는 아이콘 색.
        public static Color DroneColor(DroneType type)
        {
            switch (type)
            {
                case DroneType.Melee: return new Color(0.55f, 0.92f, 1f);
                case DroneType.Sniper: return new Color(1f, 0.62f, 0.28f);
                case DroneType.Explosion: return new Color(1f, 0.45f, 0.25f);
                case DroneType.Heal: return new Color(0.45f, 1f, 0.6f);
                case DroneType.Collector: return new Color(1f, 0.88f, 0.3f);
                default: return Color.white;
            }
        }

        // 능력치 강화 아이콘을 이미지에 넣는다. (speed = true면 이동 속도, false면 최대 체력)
        public static void SetStatIcon(UnityEngine.UI.Image image, bool speed)
        {
            if (image == null) return;
            Sprite sprite = speed ? StatSpeed : StatHealth;
            image.sprite = sprite;
            image.color = speed ? new Color(0.55f, 0.92f, 1f) : new Color(1f, 0.45f, 0.5f);
            image.gameObject.SetActive(sprite != null);
        }

        // 아이콘 이미지에 그림과 색을 한 번에 넣는다. 이미지나 그림이 없으면 이미지를 숨긴다.
        public static void SetDroneIcon(UnityEngine.UI.Image image, DroneType type, Color? tint = null)
        {
            if (image == null) return;
            Sprite sprite = Drone(type);
            image.sprite = sprite;
            image.color = tint ?? DroneColor(type);
            image.gameObject.SetActive(sprite != null);
        }
    }
}
