using UnityEngine;

// Islak balya = buz: ustundeyken CEKIS dusuk. Hiz, hedef hiza (inputun istedigi hiz) YAVASCA yaklasir:
// yavas hizlanirsin, duramazsin, donemezsin, hafif hizlanirsin. Balyadan inince cekis hemen normale doner.
// CharacterController kullanan herkese (insan + bot) eklenir.
[RequireComponent(typeof(CharacterController))]
public class SlipperyMover : MonoBehaviour
{
    public float traction = 0.6f;     // islak balyada cekis (dusuk = daha kaygan; 0.5 cok buzlu, 3 hafif kaygan)
    public float boost = 1.9f;        // islakken hedef hiz carpani (kayarken hafif hizlanma)
    public float dryTraction = 12f;   // balyadan inince normale donme hizi (yuksek = hemen normale doner)
    public float maxSpeed = 10f;
    public float airTraction = 0.5f;  // islak balyadan dusunce HAVADAYKEN kayma surer (dusuk = daha uzun suzulur)

    CharacterController cc;
    Vector3 last;
    Vector3 iceVel;
    bool wasWet;
    bool slipping;

    void Awake() { cc = GetComponent<CharacterController>(); }
    void Start() { last = transform.position; }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) { iceVel = Vector3.zero; last = transform.position; wasWet = false; slipping = false; return; }

        // Bu karede diger scriptin (PlayerController/BotController) yaptigi hareket
        Vector3 vel = (transform.position - last) / dt;
        vel.y = 0f;

        bool wet = OnWet();
        bool grounded = cc.isGrounded;
        if (wet) slipping = true;          // islak balyada kayiyoruz
        else if (grounded) slipping = false; // kuru zemine basinca normale don
        Vector3 target = wet ? vel * boost : vel;
        if (wet && !wasWet) iceVel = vel; // balyaya girerken ivme korunur
        float tr = wet ? traction : (slipping ? airTraction : dryTraction);
        iceVel = Vector3.Lerp(iceVel, target, Mathf.Min(1f, tr * dt));
        if (iceVel.magnitude > maxSpeed) iceVel = iceVel.normalized * maxSpeed;

        Vector3 extra = iceVel - vel;
        if (extra.sqrMagnitude > 0.0001f) cc.Move(new Vector3(extra.x, grounded ? -2f : 0f, extra.z) * dt);

        last = transform.position;
        wasWet = wet;
    }

    bool OnWet() { return OilSpot.IsOn(transform.position) || OnWetBale(); }

    bool OnWetBale()
    {
        var b = cc.bounds;
        Vector3 o = new Vector3(b.center.x, b.min.y + 0.1f, b.center.z);
        RaycastHit hit;
        if (Physics.Raycast(o, Vector3.down, out hit, 0.5f, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponentInParent<WetMark>() != null;
        return false;
    }
}
