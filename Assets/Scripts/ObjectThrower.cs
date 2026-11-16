using UnityEngine;
using UnityEngine.InputSystem;

// 4.8 Nesne tasima/firlatma (insan). E: al / birak, Sol tik: firlat (cooldown). Ayni anda tek sey tasinir (balya YA DA nesne).
// Botlar henuz nesne firlatmaz (md: 'basit tutulur', kesilebilir).
[RequireComponent(typeof(BaleCarrier))]
public class ObjectThrower : MonoBehaviour
{
    public float pickupRange = 3.5f;
    public float throwCooldown = 2f;
    public Vector3 holdLocalPos = new Vector3(0.5f, -0.35f, 1.0f);

    public Throwable Held { get; private set; }

    BaleCarrier carrier;
    Camera cam;
    Throwable looked;
    float cd;

    bool IsBot { get { return carrier != null && carrier.isBot; } }

    void Awake() { carrier = GetComponent<BaleCarrier>(); }
    void Start() { cam = carrier != null ? carrier.cam : GetComponentInChildren<Camera>(); }

    void Update()
    {
        if (cd > 0f) cd -= Time.deltaTime;
        if (IsBot || cam == null) return;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;
        bool playing = (GameManager.Instance == null || GameManager.Instance.IsPlaying) && !Blocked();

        looked = null;
        if (playing && Held == null && !carrier.IsCarrying && !carrier.HandsBusy
            && Physics.SphereCast(cam.transform.position, 0.25f, cam.transform.forward, out var hit, pickupRange, ~0, QueryTriggerInteraction.Ignore))
            looked = hit.collider.GetComponentInParent<Throwable>();
        if (!playing) return;

        if (kb.eKey.wasPressedThisFrame)
        {
            if (Held != null) Drop();
            else if (looked != null) Pickup(looked);
        }
        if (Held != null && cd <= 0f && Cursor.lockState == CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
            Throw();
    }

    bool Blocked() { var c = GetComponent<PlayerCombat>(); return (c != null && c.IsAsleep) || VoteSystem.IsEliminated(gameObject); }

    void Pickup(Throwable t)
    {
        Held = t;
        t.OnPicked();
        NetWorld.ThrowPick(t);
        var rb = t.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.detectCollisions = false;
        foreach (var c in t.GetComponentsInChildren<Collider>()) c.enabled = false;
        t.transform.SetParent(cam.transform, false);
        t.transform.localPosition = holdLocalPos;
        t.transform.localRotation = Quaternion.identity;
        carrier.HandsBusy = true;
    }

    Rigidbody Release()
    {
        var t = Held;
        Held = null;
        carrier.HandsBusy = false;
        t.transform.SetParent(null, true);
        foreach (var c in t.GetComponentsInChildren<Collider>()) c.enabled = true;
        var rb = t.GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.detectCollisions = true;
        rb.linearVelocity = Vector3.zero;
        return rb;
    }

    public void Drop()
    {
        if (Held == null) return;
        var t = Held;
        Release();
        t.OnDropped();
        NetWorld.ThrowDrop(t);
    }

    void Throw()
    {
        var t = Held;
        var rb = Release();
        rb.linearVelocity = cam.transform.forward * t.throwSpeed + Vector3.up * 1.5f;
        rb.angularVelocity = Random.insideUnitSphere * 8f;
        t.OnThrown(gameObject);
        NetWorld.ThrowThrow(t, rb.linearVelocity, rb.angularVelocity);
        cd = throwCooldown;
    }

    // Gecici UI (Faz 6 da guzellestirilecek)
    void OnGUI()
    {
        if (IsBot) return;
        var r = new Rect(Screen.width / 2f - 110, Screen.height / 2f + 60, 320, 24);
        if (Held != null)
            GUI.Label(GameHUD.Active ? Rect.zero : r, "Sol tik: firlat (" + Held.displayName + ")   E: birak" + (cd > 0f ? "   [bekle " + cd.ToString("F1") + " sn]" : ""));
        else if (looked != null)
            GUI.Label(GameHUD.Prompt("E: " + looked.displayName + " al", r), "E: " + looked.displayName + " al");
    }
}
