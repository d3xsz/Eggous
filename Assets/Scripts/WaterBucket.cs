using UnityEngine;
using UnityEngine.InputSystem;

// 4.3 Sabotajci kovasi (SIRA PLANI adim 4): golun yaninda duran KOVA'yi E ile alirsin, elinde tasirsin.
// Golun yaninda SOL TIK basili tut = doldur. Baska yerde yerlestirilmis balyaya bakip SOL TIK = dok (balya islanir, WetMark).
// X = kovayi birak (yerine doner). Sadece rolu Sabotajci olan oyuncuda calisir. F artik kullanilmiyor.
public class WaterBucket : MonoBehaviour
{
    public int capacity = 3;
    public float fillSeconds = 1.2f;   // 1 birim su icin sure
    public float wetSeconds = 60f;
    public float pourRange = 5f;
    public float lakeMargin = 3f;      // golun kenarindan ne kadar uzakta sayilir
    public float pickupRange = 3.5f;
    public Vector3 handLocalPos = new Vector3(0.5f, -0.4f, 1.0f);
    public float water;

    public bool HasBucket { get; private set; }
    public int usedFrame = -1;          // eski (PlayerCombat uyumu icin), artik kullanilmiyor
    public bool WantsF() { return false; } // eski, artik F yok

    Collider lake;
    Camera cam;
    BaleCarrier carrier;
    PlayerCombat combat;
    BucketStand looked, stand;
    GameObject handRoot, waterVis;

    void Start()
    {
        var g = GameObject.Find("GolAlani");
        if (g != null) lake = g.GetComponent<Collider>();
        cam = GetComponentInChildren<Camera>();
        carrier = GetComponent<BaleCarrier>();
        combat = GetComponent<PlayerCombat>();
        water = 0f; // kova basta bos (sahnede kayitli deger onemsiz)
    }

    bool Active()
    {
        return GameManager.Instance != null && GameManager.Instance.IsPlaying
            && RoleManager.GetRole(gameObject) == Role.Sabotajci
            && !VoteSystem.IsEliminated(gameObject)
            && !(combat != null && combat.IsAsleep);
    }

    bool NearLake()
    {
        if (lake == null) return false;
        var b = lake.bounds;
        b.Expand(lakeMargin * 2f);
        return b.Contains(transform.position);
    }

    void Update()
    {
        if (!Active()) { if (HasBucket) Unequip(); looked = null; return; }
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null || cam == null || carrier == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (!HasBucket)
        {
            looked = null;
            RaycastHit hit;
            if (!carrier.IsCarrying && !carrier.HandsBusy
                && Physics.SphereCast(cam.transform.position, 0.3f, cam.transform.forward, out hit, pickupRange, ~0, QueryTriggerInteraction.Ignore))
                looked = hit.collider.GetComponentInParent<BucketStand>();
            if (looked != null && kb.eKey.wasPressedThisFrame) Equip(looked);
            return;
        }

        if (kb.xKey.wasPressedThisFrame) { Unequip(); return; }

        bool nearLake = NearLake();
        if (nearLake && LookingAtBale())
        {
            // gole yakin ama balyaya bakiyor: sol tik = dok (dolum degil). Baslangic alani golun yaninda, orada dokulemiyordu.
            if (mouse.leftButton.wasPressedThisFrame && water >= 1f) Pour();
        }
        else if (nearLake)
        {
            if (mouse.leftButton.isPressed && water < capacity) water = Mathf.Min(capacity, water + Time.deltaTime / fillSeconds);
        }
        else if (mouse.leftButton.wasPressedThisFrame && water >= 1f) Pour();

        UpdateVisual();
    }

    // Nisan alinan sey dokulebilir bir balya mi? (ag: NetBale, tek oyuncu: yerlestirilmis BaleState)
    bool LookingAtBale()
    {
        if (cam == null) return false;
        RaycastHit hit;
        if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, pourRange, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (hit.collider.GetComponentInParent<NetBale>() != null) return true;
        var b = hit.collider.GetComponentInParent<BaleState>();
        return b != null && b.placed;
    }

    void Equip(BucketStand s)
    {
        stand = s;
        water = s.water;
        s.Hide();
        NetWorld.BucketTake(s);
        HasBucket = true;
        carrier.HandsBusy = true;
        BuildVisual();
    }

    // Vurulunca / uyuyunca / X: kova yerine doner (su kovada kalir)
    public void Unequip()
    {
        if (!HasBucket) return;
        HasBucket = false;
        if (carrier != null) carrier.HandsBusy = false;
        if (stand != null) DropStand();
        stand = null;
        if (handRoot != null) Destroy(handRoot);
        handRoot = null;
        waterVis = null;
    }

