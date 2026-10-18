using UnityEngine;
using UnityEngine.InputSystem;
using SurvivalDrone.Core;
using SurvivalDrone.Enemies;
using SurvivalDrone.UI;

namespace SurvivalDrone.Drones
{
    // 폭발 드론: 일정 시간마다 한 번씩 "폭발 지점" 주변 전체(범위 공격)에 피해를 준다.
    // 근접 드론과 달리 특정 적 하나를 노리는 게 아니라 "범위 안에 있는 모든 적"을 한꺼번에 때린다.
    //
    // 폭발 지점은 드론이 떠 있는 자리가 아니라 플레이어가 정한다 (10/14 개선, 10/18에 방향키에서 마우스로 바꿈):
    //  - 마우스로 "조준 커서"를 움직인다. 커서가 가리키는 바닥 자리가 폭발 지점이다. (화면 속 커서 위치에서 바닥으로 선을 쏴서 닿는 곳을 찾는다)
    //    방향키는 더 이상 조준에 쓰지 않는다. 이동은 WASD다.
    //  - 운영체제의 마우스 커서 속도는 게임이 바꿀 수 없어서, 판이 진행되는 동안에는 실제 마우스 커서를 숨기고 화면에 고정한 뒤
    //    "마우스가 움직인 양 x 감도"만큼 게임 안의 조준 커서(virtualCursor)를 움직인다. 감도는 설정 창에서 25~300 %로 정한다(AimSettings).
    //    바닥에 보이는 폭발 범위 원(ExplosionAimMarker)이 조준 커서 역할을 한다.
    //  - 시간이 멈추거나(일시정지·레벨업 선택) 판이 끝나면(승리·패배) 실제 커서를 다시 보여 주고 풀어서, 메뉴를 평소처럼 누를 수 있다.
    //    다시 시작되면 조준 커서는 멈췄던 자리에서 이어진다.
    //  - 플레이어에게서 aimMaxRange(기본 7m)보다 멀리 갈 수 없다. 가장자리에서 더 밀어도 커서가 그 자리에 머문다.
    //  - 카메라가 플레이어를 따라가므로, 마우스를 가만히 두면 폭발 지점도 플레이어와 같이 움직인다.
    //  - 지점에는 폭발 범위만큼의 원(ExplosionAimMarker)이 바닥에 보이고, 터지기 직전에 더 밝아진다.
    //
    // 기획서 4장 기준 - 강화될수록 좋아지는 것은 "폭발 범위 또는 피해량"이므로,
    // 레벨이 오르면 범위와 피해량이 함께 커지도록 만들었다.
    public class ExplosionDrone : DroneBase
    {
        // 플레이어에서 폭발 지점까지 최대 몇 미터까지 보낼 수 있는지.
        [SerializeField] private float aimMaxRange = 7f;

        // 바닥에 보여 줄 폭발 지점 표시 프리팹(폭발 범위 크기의 원). 비워 두면 표시 없이 지점만 움직인다.
        [SerializeField] private GameObject aimMarkerPrefab;

        // 폭발 직전(마지막 몇 초)에 표시가 밝아지는 구간 길이(초).
        [SerializeField] private float markerWarnSeconds = 0.6f;

        // 폭발 드론을 얻었을 때 화면 위 알림 칸에 띄울 안내 문구와 표시 시간.
        [SerializeField] private string acquireNotice = "폭발 드론 획득! 마우스로 폭발 위치를 정하세요. 바닥의 원이 커서예요.";
        [SerializeField] private float acquireNoticeSeconds = 5f;

        // 플레이어 기준 폭발 지점 위치(x, z만 사용). 처음에는 플레이어 바로 위(0, 0)다.
        private Vector2 aimOffset;

        // 게임 안의 조준 커서 위치(화면 좌표, 픽셀). 마우스 이동량 x 감도로 움직인다.
        private Vector2 virtualCursor;
        private bool virtualCursorReady;

        // 지금 실제 마우스 커서를 숨기고 고정해 둔 상태인지. 풀어 줄 때 한 번만 되돌리기 위해 기억한다.
        private bool cursorCaptured;

        // 커서를 고정한 첫 프레임에는 마우스 이동량이 튈 수 있어서 한 번 건너뛴다.
        private bool skipNextDelta;

