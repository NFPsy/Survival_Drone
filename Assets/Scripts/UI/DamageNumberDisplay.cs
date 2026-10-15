using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SurvivalDrone.Core;
using SurvivalDrone.Enemies;

namespace SurvivalDrone.UI
{
    // 적이 맞을 때 머리 위로 "피해량 숫자"를 띄우고, 플레이어가 맞을 때는 빨간 숫자를 띄우는 스크립트.
    // 드론이 때렸다는 것, 오버드라이브 때 내가 2배로 맞는다는 것이 숫자로 바로 보이게 하려고 만들었다.
    //
    // 게임 규칙(피해량·체력·밸런스)에는 전혀 영향이 없다. 화면에 보여 주기만 한다.
    //
    // 어떻게 동작하는가:
    //  - 씬에 직접 넣어 두지 않는다. 게임이 켜질 때 "InGame 씬이 열리면 표시용 오브젝트를 만들겠다"고 스스로 등록한다.
    //    (표시용 프리팹은 Resources 폴더의 DamageNumberLayer. 그래서 InGame.unity 씬 파일을 건드리지 않는다)
    //  - 적이 피해를 입으면 EnemyAI.DamageDealt 신호가 오고, 그 위치에 숫자를 하나 띄운다.
    //  - 숫자는 미리 만들어 둔 Text 30개를 돌려 쓴다. (적이 많아도 계속 새로 만들지 않아서 웹에서도 가볍다)
    //
    // 화면이 지저분해지지 않게 하는 규칙:
    //  - 같은 적이 짧은 시간(mergeWindow) 안에 여러 번 맞으면 숫자를 새로 만들지 않고 합쳐서 한 숫자로 보여 준다.
    //  - 30개가 모두 쓰이는 중이면 가장 오래된 숫자를 재활용한다.
    //  - 이 공격으로 적이 죽으면 숫자가 더 크고 노랗게 보인다. (숫자는 남은 체력이 아니라 "이번 공격의 피해량"을 보여 준다)
    public class DamageNumberDisplay : MonoBehaviour
    {
        // 표시용 프리팹의 Resources 이름과, 숫자를 띄울 씬 이름.
        private const string LayerResourceName = "DamageNumberLayer";
        private const string InGameSceneName = "InGame";

        // 숫자에 쓸 글꼴. 웹 빌드에서는 컴퓨터 글꼴을 못 쓰므로 프로젝트의 Pretendard 글꼴을 프리팹에서 지정해 둔다.
        [SerializeField] private Font font;

        // 미리 만들어 둘 숫자 개수. 이보다 많이 동시에 뜨면 가장 오래된 것을 재활용한다.
        [SerializeField] private int poolSize = 30;

        // 숫자가 화면에 떠 있는 시간(초).
        [SerializeField] private float lifetime = 0.6f;

        // 숫자가 떠오르는 높이(화면 픽셀, 기준 해상도 1920x1080 기준).
        [SerializeField] private float risePixels = 70f;

        // 같은 적이 이 시간(초) 안에 또 맞으면 숫자를 합친다.
        [SerializeField] private float mergeWindow = 0.2f;

        // 숫자 크기(일반 / 마지막 일격 / 플레이어가 맞을 때).
        [SerializeField] private int normalFontSize = 26;
        [SerializeField] private int killFontSize = 38;
        [SerializeField] private int playerFontSize = 32;

        // 회복 숫자 크기. 플레이어가 맞을 때 숫자와 비슷하게 읽히게 한다.
        [SerializeField] private int healFontSize = 30;

        // 숫자 색(일반 / 마지막 일격 / 플레이어가 맞을 때 / 회복).
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color killColor = new Color(1f, 0.88f, 0.25f, 1f);
        [SerializeField] private Color playerColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color healColor = new Color(0.4f, 1f, 0.5f, 1f);

        // 숫자 하나의 상태. 풀(pool) 안의 칸 하나에 대응한다.
        private class Entry
        {
            public Text text;
            public RectTransform rect;
            public bool active;
            public float age;
            public float value;          // 합쳐진 피해량(소수 포함, 표시할 때 반올림)
            public EnemyAI owner;        // 어느 적의 숫자인지(합치기 판단용, 플레이어 숫자는 null)
            public Vector3 anchor;       // 숫자가 떠오르기 시작하는 월드 위치(적의 머리 위)
            public float xJitter;        // 숫자끼리 겹치지 않게 좌우로 흩뿌리는 픽셀 값
            public bool isKill;
            public bool isPlayer;
            public bool isHeal;          // 플레이어가 회복한 양(초록 "+숫자")
        }

