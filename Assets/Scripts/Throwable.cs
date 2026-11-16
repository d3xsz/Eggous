using UnityEngine;

public enum ThrowKind { Misir, Elma, Kabak, Tas }

// 4.8 Firlatilabilir nesne (misir kocani, elma, kabak, tas). Firlatilinca carptigi oyuncuyu iter + sersemletir.
// Kullanilan nesne respawnSeconds sonra yerinde yeniden dogar.
[RequireComponent(typeof(Rigidbody))]
public class Throwable : MonoBehaviour
{
    public ThrowKind kind;
    public string displayName = "Nesne";
    public float throwSpeed = 14f;
    public float knockMult = 1f;       // PlayerCombat.knockbackSpeed carpani (kabak agir = cok iter, elma hafif)
    public float stunSeconds = 1f;
    public float respawnSeconds = 30f;
    public float fallY = -8f;

    [HideInInspector] public GameObject thrower;
    public bool held;

    Rigidbody rb;
    Vector3 homePos;
    Quaternion homeRot;
    bool inFlight, used;
    float flightUntil, usedAt;
    Vector3 lastVel;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        homePos = transform.position;
        homeRot = transform.rotation;
    }

    void FixedUpdate() { if (!held) lastVel = rb.linearVelocity; }

    void Update()
    {
        if (held) return;
        if (transform.position.y < fallY) { ResetHome(); return; }
        if (used && Time.time - usedAt > respawnSeconds) { ResetHome(); return; }
        if (inFlight && Time.time > flightUntil) inFlight = false;
    }

    public void OnPicked() { held = true; used = true; usedAt = Time.time; inFlight = false; }
    public void OnDropped() { held = false; inFlight = false; used = true; usedAt = Time.time; }
    public void OnThrown(GameObject by)
    {
        held = false;
        thrower = by;
        inFlight = true;
        flightUntil = Time.time + 2.5f;
        used = true;
        usedAt = Time.time;
    }

    // Ag: baska oyuncunun firlattigi kopya sadece gorsel/fizik (isabet SADECE firlatan istemcide hesaplanir)
    public void OnThrownRemote() { held = false; inFlight = false; used = true; usedAt = Time.time; }

    void ResetHome()
    {
        held = false;
        inFlight = false;
        used = false;
        thrower = null;
        transform.SetParent(null, true);
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = true;
        rb.isKinematic = false;
        rb.detectCollisions = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(homePos, homeRot);
    }

    void OnCollisionEnter(Collision c)
    {
        if (!inFlight) return;
        var victim = c.collider.GetComponentInParent<PlayerCombat>();
        if (victim == null) return;
        if (thrower != null && victim.gameObject == thrower) return;

        Vector3 dir = new Vector3(lastVel.x, 0f, lastVel.z);
        if (dir.sqrMagnitude < 0.01f) dir = victim.transform.position - transform.position;
        dir.y = 0f;
        dir.Normalize();
        PlayerCombat att = thrower != null ? thrower.GetComponent<PlayerCombat>() : null;
        victim.ReceiveHit(dir, att, knockMult, stunSeconds, displayName + " firlatti!");
        inFlight = false;
    }
}
