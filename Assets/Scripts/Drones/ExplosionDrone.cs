using UnityEngine;
using UnityEngine.InputSystem;
using SurvivalDrone.Enemies;
using SurvivalDrone.UI;

namespace SurvivalDrone.Drones
{
    // 폭발 드론: 일정 시간마다 한 번씩 "폭발 지점" 주변 전체(범위 공격)에 피해를 준다.
    // 근접 드론과 달리 특정 적 하나를 노리는 게 아니라 "범위 안에 있는 모든 적"을 한꺼번에 때린다.
    //
    // 폭발 지점은 드론이 떠 있는 자리가 아니라 플레이어가 정한다 (10/14 개선):
    //  - 이 드론을 가지고 있는 동안 방향키(↑↓←→)는 플레이어 이동이 아니라 "폭발 지점 조작"이 된다. 이동은 WASD만 한다.
    //  - 폭발 지점은 플레이어 기준으로 얼마나 떨어져 있는지(aimOffset)로 기억해서, 플레이어가 움직이면 같이 따라온다.
    //  - 방향키를 떼면 마지막 자리에 그대로 남는다. 폭발 간격(기본 3초) 동안 지점을 옮겨 두면 시간이 됐을 때 그 자리에서 터진다.
    //  - 지점에는 폭발 범위만큼의 원(ExplosionAimMarker)이 바닥에 보이고, 터지기 직전에 더 밝아진다.
    //
    // 기획서 4장 기준 - 강화될수록 좋아지는 것은 "폭발 범위 또는 피해량"이므로,
    // 레벨이 오르면 범위와 피해량이 함께 커지도록 만들었다.
    public class ExplosionDrone : DroneBase
    {
        // 이 드론이 있는 동안 방향키를 "폭발 지점 조작"에 쓰고 있는지. PlayerController가 이 값을 보고 방향키 이동을 막는다.
        // 드론이 꺼지거나 사라지면(판 종료·씬 이동) OnDisable에서 꺼지므로 다음 판에 남지 않는다.
        // (드론은 한 판에 하나뿐이지만, 혹시 둘이 겹쳐 켜졌다 꺼지는 순간에도 어긋나지 않도록 개수로 센다)
        private static int reservedCount;
        public static bool ArrowKeysReserved => reservedCount > 0;

        // 방향키로 폭발 지점을 움직이는 속도(초당 미터).
        [SerializeField] private float aimSpeed = 8f;

        // 플레이어에서 폭발 지점까지 최대 몇 미터까지 보낼 수 있는지.
        [SerializeField] private float aimMaxRange = 7f;

        // 바닥에 보여 줄 폭발 지점 표시 프리팹(폭발 범위 크기의 원). 비워 두면 표시 없이 지점만 움직인다.
        [SerializeField] private GameObject aimMarkerPrefab;

        // 폭발 직전(마지막 몇 초)에 표시가 밝아지는 구간 길이(초).
        [SerializeField] private float markerWarnSeconds = 0.6f;

        // 폭발 드론을 얻었을 때 화면 위 알림 칸에 띄울 안내 문구와 표시 시간.
        [SerializeField] private string acquireNotice = "폭발 드론 획득! 방향키(↑↓←→)로 폭발 위치를 정하세요. 이동은 WASD입니다.";
        [SerializeField] private float acquireNoticeSeconds = 5f;

        // 플레이어 기준 폭발 지점 위치(x, z만 사용). 처음에는 플레이어 바로 위(0, 0)다.
        private Vector2 aimOffset;

        private Transform markerTransform;
        private Renderer markerRenderer;
        private MaterialPropertyBlock markerBlock;
        private Color markerBaseColor = Color.white;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // 한 번 폭발할 때 주는 피해량 (레벨에 따라 배율이 곱해짐).
        [SerializeField] private float baseDamage = 10f;

        // 몇 초마다 한 번씩 폭발하는지. 기획서 기준 3초에 1번.
        [SerializeField] private float baseInterval = 3f;

        // 폭발이 닿는 범위(반지름). 레벨이 오르면 이 값이 커진다.
        [SerializeField] private float baseRadius = 3f;

        // 다음 폭발까지 남은 시간.
        private float explodeTimer;

        // 터질 때 재생할 파티클. 인스펙터에서 지정 안 해도 자식에서 자동으로 찾는다.
        [SerializeField] private ParticleSystem explosionEffect;