    const float BH = 0.44f, BRb = 0.17f, BRt = 0.235f, BWall = 0.012f; // KovaGovde mesh'iyle ayni

    void BuildVisual()
    {
        handRoot = new GameObject("ElKova");
        handRoot.transform.SetParent(cam.transform, false);
        handRoot.transform.localPosition = new Vector3(0.46f, -0.5f, 0.95f);
        handRoot.transform.localRotation = Quaternion.Euler(16f, -32f, 0f);

        var bucket = new GameObject("KovaGovde");
        bucket.transform.SetParent(handRoot.transform, false);
        bucket.transform.localPosition = new Vector3(0f, -0.11f, 0f);
        bucket.transform.localScale = Vector3.one * 0.5f;
        AddMesh(bucket, Resources.Load<Mesh>("Kova/KovaGovde"), Resources.Load<Material>("Kova/Kova"));

        var handle = new GameObject("Kulp");
        handle.transform.SetParent(bucket.transform, false);
        AddMesh(handle, Resources.Load<Mesh>("Kova/KovaKulp"), Resources.Load<Material>("Kova/KovaKulp"));

        waterVis = new GameObject("Su");
        waterVis.transform.SetParent(bucket.transform, false);
        AddMesh(waterVis, Resources.Load<Mesh>("Kova/KovaSuDisk"), Resources.Load<Material>("Kova/KovaSu"));
        UpdateVisual();
    }

    static void AddMesh(GameObject g, Mesh m, Material mat)
    {
        g.AddComponent<MeshFilter>().sharedMesh = m;
        var r = g.AddComponent<MeshRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void UpdateVisual()
    {
        if (waterVis == null) return;
        if (handRoot != null) handRoot.transform.localRotation = Quaternion.Euler(16f + Mathf.Sin(Time.time * 1.7f) * 1.2f, -32f, Mathf.Sin(Time.time * 1.3f) * 2f);
        float frac = capacity > 0 ? Mathf.Clamp01(water / capacity) : 0f;
        waterVis.GetComponent<Renderer>().enabled = frac > 0.01f;
        float y = 0.03f + 0.31f * frac;
        float rad = Mathf.Lerp(BRb - BWall, BRt - BWall, y / BH) - 0.004f;
        waterVis.transform.localPosition = new Vector3(0f, y, 0f);
        waterVis.transform.localScale = new Vector3(rad, 1f, rad);
    }

    // Kovayi onune, zemine (ya da balyanin ustune) birakir; su kovada kalir
    void DropStand()
    {
        Vector3 pos = transform.position + transform.forward * 1.1f;
        RaycastHit h;
        if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out h, 30f, ~0, QueryTriggerInteraction.Ignore)) pos = h.point;
        stand.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        stand.water = water;
        water = 0f;
        stand.Show();
        NetWorld.BucketDrop(stand);
    }

    void Pour()
    {
        if (cam == null) return;
        RaycastHit hit;
        if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, pourRange, ~0, QueryTriggerInteraction.Ignore)) { Debug.Log("[Kova] dokme: karsida bir sey yok (menzil " + pourRange + ")"); return; }
        var nb = hit.collider.GetComponentInParent<NetBale>();
        if (nb != null && NetWorld.Active) { NetWorld.Wet(nb.NetworkObject, wetSeconds); water -= 1f; return; }
        var bale = hit.collider.GetComponentInParent<BaleState>();
        if (bale == null || !bale.placed) { Debug.Log("[Kova] dokme: baktigin sey dokulebilir balya degil -> " + hit.collider.name); return; }
        WetMark.Apply(bale.gameObject, wetSeconds);
        water -= 1f;
    }

    // Gecici ipucu (Faz 6'da guzellestirilecek)
    void OnGUI()
    {
        if (!Active()) return;
        if (!HasBucket)
        {
            if (looked != null) GUI.Label(GameHUD.Prompt("E: Kovayi al", new Rect(Screen.width / 2f - 80, Screen.height / 2f + 30, 300, 24)), "E: Kovayi al");
            return;
        }
        string s = "Kova: " + Mathf.FloorToInt(water) + "/" + capacity
            + (NearLake() && !LookingAtBale() ? "  (sol tik basili tut: doldur)" : "  (sol tik: baktigin balyaya dok)") + "   X: birak";
        GUI.Label(GameHUD.Active ? Rect.zero : new Rect(15, Screen.height - 110, 520, 24), s);
    }
}
