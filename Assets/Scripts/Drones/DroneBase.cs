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
        // 예: 레벨 3이면 1 + 0.28*(3-1) = 1.56배. 자식 클래스(MeleeDrone 등)가 데미지, 속도 등에 곱해서 사용.
        protected float GetScale()
        {
            float scale = 1f + statGrowthPerLevel * (level - 1);

            // 최대 레벨(변신 상태)이면 추가 보너스 배율을 한 번 더 곱해준다.
            if (IsTransformed) scale *= transformedBonusMultiplier;
            return scale;
        }

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
