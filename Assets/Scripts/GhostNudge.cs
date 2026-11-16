using UnityEngine;
using UnityEngine.InputSystem;

// 4.5 Hayalet modu (basit): oylamayla elenen INSAN oyuncu ucabilir (Bosluk basili) ve F ile yerlestirilmis balyalari /
// nesneleri hafifce karistirabilir. Balya koyamaz, vuramaz, item alamaz.
public class GhostNudge : MonoBehaviour
{
    public float range = 6f;
    public float cooldown = 1.5f;
    public float impulse = 2.5f;

    BaleCarrier carrier;
    PlayerController pc;
    Camera cam;
    float cd;

    bool Ghost { get { return VoteSystem.IsEliminated(gameObject); } }

    void Awake() { carrier = GetComponent<BaleCarrier>(); pc = GetComponent<PlayerController>(); }
    void Start() { cam = carrier != null ? carrier.cam : Camera.main; }

    void Update()
    {
        if (!Ghost || (carrier != null && carrier.isBot) || cam == null) return;
        if (cd > 0f) cd -= Time.deltaTime;
        if (pc != null) pc.jetUntil = Time.time + 0.5f; // Bosluk basili tutunca ucar
        var kb = Keyboard.current;
        if (kb == null) return;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && cd <= 0f && Cursor.lockState == CursorLockMode.Locked) Nudge();
    }

    void Nudge()
    {
        RaycastHit hit;
        if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, range, ~0, QueryTriggerInteraction.Ignore)) return;
        Vector3 push = cam.transform.forward * impulse + Vector3.up * 0.6f;
        var bs = hit.collider.GetComponentInParent<BaleState>();
        if (bs != null && bs.placed)
        {
            bs.Unfreeze();
            var rb = bs.GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(push, ForceMode.VelocityChange);
            cd = cooldown;
            return;
        }
        var th = hit.collider.GetComponentInParent<Throwable>();
        if (th != null)
        {
            var rb = th.GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(push, ForceMode.VelocityChange);
            cd = cooldown;
        }
    }

    void OnGUI()
    {
        if (!Ghost || (carrier != null && carrier.isBot)) return;
        GUI.Label(new Rect(Screen.width / 2f - 200, 14, 420, 24), "HAYALET: Bosluk = uc, Sol tik = balyalari karistir");
    }
}
