using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Rol karti: tur basinda insan oyuncu SADECE kendi rolunu, gizli gorevini ve ortak hedefi gorur.
// showSeconds sonra kapanir; Tab basili tutulunca tekrar gorunur.
public class RoleCardUI : MonoBehaviour
{
    [System.Serializable]
    public class RoleInfo
    {
        public Role role;
        [TextArea] public string task;
        public Color color = Color.white;
    }

    [Header("UI (sahnede kurulu)")]
    public CanvasGroup group;
    public Text title;
    public Text task;
    public Text goal;
    public Text hint;

    [Header("Metinler / ayar")]
    [TextArea] public string commonGoal = "Ortak hedef: sure dolmadan balyalardan yol kurup kuluckaya ulasmak.";
    public string hintText = "Tab (basili tut): rolunu tekrar gor";
    public float showSeconds = 6f;
    public float fadeSpeed = 4f;

    public RoleInfo[] infos = new RoleInfo[]
    {
        new RoleInfo { role = Role.Safe, color = new Color(0.45f, 0.85f, 0.5f),
            task = "Gizli gorevin: Sure dolmadan kuluckaya ulas ve sabotajciyi bul. Balyalari duzgun yerlestir." },
        new RoleInfo { role = Role.Tirmanici, color = new Color(1f, 0.65f, 0.25f),
            task = "Gizli gorevin: Safe gibi davran ama kuluckaya ILK ulasan sen ol. Ilk ulasirsan kazanirsin." },
        new RoleInfo { role = Role.Palyaco, color = new Color(0.75f, 0.5f, 1f),
            task = "Gizli gorevin: Masum gorun ama oylamayla elenmek iste. Elenirsen kesin kazanirsin." },
        new RoleInfo { role = Role.Sabotajci, color = new Color(1f, 0.35f, 0.35f),
            task = "Gizli gorevin: Balyalari hafif yamuk koy, golden su alip balyalara dok, gizli item kullan. Sure bitene kadar kimsenin cikmasini engelle." },
    };

    GameObject human;
    float timer;

    void OnEnable() { RoleManager.RolesAssigned += OnRoles; }
    void OnDisable() { RoleManager.RolesAssigned -= OnRoles; }

    void Start()
    {
        FindHuman();
        if (group != null) group.alpha = 0f;
        if (human != null && RoleManager.HasRole(human)) OnRoles();
    }

    void FindHuman()
    {
        var pc = PlayerController.LocalOrFind();
        human = pc != null ? pc.gameObject : null;
    }

    void OnRoles()
    {
        if (human == null) FindHuman();
        if (human == null || !RoleManager.HasRole(human)) return;
        Role r = RoleManager.GetRole(human);
        RoleInfo info = null;
        foreach (var i in infos) if (i.role == r) { info = i; break; }

        if (title != null)
        {
            title.text = RoleManager.DisplayName(r);
            title.color = info != null ? info.color : Color.white;
        }
        if (task != null) task.text = info != null ? info.task : "";
        if (goal != null) goal.text = commonGoal;
        if (hint != null) hint.text = hintText;
        timer = showSeconds;
    }

    void Update()
    {
        if (group == null) return;
        if (timer > 0f) timer -= Time.deltaTime;
        bool hold = Keyboard.current != null && Keyboard.current.tabKey.isPressed;
        bool playing = GameManager.Instance == null || GameManager.Instance.IsPlaying;
        bool hasRole = human != null && RoleManager.HasRole(human);
        bool show = playing && hasRole && (timer > 0f || hold);
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, fadeSpeed * Time.deltaTime);
    }
}