        // 터질 때 재생할 효과음.
        [SerializeField] private AudioClip explosionSound;

        // 터질 때 바닥에서 피해 범위 끝까지 퍼져 나가는 충격파 링. "어디까지 피해가 닿는지"를 눈으로 보여 준다.
        // 비워두면 링 없이 예전처럼 불꽃만 터진다.
        [SerializeField] private ParticleSystem shockwaveRing;

        // 터지는 순간 드론 위치에서 한 번 크게 번쩍이는 빛. 비워두면 번쩍임이 없다.
        [SerializeField] private ParticleSystem coreFlash;

        private void Awake()
        {
            if (explosionEffect == null) explosionEffect = GetComponentInChildren<ParticleSystem>();

            // 불꽃 파티클은 원래 "드론 기준"으로 시뮬레이션돼서, 터진 뒤 드론이 움직이면 불꽃도 같이 끌려갔다.
            // 이제 폭발 지점이 드론과 다른 곳이므로, 터진 자리에 남도록 "월드 기준"으로 바꾼다.
            if (explosionEffect != null)
            {
                var burstMain = explosionEffect.main;
                burstMain.simulationSpace = ParticleSystemSimulationSpace.World;
            }
        }

        // 이 드론이 켜져 있는 동안 방향키를 폭발 지점 조작용으로 예약한다.
        private void OnEnable()
        {
            reservedCount++;
        }

        private void OnDisable()
        {
            reservedCount = Mathf.Max(0, reservedCount - 1);
        }

        private void Start()
        {
            // 바닥에 폭발 지점 표시를 만든다. 드론의 자식으로 두면 드론 크기(0.5배)와 움직임에 휘둘리므로 따로 둔다.
            if (aimMarkerPrefab != null)
            {
                var marker = Instantiate(aimMarkerPrefab);
                markerTransform = marker.transform;
                markerRenderer = marker.GetComponentInChildren<Renderer>();
                markerBlock = new MaterialPropertyBlock();
                if (markerRenderer != null && markerRenderer.sharedMaterial != null && markerRenderer.sharedMaterial.HasProperty(BaseColorId))
                    markerBaseColor = markerRenderer.sharedMaterial.GetColor(BaseColorId);
            }

            // 방향키가 이동이 아니라 폭발 지점 조작이 된다는 것을 알린다. (방향키로만 이동하던 사람이 당황하지 않도록)
            if (!string.IsNullOrEmpty(acquireNotice)) HUDNotice.Instance?.Show(acquireNotice, acquireNoticeSeconds);
        }

        private void OnDestroy()
        {
            if (markerTransform != null) Destroy(markerTransform.gameObject);
        }

        protected override void Update()
        {
            // 드론의 몸은 예전처럼 플레이어 주변 자기 자리에 떠 있으면 되므로(위치는 폭발과 무관),
            // 부모 클래스(DroneBase)의 기본 "따라다니기" 동작을 그대로 사용한다.
            base.Update();

            // 방향키로 폭발 지점을 옮긴다.
            UpdateAim();

            // 폭발 쿨타임을 줄여나가다가, 다 되면 한 번 터뜨린다.
            explodeTimer -= Time.deltaTime;
            if (explodeTimer <= 0f)
            {
                // 오버드라이브 중이면 폭발 간격도 배율만큼 짧아진다.
                explodeTimer = baseInterval / OverdriveAttackSpeed;
                Explode();
            }

            UpdateMarker();
        }

        // 방향키 입력으로 폭발 지점(aimOffset)을 움직인다. 이동 방향은 플레이어 이동과 같다(↑ = 화면 위쪽).
        // 시간이 멈춘 동안(일시정지·레벨업 선택)에는 Time.deltaTime이 0이라 지점도 움직이지 않는다.
        private void UpdateAim()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            Vector2 input = Vector2.zero;
            if (keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (input == Vector2.zero) return;

            // 대각선이 더 빨라지지 않게 길이를 1로 맞추고, 플레이어에서 최대 거리를 넘지 않게 제한한다.
            aimOffset += input.normalized * aimSpeed * Time.deltaTime;
            aimOffset = Vector2.ClampMagnitude(aimOffset, aimMaxRange);
        }