        // 판 기록용: 마우스 커서를 고정하는 데 성공했는지. ""(폭발 드론이 없었음) / "고정성공" / "고정실패".
        // 웹(WebGL)에서는 브라우저가 마우스 고정에 클릭 같은 사용자 동작을 요구해서 실패할 수 있어, 로그로 확인하려고 남긴다.
        private static string aimLockLog = "";

        // 커서를 고정한 뒤 이 시간(초)이 지나서 실제로 고정됐는지 확인한다. (웹에서는 고정이 바로 적용되지 않을 수 있다)
        private const float LockCheckDelaySeconds = 0.5f;
        private bool lockCheckPending;
        private float lockCheckAt;

        // 새 판이 시작될 때 지난 판의 조준 기록을 비운다. (static이라 씬을 다시 불러도 값이 남는다)
        public static void ResetLogState()
        {
            aimLockLog = "";
        }

        // 판 기록 끝에 덧붙일 마우스 조준 요약. 폭발 드론을 한 번도 쓰지 않은 판은 빈 글자(기록에 아무것도 덧붙지 않음).
        // 예) "마우스감도=100% 조준커서=고정성공"
        public static string BuildLogSummary()
        {
            if (string.IsNullOrEmpty(aimLockLog)) return "";
            return $"마우스감도={Mathf.RoundToInt(AimSettings.LoadSensitivity() * 100f)}% 조준커서={aimLockLog}";
        }

        // 지금 쓰는 감도. 커서를 고정할 때마다 저장된 값을 새로 읽는다. (설정 창은 시간이 멈춘 동안만 열리므로 다시 시작할 때 반영된다)
        private float sensitivity = AimSettings.DefaultSensitivity;

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

        // 불꽃·링·섬광 위에 연기·검은 파편·불씨를 더해 묵직하게 보이게 하는 이펙트의 원본(ExplosionAfter.prefab). 비워두면 예전처럼 동작한다.
        // 드론이 0.5배로 줄어 있어서, 자식으로 두면 이펙트도 같이 작아진다. 그래서 따로(최상위에) 하나 만들어 두고 쓴다.
        [SerializeField] private ExplosionAfterEffect afterEffectPrefab;
        private ExplosionAfterEffect afterEffect;

        private void Awake()
        {
            if (afterEffectPrefab != null) afterEffect = Instantiate(afterEffectPrefab);
            if (explosionEffect == null) explosionEffect = GetComponentInChildren<ParticleSystem>();

            // 불꽃 파티클은 원래 "드론 기준"으로 시뮬레이션돼서, 터진 뒤 드론이 움직이면 불꽃도 같이 끌려갔다.
            // 이제 폭발 지점이 드론과 다른 곳이므로, 터진 자리에 남도록 "월드 기준"으로 바꾼다.
            if (explosionEffect != null)
            {
                var burstMain = explosionEffect.main;
                burstMain.simulationSpace = ParticleSystemSimulationSpace.World;
            }
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

            // 마우스로 폭발 위치를 정한다는 것을 알린다.
            if (!string.IsNullOrEmpty(acquireNotice)) HUDNotice.Instance?.Show(acquireNotice, acquireNoticeSeconds);
        }

        // 꺼지거나 사라질 때(씬이 바뀔 때 포함) 숨겨 둔 커서를 반드시 되돌린다. 안 그러면 메뉴에서 마우스가 안 보이게 된다.
        private void OnDisable()
        {
            ReleaseCursor();
        }

        private void OnDestroy()
        {
            ReleaseCursor();
            if (markerTransform != null) Destroy(markerTransform.gameObject);
            if (afterEffect != null) Destroy(afterEffect.gameObject);
        }

