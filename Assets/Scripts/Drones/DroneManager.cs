using System.Collections.Generic;
using UnityEngine;

namespace SurvivalDrone.Drones
{
    // 현재 게임에 존재하는 드론 종류들. 새로운 드론 종류를 추가하려면 여기에도 추가해야 한다.
    public enum DroneType { Melee, Sniper, Collector }

    // "드론 종류 + 그 드론의 프리팹"을 한 쌍으로 묶어두는 데이터.
    // 인스펙터에서 리스트로 편집할 수 있도록 [System.Serializable]을 붙였다.
    [System.Serializable]
    public class DronePrefabEntry
    {
        public DroneType type;
        public GameObject prefab;
    }

    // 플레이어가 보유한 드론들을 관리하는 스크립트. 플레이어 오브젝트에 붙는다.
    // 드론 장착, 드론 레벨업, 드론들이 서로 겹치지 않게 위치 재배치하는 역할을 한다.
    public class DroneManager : MonoBehaviour
    {
        // 각 드론 종류별 프리팹 목록. 인스펙터에서 3종(근접/저격/수집) 프리팹을 연결해둔다.
        [SerializeField] private List<DronePrefabEntry> dronePrefabs = new List<DronePrefabEntry>();

        // 게임을 시작할 때 기본으로 장착하고 시작할 드론 (기획서 기준 근접 드론 1개).
        [SerializeField] private DroneType startingDrone = DroneType.Melee;

        // 드론들을 플레이어 주위에 배치할 때 사용하는 반지름.
        [SerializeField] private float slotRadius = 2.5f;

        // 동시에 보유할 수 있는 최대 드론 개수 (기획서 기준 5~6개).
        [SerializeField] private int maxDrones = 6;

        // 현재 보유 중인 드론들을 "종류 -> 실제 드론 오브젝트" 형태로 저장하는 사전(Dictionary).
        // Dictionary를 쓰면 "이 종류의 드론을 가지고 있나?"를 빠르게 확인할 수 있다.
        private readonly Dictionary<DroneType, DroneBase> owned = new Dictionary<DroneType, DroneBase>();

        // 현재 보유한 드론 개수.
        public int OwnedCount => owned.Count;

        // 최대 보유 가능 개수.
        public int MaxDrones => maxDrones;

        // 외부(레벨업 UI 등)에서 보유 드론 목록을 읽기 전용으로 볼 수 있게 해주는 프로퍼티.
        public IReadOnlyDictionary<DroneType, DroneBase> Owned => owned;

        private void Start()
        {
            // 게임 시작 시 기본 드론(근접 드론)을 자동으로 장착.
            AddDrone(startingDrone);
        }

        // 이 종류의 드론을 이미 가지고 있는지 확인.
        public bool HasDrone(DroneType type)
        {
            return owned.ContainsKey(type);
        }

        // 새 드론을 추가할 수 있는 상태인지 확인 (아직 없고, 최대 개수를 넘지 않았을 때만 가능).
        public bool CanAddDrone(DroneType type)
        {
            return !HasDrone(type) && owned.Count < maxDrones;
        }

        // 이미 가진 드론을 더 강화할 수 있는지 확인 (가지고 있고, 아직 최대 레벨이 아닐 때만 가능).
        public bool CanUpgradeDrone(DroneType type)
        {
            return owned.TryGetValue(type, out var drone) && drone.Level < drone.MaxLevel;
        }

        // 새 드론을 실제로 장착하는 함수. 레벨업 선택지에서 "신규 드론"을 고르면 호출된다.
        public bool AddDrone(DroneType type)
        {
            // 이미 가지고 있으면 중복으로 추가하지 않는다.
            if (HasDrone(type)) return false;

            // 이 종류에 해당하는 프리팹 정보를 목록에서 찾는다.
            var entry = dronePrefabs.Find(e => e.type == type);
            if (entry == null || entry.prefab == null) return false;

            // 프리팹으로 실제 드론 오브젝트를 생성하고, 플레이어(transform)의 자식으로 넣는다.
            var obj = Instantiate(entry.prefab, transform);
            var drone = obj.GetComponent<DroneBase>();
            if (drone == null) return false;

            // 새로 만든 드론에게 "네 주인은 나(플레이어)야"라고 알려준다.
            drone.SetOwner(transform);
            owned[type] = drone;

            // 드론이 하나 늘었으니 모든 드론의 배치 위치를 다시 계산한다.
            RecalculateSlots();
            return true;
        }

        // 이미 보유한 드론을 한 단계 강화하는 함수. 레벨업 선택지에서 "드론 강화"를 고르면 호출된다.
        public bool UpgradeDrone(DroneType type)
        {
            return owned.TryGetValue(type, out var drone) && drone.TryLevelUp();
        }

        // 보유한 드론들이 플레이어 주위에서 서로 겹치지 않도록, 원 모양으로 균등하게 배치하는 함수.
        // 예: 드론이 2개면 서로 180도 반대편에, 3개면 120도씩 떨어지도록 배치.
        private void RecalculateSlots()
        {
            int index = 0;
            int count = owned.Count;
            foreach (var drone in owned.Values)
            {
                // 360도를 드론 개수만큼 나눠서, 각 드론마다 다른 각도를 부여.
                float angle = (360f / Mathf.Max(count, 1)) * index;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * slotRadius);
                drone.SetSlotOffset(offset);
                index++;
            }
        }
    }
}