        // 지금 폭발 지점의 월드 좌표. 높이는 드론이 떠 있는 높이를 그대로 써서(예전과 같은 판정 높이) 가로 위치만 바뀐다.
        private Vector3 AimPoint()
        {
            Vector3 center = owner != null ? owner.position : transform.position;
            return new Vector3(center.x + aimOffset.x, transform.position.y, center.z + aimOffset.y);
        }

        // 현재 레벨 기준 폭발 반지름. 범위는 제곱근을 써서 완만하게 커진다.
        private float CurrentRadius()
        {
            return baseRadius * Mathf.Sqrt(GetScale());
        }

        // 바닥의 폭발 지점 표시를 알맞은 자리·크기로 맞추고, 터지기 직전에는 더 밝게 만든다.
        private void UpdateMarker()
        {
            if (markerTransform == null) return;

            Vector3 point = AimPoint();
            markerTransform.position = new Vector3(point.x, markerTransform.position.y, point.z);

            // 표시 그림은 한가운데부터 가장자리까지가 한 장이라서, 크기를 "지름"으로 맞추면 가장자리가 실제 피해 범위와 같아진다.
            float diameter = CurrentRadius() * 2f;
            markerTransform.localScale = new Vector3(diameter, diameter, 1f);

            // 폭발까지 남은 시간이 markerWarnSeconds 안이면 투명도를 1까지 올려 "곧 터진다"를 알린다.
            if (markerRenderer != null)
            {
                float warn = markerWarnSeconds > 0f ? Mathf.Clamp01(1f - explodeTimer / markerWarnSeconds) : 0f;
                Color color = markerBaseColor;
                color.a = Mathf.Lerp(markerBaseColor.a, 1f, warn);
                markerRenderer.GetPropertyBlock(markerBlock);
                markerBlock.SetColor(BaseColorId, color);
                markerRenderer.SetPropertyBlock(markerBlock);
            }
        }

        // 실제로 폭발 지점 주변에 피해를 주는 함수.
        private void Explode()
        {
            // 현재 레벨 기준 배율.
            float scale = GetScale();

            // 범위는 제곱근을 써서 완만하게, 피해량은 배율 그대로 커지게 한다.
            float radius = CurrentRadius();
            float damage = baseDamage * scale;

            // 폭발 지점(드론 위치가 아니라 플레이어가 정한 자리).
            Vector3 center = AimPoint();

            // Physics.OverlapSphere로 이 범위 안에 있는 모든 콜라이더를 찾아서 적이면 피해를 준다.
            var colliders = Physics.OverlapSphere(center, radius);
            foreach (var col in colliders)
            {
                var enemy = col.GetComponent<EnemyAI>();
                if (enemy != null) enemy.ApplyDamage(damage);
            }

            // 폭발 파티클도 실제 피해 범위(radius)에 맞춰 크기를 조절하고, 폭발 지점으로 옮긴 뒤 재생.
            if (explosionEffect != null)
            {
                var shape = explosionEffect.shape;
                shape.radius = radius;
                explosionEffect.transform.position = center;
                explosionEffect.Play();
            }
            PlayShockwave(center, radius);
            SurvivalDrone.Core.AudioManager.Instance?.PlaySfx(explosionSound);
        }

        // 충격파 링과 중심 번쩍임을 재생한다. 두 효과는 "월드 기준"으로 시뮬레이션되는 파티클이라,
        // 재생 직전에 위치를 정해 두면 그 뒤에 드론이 움직여도 터진 자리에 남아 퍼져 나간다.
        private void PlayShockwave(Vector3 center, float radius)
        {
            if (shockwaveRing != null)
            {
                // 링은 바닥 바로 위(y=0.1)에 눕혀서 폭발 지점 바로 아래부터 퍼지게 한다.
                shockwaveRing.transform.position = new Vector3(center.x, 0.1f, center.z);

                // 링 그림은 한가운데부터 가장자리까지가 한 장이라서, 크기를 "지름"(반지름 x 2)으로 맞추면
                // 다 퍼졌을 때 링의 바깥 가장자리가 실제 피해 범위와 같아진다.
                var ringMain = shockwaveRing.main;
                ringMain.startSize = radius * 2f;
                shockwaveRing.Play();
            }

            if (coreFlash != null)
            {
                coreFlash.transform.position = center;
                var flashMain = coreFlash.main;
                flashMain.startSize = Mathf.Max(1.5f, radius * 0.8f);
                coreFlash.Play();
            }
        }
    }
}
