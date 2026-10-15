using UnityEngine;

namespace SurvivalDrone.Drones
{
    // 폭발 드론이 터질 때, 기존 불꽃·충격파 링·중심 섬광 위에 "묵직함"을 더해 주는 이펙트:
    // 어두운 연기 + 날아가는 검은 파편 + 떠오르는 불씨. (CBT 3번 테스터의 "공격 이펙트" 의견과 사용자가 가져온 참고 그림 1번을 바탕으로 만들었다.)
    // 피해·반경·간격 같은 게임 수치는 바뀌지 않고 눈에 보이는 연출만 더한다.
    //
    // 구성 (ExplosionAfter.prefab, ExplosionEffectBuilder 메뉴가 만들어 준다):
    //  - Smoke: 짙은 회색 연기 덩어리 5개. 천천히 위로 올라가며 커지고 사라진다. (약 0.9초)
    //  - Debris: 어두운 각진 파편 9개. 사방으로 날아가다 중력으로 떨어지며 빙글 돈다. (약 0.55초)
    //  - Embers: 작은 주황 불씨 8개. 위로 떠오르며 사라진다. (약 0.7초)
    //  세 가지 모두 "월드 기준"으로 시뮬레이션되는 파티클이라, 재생 직전에 위치를 정해 두면 터진 자리에 남는다.
    //  연기·불씨가 퍼지는 크기는 폭발 반경에 비례해서, 레벨이 올라 폭발이 커지면 같이 커진다.
    //  그림(연기 덩어리, 파편)은 코드로 그려서 저장한 것이라 별도 FBX 파일은 없다.
    //
    // 폭발 드론은 크기가 0.5배라 이 이펙트를 자식으로 두면 같이 작아진다. 그래서 드론이 만들 때 따로(최상위에) 만들어 쓴다.
    public class ExplosionAfterEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private ParticleSystem debris;
        [SerializeField] private ParticleSystem embers;

        // 연기 덩어리 크기: 폭발 반경의 이 배율(최소~최대). 반경 3m면 1.2~1.8m.
        [SerializeField] private float smokeSizeMin = 0.4f;
        [SerializeField] private float smokeSizeMax = 0.6f;

        // 연기와 불씨가 나타나는 범위: 폭발 반경의 이 배율. 반경 3m면 연기는 1.5m, 불씨는 1.2m 안쪽.
        [SerializeField] private float smokeSpread = 0.5f;
        [SerializeField] private float emberSpread = 0.4f;

        // 연기·파편·불씨가 시작하는 높이(바닥에서 미터). 폭발 지점이 드론 높이라 그대로 쓰면 공중에 떠 보이므로 바닥 근처로 맞춘다.
        [SerializeField] private float groundHeight = 0.35f;

        // 폭발 드론이 여러 대 동시에 터져도 화면이 연기로 뒤덮이지 않도록, 연기는 동시에 최대 3곳까지만 보여 준다.
        // (파편과 불씨는 작아서 항상 재생한다.) 슬롯마다 "이 연기가 사라지는 시각"을 적어 두고, 비어 있는 슬롯이 없으면 연기를 건너뛴다.
        private const int MaxSimultaneousSmokes = 3;
        private const float SmokeLifeSeconds = 1f;
        private static readonly float[] smokeEndTimes = new float[MaxSimultaneousSmokes];

        // 폭발 지점(center, 월드 좌표)과 폭발 반경(radius)을 받아 한 번 재생한다.
        public void Play(Vector3 center, float radius)
        {
            Vector3 ground = new Vector3(center.x, groundHeight, center.z);
            Prepare(smoke, ground, radius * smokeSpread, radius * smokeSizeMin, radius * smokeSizeMax);
            Prepare(debris, ground, 0f, 0f, 0f);
            Prepare(embers, ground, radius * emberSpread, 0f, 0f);

            if (smoke != null && TryReserveSmokeSlot()) smoke.Play();
            if (debris != null) debris.Play();
            if (embers != null) embers.Play();
        }

        // 검증·미리보기용: 시간 t초 지난 순간의 모습으로 한 번에 맞춘다. (Play를 한 번 부른 뒤에 쓴다. 에디터 화면은 실시간으로 거의 안 돌아서 정지 장면을 찍을 때 필요하다)
        public void Evaluate(float t)
        {
            if (smoke != null) smoke.Simulate(t, true, true);
            if (debris != null) debris.Simulate(t, true, true);
            if (embers != null) embers.Simulate(t, true, true);
        }

        // 비어 있는 연기 슬롯이 있으면 하나를 잡고 true, 모두 사용 중이면 false.
        private static bool TryReserveSmokeSlot()
        {
            for (int i = 0; i < smokeEndTimes.Length; i++)
            {
                // 사라지는 시각이 지났거나, 지금보다 1초보다 더 먼 미래라면(이전 플레이 때 남은 값) 빈 슬롯으로 본다.
                if (smokeEndTimes[i] <= Time.time || smokeEndTimes[i] > Time.time + SmokeLifeSeconds)
                {
                    smokeEndTimes[i] = Time.time + SmokeLifeSeconds;
                    return true;
                }
            }
            return false;
        }

        // 파티클을 놓을 자리와 퍼지는 범위·크기를 정한다. spread가 0이면 범위는 프리팹 값 그대로, size가 0이면 크기도 그대로 둔다.
        private static void Prepare(ParticleSystem ps, Vector3 position, float spread, float sizeMin, float sizeMax)
        {
            if (ps == null) return;

            ps.transform.position = position;

            if (spread > 0f)
            {
                var shape = ps.shape;
                shape.radius = spread;
            }
            if (sizeMax > 0f)
            {
                var main = ps.main;
                main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            }
        }
    }
}
