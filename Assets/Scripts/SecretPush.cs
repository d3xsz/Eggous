using UnityEngine;
using UnityEngine.InputSystem;

// 5.3 Gizli Itme (SADECE sabotajci): H. Vurmanin sessiz hali: yakindaki oyuncuyu hafifce iter (sersemletmez), yoksa baktigi
// yerlestirilmis balyayi hafifce kaydirir. Animasyon, ses, duyuru, efekt YOK: kimse gormez.
public class SecretPush : MonoBehaviour
{
    public float range = 3f;
    public float cooldown = 6f;
    public float pushSpeed = 11f;
    public float baleImpulse = 5f;

    BaleCarrier carrier;
    PlayerCombat combat;
    Camera cam;
    float cd; public float CdLeft { get { return cd; } }

    void Awake() { carrier = GetComponent<BaleCarrier>(); combat = GetComponent<PlayerCombat>(); }
    void Start() { cam = carrier != null ? carrier.cam : Camera.main; }

    bool Active()
    {
        return carrier != null && !carrier.isBot && RoleManager.GetRole(gameObject) == Role.Sabotajci && !VoteSystem.IsEliminated(gameObject);
    }

    void Update()
    {
        if (cd > 0f) cd -= Time.deltaTime;
        if (!Active() || cam == null) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        bool playing = GameManager.Instance == null || GameManager.Instance.IsPlaying;
        if (!playing || (combat != null && combat.IsAsleep)) return;
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame && cd <= 0f && Cursor.lockState == CursorLockMode.Locked) Do();
    }

    void Do()
    {
        Vector3 flat = new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z);
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
            if (Mathf.Abs(to.y) > 1.6f || d > range + 0.4f) continue;
            if (d > 0.4f && Vector3.Dot(flat, tf.normalized) < 0.25f) continue;
            if (d < bd) { bd = d; best = o; }
        }
        if (best != null) { best.ReceivePush(flat, pushSpeed); cd = cooldown; return; }

        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, range + 1f, ~0, QueryTriggerInteraction.Ignore))
        {
            var bs = hit.collider.GetComponentInParent<BaleState>();
            if (bs != null && bs.placed)
            {
                bs.Unfreeze(); // autoFreeze: durunca tekrar donar
                var rb = bs.GetComponent<Rigidbody>();
                if (rb != null) rb.AddForce(flat * baleImpulse, ForceMode.VelocityChange);
                cd = cooldown;
            }
        }
    }

    // Gecici UI: sadece sabotajci kendi ekraninda gorur
    void OnGUI()
    {
        if (!Active()) return;
        GUI.Label(GameHUD.Active ? Rect.zero : new Rect(15, Screen.height - 38, 300, 24), cd > 0f ? "Gizli Itme: " + cd.ToString("F1") + " sn" : "Gizli Itme: hazir (sag tik)");
    }
}
