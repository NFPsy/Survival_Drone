using UnityEngine;

namespace SurvivalDrone.Drones
{
    // 모든 드론(근접/저격/수집 등)이 공통으로 가지는 기능을 모아둔 "부모 클래스".
    // abstract(추상 클래스)라서 이 클래스 자체는 게임 오브젝트에 직접 붙일 수 없고,
    // 반드시 MeleeDrone, SniperDrone 처럼 상속받은 자식 클래스를 사용해야 한다.
    //
    // 여기서 공통으로 처리하는 것: 레벨(1~5), 레벨에 따른 스탯 배율 계산, 플레이어 따라다니기.
    public abstract class DroneBase : MonoBehaviour
    {
        // 현재 드론 레벨. protected라서 자식 클래스(MeleeDrone 등)에서도 접근 가능.
        [SerializeField] protected int level = 1;

        // 드론이 도달할 수 있는 최대 레벨. 기획서 기준 5단계.
        [SerializeField] private int maxLevel = 5;

        // 레벨이 1 오를 때마다 성능이 몇 % 좋아지는지 (0.16 = 16%). 22%로도 여전히 쉽다는 피드백으로 추가 하향.
        [SerializeField] private float statGrowthPerLevel = 0.16f;

        // 최대 레벨(5)에 도달해서 "변신"했을 때 추가로 곱해지는 보너스 배율.
        [SerializeField] private float transformedBonusMultiplier = 1.2f;

        // 뽑기로 얻은 드론의 "등급 × 강화" 배율 (격납고에서 장착한 드론에만 들어온다). 기본은 1배(변화 없음).
        // 판 안 레벨업 배율(GetScale)에 한 번 더 곱해진다. 값은 판을 시작할 때 DroneManager가 정해준다.
        // 직렬화(인스펙터/씬 저장) 대상이 아니라서 씬·프리팹에 옛 값이 남아 코드 기본값을 덮어쓰는 일이 없다.
        private float metaMultiplier = 1f;

        // 이 드론의 주인(플레이어)의 Transform. 이 위치를 기준으로 따라다닌다.
        [SerializeField] protected Transform owner;

        // 주인으로부터 얼마나 떨어진 위치에 있을지(드론들이 서로 겹치지 않도록 DroneManager가 계산해줌).
        [SerializeField] private Vector3 slotOffset = Vector3.zero;

        // 목표 위치로 얼마나 빠르게 따라갈지(값이 클수록 더 빠르게 쫓아감).
        [SerializeField] private float followLerp = 8f;

        // 외부에서 읽을 수 있는 현재 레벨.
        public int Level => level;

        // 외부에서 읽을 수 있는 최대 레벨.
        public int MaxLevel => maxLevel;

        // 최대 레벨에 도달했는지 여부 ("변신"한 상태인지).
        public bool IsTransformed => level >= maxLevel;

        // 외부에서 읽을 수 있는 등급 × 강화 배율.
        public float MetaMultiplier => metaMultiplier;

        // DroneManager가 판 시작 때 장착한 드론의 등급 × 강화 배율을 넣어줄 때 사용. 0 이하 값은 무시한다.
        public void SetMetaMultiplier(float multiplier)
        {
            if (multiplier > 0f) metaMultiplier = multiplier;
        }

        // DroneManager가 드론을 생성한 직후 주인을 지정해줄 때 사용.
        public void SetOwner(Transform newOwner)
        {
            owner = newOwner;
        }

        // DroneManager가 여러 드론이 겹치지 않도록 위치(오프셋)를 재배치할 때 사용.
        public void SetSlotOffset(Vector3 offset)
        {
            slotOffset = offset;
        }

        // 레벨업 선택지에서 "이 드론 강화"를 골랐을 때 호출되는 함수.
        // 최대 레벨이면 더 이상 오르지 않고 false를 반환.
        public bool TryLevelUp()
        {
            if (level >= maxLevel) return false;

            level++;
            // 자식 클래스가 레벨업 시점에 추가로 하고 싶은 처리를 할 수 있도록 알림(기본은 아무것도 안 함).
            OnLevelChanged();

            // 방금 최대 레벨에 도달했다면 "변신" 처리도 함께 실행.
            if (IsTransformed) OnTransformed();
            return true;
        }

        // 현재 레벨을 기준으로 "몇 배 강해졌는지"를 계산하는 함수.
        // 예: 레벨 3이면 1 + 0.16*(3-1) = 1.32배. 자식 클래스(MeleeDrone 등)가 데미지, 속도 등에 곱해서 사용.
        // 뽑기로 얻은 등급 × 강화 배율(metaMultiplier)도 여기서 함께 곱한다 —
        // 모든 드론이 능력치를 이 함수의 값으로 계산하기 때문에, 한 곳만 고치면 판 안 레벨업과 같은 능력치 전부에 반영된다.
        protected float GetScale()
        {
            float scale = 1f + statGrowthPerLevel * (level - 1);

            // 최대 레벨(변신 상태)이면 추가 보너스 배율을 한 번 더 곱해준다.
            if (IsTransformed) scale *= transformedBonusMultiplier;

            // 뽑기 등급·강화 배율 (장착하지 않은 드론이나 메타 시스템이 없으면 1배).
            return scale * metaMultiplier;
        }

        // 플레이어가 "오버드라이브"(액티브 스킬)를 켰을 때 공격 속도가 몇 배가 되는지 알려주는 값.
        // 평소엔 1배(변화 없음), 오버드라이브 중에는 2.5배가 된다.
        //
        // 공격형 드론들(근접/저격/폭발)은 공격 쿨타임을 계산할 때 이 값으로 나눠주면
        // 그만큼 더 자주 공격하게 된다. 예: 0.5초 간격 ÷ 2.5배 = 0.2초 간격.
        //
        // 회복 드론은 일부러 이 배율을 쓰지 않는다 — 오버드라이브는 "피해를 2배로 받는 대신
        // 화력을 얻는" 위험 감수 스킬인데, 회복까지 빨라지면 그 위험이 사라져버리기 때문이다.
        protected static float OverdriveAttackSpeed => SurvivalDrone.Player.OverdriveSystem.AttackSpeedMultiplier;

        // 레벨이 바뀔 때마다 호출되는 함수. 자식 클래스가 필요하면 override해서 사용(기본은 빈 함수).
        protected virtual void OnLevelChanged()
        {
        }

        // 최대 레벨에 도달해서 "변신"할 때 호출되는 함수. 기본적으로는 크기를 키워서 시각적으로 표시.
        // 필요하면 자식 클래스에서 override해서 다른 연출(색상 변경 등)을 추가할 수 있다.
        protected virtual void OnTransformed()
        {
            transform.localScale *= 1.25f;
        }

        // 매 프레임 실행되는 기본 동작: 주인 위치 + 지정된 오프셋 위치로 부드럽게 이동.
        // MeleeDrone처럼 궤도를 도는 등 다른 움직임이 필요한 자식 클래스는 이 함수를 override해서 대체한다.
        protected virtual void Update()
        {
            if (owner == null) return;

            // owner.TransformDirection을 사용해서, 주인이 회전해도 오프셋이 주인 기준 방향으로 따라 돌게 함.
            Vector3 targetPos = owner.position + owner.TransformDirection(slotOffset);

            // 즉시 이동이 아니라 Lerp(선형 보간)로 부드럽게 따라가도록 함.
            transform.position = Vector3.Lerp(transform.position, targetPos, followLerp * Time.deltaTime);
        }
    }
}