        protected override void Update()
        {
            // 드론의 몸은 예전처럼 플레이어 주변 자기 자리에 떠 있으면 되므로(위치는 폭발과 무관),
            // 부모 클래스(DroneBase)의 기본 "따라다니기" 동작을 그대로 사용한다.
            base.Update();

            // 마우스로 조준 커서를 움직여 폭발 지점을 옮긴다.
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

        // 마우스 이동량으로 조준 커서(virtualCursor)를 움직이고, 그 자리를 바닥에 투영해 폭발 지점(aimOffset)을 정한다.
        // 판이 진행 중이 아니면(시간 정지·승패 결정) 실제 커서를 되돌리고 아무것도 바꾸지 않는다.
        private void UpdateAim()
        {
            bool playing = Time.timeScale > 0f && GameManager.Instance != null && GameManager.Instance.State == MatchState.Playing;
            if (!playing)
            {
                ReleaseCursor();
                return;
            }

            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return;

            Vector3 center = owner != null ? owner.position : transform.position;

            // 처음이면 조준 커서를 지금 폭발 지점(처음엔 플레이어 위치)이 보이는 화면 자리에 둔다.
            if (!virtualCursorReady)
            {
                Vector3 start = cam.WorldToScreenPoint(new Vector3(center.x + aimOffset.x, 0f, center.z + aimOffset.y));
                virtualCursor = new Vector2(start.x, start.y);
                virtualCursorReady = true;
            }

            CaptureCursor();
            CheckCursorLock();

            if (skipNextDelta) skipNextDelta = false;
            else virtualCursor += mouse.delta.ReadValue() * sensitivity;

            // 화면 밖으로 나가지 못하게 한다.
            virtualCursor.x = Mathf.Clamp(virtualCursor.x, 0f, Screen.width);
            virtualCursor.y = Mathf.Clamp(virtualCursor.y, 0f, Screen.height);

            Ray ray = cam.ScreenPointToRay(virtualCursor);
            if (!TryGetAimOffset(ray, center, aimMaxRange, out Vector2 offset)) return;
            aimOffset = offset;

            // 7m 제한에 걸렸다면 조준 커서도 제한된 자리로 되돌려서, 가장자리에서 더 밀어도 커서가 바깥으로 쌓이지 않게 한다.
            // (제한에 안 걸렸으면 거의 같은 자리라 그대로다)
            Vector3 limited = cam.WorldToScreenPoint(new Vector3(center.x + aimOffset.x, 0f, center.z + aimOffset.y));
            if (limited.z > 0f) virtualCursor = new Vector2(limited.x, limited.y);
        }

        // 실제 마우스 커서를 숨기고 화면에 고정한다. (이미 그렇게 해 뒀으면 아무것도 안 한다)
        private void CaptureCursor()
        {
            if (cursorCaptured) return;
            sensitivity = AimSettings.LoadSensitivity();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            cursorCaptured = true;
            skipNextDelta = true;

            // 폭발 드론을 쓴 판이라는 표시. 확인 결과는 잠시 뒤 CheckCursorLock이 적는다.
            if (string.IsNullOrEmpty(aimLockLog)) aimLockLog = "고정확인중";
            lockCheckPending = true;
            lockCheckAt = Time.unscaledTime + LockCheckDelaySeconds;
        }

        // 커서를 고정한 지 잠시 뒤에 실제로 고정됐는지 확인해서 판 기록용으로 적는다. 한 번이라도 성공하면 성공으로 남긴다.
        private void CheckCursorLock()
        {
            if (!lockCheckPending || Time.unscaledTime < lockCheckAt) return;
            lockCheckPending = false;

            if (Cursor.lockState == CursorLockMode.Locked) aimLockLog = "고정성공";
            else if (aimLockLog != "고정성공") aimLockLog = "고정실패";
        }

        // 숨겨 둔 실제 커서를 다시 보이게 하고 풀어 준다. (내가 숨겨 둔 상태일 때만)
        private void ReleaseCursor()
        {
            if (!cursorCaptured) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            cursorCaptured = false;
        }

        // 마우스에서 나온 선(ray)이 바닥(높이 0)에 닿는 자리를 찾아, 플레이어 기준으로 얼마나 떨어져 있는지(x, z)를 돌려준다.
        // 플레이어에게서 maxRange보다 멀면 같은 방향의 maxRange 지점으로 줄인다. 선이 바닥에 닿지 않으면(하늘을 가리키면) false.
        // 화면이나 입력 장치 없이 숫자만으로 계산해서, 에디터에서 따로 시험해 볼 수 있다.
        public static bool TryGetAimOffset(Ray ray, Vector3 playerPosition, float maxRange, out Vector2 offset)
        {
            offset = Vector2.zero;
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float distance)) return false;

            Vector3 hit = ray.GetPoint(distance);
            offset = Vector2.ClampMagnitude(new Vector2(hit.x - playerPosition.x, hit.z - playerPosition.z), maxRange);
            return true;
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
            afterEffect?.Play(center, radius); // 연기·검은 파편·불씨
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
