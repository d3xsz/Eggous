using UnityEngine;
using UnityEngine.InputSystem;

// Oyuncunun balya alma/tasima davranisi. Ayni anda tek sey tasinir.
public class BaleCarrier : MonoBehaviour
{
    public Camera cam;
    public bool isBot; // bot: klavye okumaz, BotController yonetir
    public float pickupRange = 3.5f;
    public Vector3 holdLocalPos = new Vector3(0.7f, -0.6f, 1.6f);

    public GameObject HeldBale { get; private set; }
    public bool IsCarrying { get { return HeldBale != null || NetCarrying; } }
    public bool NetCarrying { get; set; }   // ag: uzaktaki oyuncunun balya tasidigi bilgisi (gorunum icin)
    void NetNotify(bool c) { var np = GetComponent<NetPlayer>(); if (np != null) np.SetCarrying(c); }
    public bool HandsBusy { get; set; } // baska bir sey (firlatilabilir nesne) tasiniyor

    BaleSpawner lookedSpawner;

    void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (isBot || kb == null) return;

        lookedSpawner = null;
        if (!IsCarrying && Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, pickupRange, ~0, QueryTriggerInteraction.Collide))
            lookedSpawner = hit.collider.GetComponentInParent<BaleSpawner>();

        if (kb.xKey.wasPressedThisFrame && IsCarrying && !AsleepNow()) Drop();
        if (kb.eKey.wasPressedThisFrame && !AsleepNow())
        {
            if (!IsCarrying && !HandsBusy && lookedSpawner != null) { if (TurnManager.CanAct(gameObject)) Pickup(lookedSpawner.Take()); }
            // tasirken SOL TIK = yerlestirme (PlacementPreview), X = birak
        }
    }

    bool AsleepNow() { var c = GetComponent<PlayerCombat>(); return c != null && c.IsAsleep; }

    public void Pickup(GameObject bale)
    {
        if (bale == null) return;
        HeldBale = bale;
        NetNotify(true);
        var rb = bale.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; rb.detectCollisions = false; }
        foreach (var c in bale.GetComponentsInChildren<Collider>()) c.enabled = false;
        bale.transform.SetParent(cam.transform, false);
        bale.transform.localPosition = holdLocalPos;
        bale.transform.localRotation = Quaternion.identity;
    }

    // Elden birakir, fizige gecer (yerlestirme sayilmaz)
    public void Drop()
    {
        if (HeldBale == null) return;
        var b = HeldBale;
        HeldBale = null;
        NetNotify(false);
        if (GameManager.NetMode && NetGame.Instance != null)   // ag: sunucu serbest balya dogurur
        {
            Vector3 dp = b.transform.position; Quaternion dr = b.transform.rotation;
            Destroy(b);
            NetGame.Instance.DropBale(dp, dr);
            return;
        }
        b.transform.SetParent(null, true);
        foreach (var c in b.GetComponentsInChildren<Collider>()) c.enabled = true;
        var rb = b.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = false; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.detectCollisions = true; rb.linearVelocity = Vector3.zero; }
    }

    // Yerlestirme (2.3) elindeki balyayi alip kendisi yonetir
    public GameObject Release()
    {
        var b = HeldBale;
        HeldBale = null;
        NetNotify(false);
        if (b != null) { b.transform.SetParent(null, true); var r = b.GetComponent<Rigidbody>(); if (r != null) { r.interpolation = RigidbodyInterpolation.Interpolate; r.detectCollisions = true; } }
        return b;
    }

    void OnGUI() { if (isBot) return; OnGUIHuman(); }

    void OnGUIHuman()
    {
        if (!IsCarrying && lookedSpawner != null)
            GUI.Label(GameHUD.Prompt((TurnManager.CanAct(gameObject) ? "E: Balya al" : "Sira sende degil"), new Rect(Screen.width / 2f - 60, Screen.height / 2f + 30, 200, 30)), (TurnManager.CanAct(gameObject) ? "E: Balya al" : "Sira sende degil"));
        else if (IsCarrying)
            GUI.Label(GameHUD.Active ? Rect.zero : new Rect(Screen.width / 2f - 60, Screen.height / 2f + 30, 200, 30), "Sol tik: yerlestir   X: birak");
    }
}
