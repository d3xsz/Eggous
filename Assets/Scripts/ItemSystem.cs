using UnityEngine;
using UnityEngine.InputSystem;

public enum ItemType { None, CivcivRoketi, UykuTuyu, YagLekesi, TozBulutu }

// 5.2 Item sistemi (insan oyuncu): 2 SLOTLU envanter. 1 / 2 tuslari = o slottaki item'i eline al (tekrar basinca kaldir),
// SOL TIK = kullan. Item elindeyken baska sey (balya/nesne/kova) tasinamaz (BaleCarrier.HandsBusy).
public class ItemSystem : MonoBehaviour
{
    public static ItemSystem Local;                       // insan oyuncunun envanteri
    public ItemType[] slots = new ItemType[2];
    public int selected = -1;                             // elde olan slot (-1: yok)
    public ItemType Held { get { return selected >= 0 ? slots[selected] : ItemType.None; } }
    public bool Equipped { get { return selected >= 0 && slots[selected] != ItemType.None; } }
    public bool HasFreeSlot { get { foreach (var s in slots) if (s == ItemType.None) return true; return false; } }

    [Header("Civciv Roketi")] public float jetSeconds = 2.5f;
    [Header("Uyku Tuyu")] public float sleepRange = 4f; public float sleepSeconds = 4f;
    [Header("Yag Lekesi")] public float oilRadius = 1.8f; public float oilSeconds = 20f;
    [Header("Toz Bulutu")] public float dustRadius = 3.2f; public float dustSeconds = 6f;
    [Header("Elde gorunum")] public Vector3 handLocalPos = new Vector3(0.45f, -0.35f, 0.9f);

    PlayerController pc;
    PlayerCombat combat;
    BaleCarrier carrier;
    Camera cam;
    GameObject handVisual;
    string msg;
    float msgUntil, fog;

    public static string Name(ItemType t)
    {
        switch (t)
        {
            case ItemType.CivcivRoketi: return "Civciv Roketi";
            case ItemType.UykuTuyu: return "Uyku Tüyü";
            case ItemType.YagLekesi: return "Yağ Lekesi";
            case ItemType.TozBulutu: return "Toz Bulutu";
        }
        return "yok";
    }

    void Awake()
    {
        pc = GetComponent<PlayerController>();
        combat = GetComponent<PlayerCombat>();
        carrier = GetComponent<BaleCarrier>();
        if (!GameManager.NetMode && (carrier == null || !carrier.isBot)) Local = this;   // ag: Local'i NetPlayer (sahip) atar; uzak kopyalar ezmesin
    }

    void Start() { cam = carrier != null ? carrier.cam : Camera.main; }