        private Entry[] entries;
        private Canvas canvas;
        private Camera cam;
        private Health playerHealth;
        private Transform playerTransform;

        // ── 게임이 켜질 때 한 번 불려서, InGame 씬이 열릴 때마다 표시용 오브젝트를 만들도록 등록한다 ──
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != InGameSceneName) return;

            var prefab = Resources.Load<GameObject>(LayerResourceName);
            if (prefab == null) return; // 프리팹이 없으면 숫자 없이 예전처럼 동작한다.

            var go = Instantiate(prefab);
            // InGame 씬에 소속시켜서, 씬을 나갈 때 같이 사라지게 한다.
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            entries = new Entry[poolSize];
            for (int i = 0; i < poolSize; i++) entries[i] = CreateEntry(i);
        }

        // 숫자 하나(Text 오브젝트)를 만들어 풀에 넣는다. 처음에는 꺼 둔다.
        private Entry CreateEntry(int index)
        {
            var go = new GameObject("Number" + index, typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var rect = go.GetComponent<RectTransform>();
            // 왼쪽 아래를 기준점으로 해서, anchoredPosition에 화면 좌표(픽셀)를 그대로 넣을 수 있게 한다.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200f, 60f);

            var text = go.AddComponent<Text>();
            text.font = font;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false; // 숫자가 마우스 클릭을 가로채지 않게 한다.
            text.fontStyle = FontStyle.Bold;

            // 어두운 바닥과 밝은 바닥 모두에서 읽히도록 검은 테두리를 두른다.
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            go.SetActive(false);
            return new Entry { text = text, rect = rect };
        }

        private void OnEnable()
        {
            EnemyAI.DamageDealt += HandleEnemyDamaged;
        }

        private void OnDisable()
        {
            EnemyAI.DamageDealt -= HandleEnemyDamaged;
            if (playerHealth != null)
            {
                playerHealth.OnDamaged -= HandlePlayerDamaged;
                playerHealth.OnHealed -= HandlePlayerHealed;
            }
        }

        private void Start()
        {
            // 플레이어가 맞을 때 빨간 숫자를 띄우려고 플레이어의 체력 컴포넌트에 연결한다.
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                playerHealth = playerObj.GetComponent<Health>();
                if (playerHealth != null)
                {
                    playerHealth.OnDamaged += HandlePlayerDamaged;
                    playerHealth.OnHealed += HandlePlayerHealed;
                }
            }
        }

        // ── 신호 받기 ──

        private void HandleEnemyDamaged(EnemyAI enemy, float amount, bool lethal)
        {
            if (enemy == null) return;

            // 같은 적의 숫자가 아직 갓 떠올랐다면 새로 만들지 않고 합친다.
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.active && !e.isPlayer && e.owner == enemy && e.age < mergeWindow)
                {
                    e.value += amount;
                    if (lethal) e.isKill = true;
                    ApplyStyle(e);
                    return;
                }
            }

            // 적의 머리 위 위치: 적 크기(보스는 크다)에 비례해서 정한다.
            Vector3 anchor = enemy.transform.position + Vector3.up * (Mathf.Abs(enemy.transform.lossyScale.y) * 1.2f);
            Show(amount, anchor, enemy, lethal, false);
        }

        private void HandlePlayerDamaged(float amount)
        {
            if (playerTransform == null) return;
            Show(amount, playerTransform.position + Vector3.up * 2.2f, null, false, true);
        }

        // 플레이어가 체력을 회복했을 때(회복 드론) 머리 위로 초록 "+숫자"를 띄운다. amount는 실제로 채워진 양이다.
        private void HandlePlayerHealed(float amount)
        {
            if (playerTransform == null) return;
            Show(amount, playerTransform.position + Vector3.up * 2.2f, null, false, false, true);
        }

        // ── 숫자 띄우기 ──

        private void Show(float amount, Vector3 anchor, EnemyAI owner, bool isKill, bool isPlayer, bool isHeal = false)
        {
            var e = GetFreeEntry();
            e.active = true;
            e.age = 0f;
            e.value = amount;
            e.owner = owner;
            e.anchor = anchor;
            e.xJitter = Random.Range(-28f, 28f);
            e.isKill = isKill;
            e.isPlayer = isPlayer;
            e.isHeal = isHeal;
            e.rect.gameObject.SetActive(true);
            ApplyStyle(e);
            UpdatePosition(e); // 첫 프레임에 엉뚱한 위치(0,0)에서 깜빡이지 않도록 바로 위치를 잡는다.
        }

        // 쉬고 있는 칸을 찾고, 모두 쓰이는 중이면 가장 오래된 칸을 재활용한다.
        private Entry GetFreeEntry()
        {
            Entry oldest = entries[0];
            for (int i = 0; i < entries.Length; i++)
            {
                if (!entries[i].active) return entries[i];
                if (entries[i].age > oldest.age) oldest = entries[i];
            }
            return oldest;
        }

        // 숫자의 글자·크기·색을 현재 상태에 맞게 바꾼다.
        private void ApplyStyle(Entry e)
        {
            if (e.isHeal)
            {
                // 회복량은 1~3처럼 작아서 정수로 반올림하면 "+1"만 계속 보인다. 10 미만은 소수 첫째 자리까지 보여 준다(1.2 → "+1.2", 2.0 → "+2").
                e.text.text = "+" + (e.value < 10f ? e.value.ToString("0.#") : Mathf.RoundToInt(e.value).ToString());
                e.text.fontSize = healFontSize;
                e.text.color = healColor;
                return;
            }

            e.text.text = Mathf.Max(1, Mathf.RoundToInt(e.value)).ToString();

            if (e.isPlayer)
            {
                e.text.fontSize = playerFontSize;
                e.text.color = playerColor;
            }
            else if (e.isKill)
            {
                e.text.fontSize = killFontSize;
                e.text.color = killColor;
            }
            else
            {
                e.text.fontSize = normalFontSize;
                e.text.color = normalColor;
            }
        }

        // ── 매 프레임: 떠오르게 하고, 점점 투명하게 하고, 끝나면 풀로 돌려보낸다 ──

        private void Update()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (!e.active) continue;

                // 시간이 멈추는 동안(일시정지·레벨업 선택)에는 숫자도 같이 멈춘다.
                e.age += Time.deltaTime;
                if (e.age >= lifetime)
                {
                    e.active = false;
                    e.owner = null;
                    e.rect.gameObject.SetActive(false);
                    continue;
                }

                UpdatePosition(e);

                // 앞 60%는 선명하게, 뒤 40%에 걸쳐 투명해진다.
                float t = e.age / lifetime;
                float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                Color c = e.text.color;
                c.a = alpha;
                e.text.color = c;

                // 처음 0.1초는 살짝 커졌다가 원래 크기로 돌아오는 "톡" 튀는 느낌.
                float pop = e.age < 0.1f ? Mathf.Lerp(1.35f, 1f, e.age / 0.1f) : 1f;
                e.rect.localScale = Vector3.one * pop;
            }
        }

        // 숫자의 화면 위치를 정한다. 적이 아직 살아 있으면 머리 위를 따라가고, 죽었으면 마지막 위치에 남는다.
        private void UpdatePosition(Entry e)
        {
            if (e.owner != null) e.anchor = e.owner.transform.position + Vector3.up * (Mathf.Abs(e.owner.transform.lossyScale.y) * 1.2f);

            Vector3 screen = cam.WorldToScreenPoint(e.anchor);

            // 카메라 뒤쪽이거나 화면 밖이면 숨긴다.
            bool visible = screen.z > 0f && screen.x > -50f && screen.x < Screen.width + 50f && screen.y > -50f && screen.y < Screen.height + 50f;
            if (e.text.enabled != visible) e.text.enabled = visible;
            if (!visible) return;

            float rise = risePixels * canvas.scaleFactor * Mathf.Sin(Mathf.Clamp01(e.age / lifetime) * Mathf.PI * 0.5f); // 처음엔 빨리, 끝엔 천천히 오른다.
            Vector2 pos = new Vector2(screen.x + e.xJitter * canvas.scaleFactor, screen.y + rise);
            e.rect.anchoredPosition = pos / canvas.scaleFactor;
        }
    }
}