    // Bos slota koyar. Dolu ise false.
    public bool Give(ItemType t)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == ItemType.None) { slots[i] = t; Say(Name(t) + " -> slot " + (i + 1) + " (tus: " + (i + 1) + ")"); AutoEquip(i); return true; }
        Say("Envanter dolu!");
        return false;
    }

    // Item alinca (sandik / kutu) 1'e basmaya gerek kalmadan direkt ele gelir. El doluysa (balya/nesne/kova/baska item) sadece envantere girer.
    void AutoEquip(int i)
    {
        if (carrier == null || cam == null || Equipped) return;
        if (carrier.IsCarrying || carrier.HandsBusy) return;
        if (combat != null && combat.IsAsleep) return;
        Select(i);
    }

    public void Say(string s) { msg = s; msgUntil = Time.time + 2.5f; }

    void Update()
    {
        Vector3 eye = cam != null ? cam.transform.position : transform.position;
        bool inDust = DustCloud.IsInside(eye);
        fog = Mathf.MoveTowards(fog, inDust ? 0.95f : 0f, Time.deltaTime * (inDust ? 4f : 1.5f));

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;
        bool playing = GameManager.Instance == null || GameManager.Instance.IsPlaying;
        bool blocked = !playing || (combat != null && combat.IsAsleep) || VoteSystem.IsEliminated(gameObject);
        if (blocked) { if (Equipped) Unequip(); return; }
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Select(0);
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Select(1);
        if (Equipped && mouse.leftButton.wasPressedThisFrame) Use();
    }

    void Select(int i)
    {
        if (i < 0 || i >= slots.Length || carrier == null || cam == null) return;
        if (slots[i] == ItemType.None) { Say("Slot " + (i + 1) + " bos"); return; }
        if (selected == i) { Unequip(); return; }                          // ayni tus: kaldir
        bool was = Equipped;
        if (!was && (carrier.IsCarrying || carrier.HandsBusy)) { Say("Once elini bosalt (balya / nesne)"); return; }
        if (handVisual != null) Destroy(handVisual);
        handVisual = null;
        selected = i;                                                       // baska slottan gecis: el zaten bizim
        carrier.HandsBusy = true;
        SpawnVisual();
    }

    // Vurulunca / uyuyunca / kaldirinca: item'lar envanterde kalir, el bosalir
    public void Unequip()
    {
        if (selected < 0) return;
        selected = -1;
        if (carrier != null) carrier.HandsBusy = false;
        if (handVisual != null) Destroy(handVisual);
        handVisual = null;
    }

    void SpawnVisual()
    {
        ItemType h = Held;
        PrimitiveType prim = (h == ItemType.CivcivRoketi || h == ItemType.UykuTuyu) ? PrimitiveType.Capsule : PrimitiveType.Sphere;
        var g = GameObject.CreatePrimitive(prim);
        Destroy(g.GetComponent<Collider>());
        g.name = "ElIteme_" + h;
        Color col = Color.white;
        switch (h)
        {
            case ItemType.CivcivRoketi: col = new Color(0.9f, 0.2f, 0.2f); g.transform.localScale = new Vector3(0.14f, 0.3f, 0.14f); break;
            case ItemType.UykuTuyu: col = new Color(0.95f, 0.95f, 1f); g.transform.localScale = new Vector3(0.06f, 0.32f, 0.16f); break;
            case ItemType.YagLekesi: col = new Color(0.08f, 0.08f, 0.1f); g.transform.localScale = Vector3.one * 0.24f; break;
            case ItemType.TozBulutu: col = new Color(0.6f, 0.58f, 0.52f); g.transform.localScale = Vector3.one * 0.26f; break;
        }
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = col;
        g.GetComponent<Renderer>().sharedMaterial = m;
        g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.transform.SetParent(cam.transform, false);
        g.transform.localPosition = handLocalPos;
        g.transform.localRotation = Quaternion.Euler(20f, 0f, 15f);
        handVisual = g;
    }

    void Use()
    {
        bool used = false;
        switch (Held)
        {
            case ItemType.CivcivRoketi:
                if (pc != null) { pc.jetUntil = Time.time + jetSeconds; Say("Civciv Roketi! Boşluk tuşunu basılı tut ("+ jetSeconds + " sn)"); used = true; }
                break;
            case ItemType.UykuTuyu:
                if (TrySleep()) used = true; else Say("Hedef yok! (önünde biri olmalı)");
                break;
            case ItemType.YagLekesi:
                { Vector3 op = GroundPoint(transform.position + transform.forward * 1.6f); OilSpot.Create(op, oilRadius, oilSeconds); NetWorld.Oil(op, oilRadius, oilSeconds); }
                Say("Yağ lekesi bırakıldı"); used = true;
                break;
            case ItemType.TozBulutu:
                { Vector3 dp = transform.position + transform.forward * 3f + Vector3.up * 1.2f; DustCloud.Create(dp, dustRadius, dustSeconds); NetWorld.Dust(dp, dustRadius, dustSeconds); }
                Say("Toz bulutu!"); used = true;
                break;
        }
        if (used) { slots[selected] = ItemType.None; Unequip(); }
    }

    static Vector3 GroundPoint(Vector3 p)
    {
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out hit, 8f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
        return p;
    }

    bool TrySleep()
    {
        Transform look = cam != null ? cam.transform : transform;
        Vector3 flat = new Vector3(look.forward.x, 0f, look.forward.z);
        if (flat.sqrMagnitude < 0.001f) flat = transform.forward;
        flat.Normalize();
        PlayerCombat best = null;
        float bd = 999f;
        foreach (var o in PlayerCombat.All)
        {
            if (o == combat) continue;
            Vector3 to = o.transform.position - transform.position;
            Vector3 tf = new Vector3(to.x, 0f, to.z);
            float d = tf.magnitude;
            if (Mathf.Abs(to.y) > 1.6f || d > sleepRange) continue;
            if (d > 0.4f && Vector3.Dot(flat, tf.normalized) < 0.3f) continue;
            if (d < bd) { bd = d; best = o; }
        }
        if (best == null) return false;
        best.Sleep(sleepSeconds, combat);
        return true;
    }

    // Item satiri artik GameHUD'da (iki slot). Burada sadece toz perdesi, roket sayaci ve kisa mesaj.
    void OnGUI()
    {
        if (fog > 0.01f)
        {
            GUI.color = new Color(0.75f, 0.75f, 0.72f, fog);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        if (pc != null && Time.time < pc.jetUntil) GUI.Label(new Rect(15, Screen.height - 86, 300, 24), "Roket: " + (pc.jetUntil - Time.time).ToString("F1") + " sn");
        if (Time.time < msgUntil) GUI.Label(new Rect(Screen.width / 2f - 220, Screen.height / 2f + 90, 440, 24), msg);
    }
}
