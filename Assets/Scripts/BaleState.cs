using UnityEngine;

// Yerlestirilmis balya: once fizikle oturur, durunca DONAR (kinematic) ki yol coksun.
// Islak balya (4.3) Unfreeze() ile serbest kalir; autoFreeze=false iken kaymaya devam eder.
public class BaleState : MonoBehaviour
{
    public bool placed;
    public GameObject placedBy; // kim yerlestirdi (4.1)
    public bool fell;
    public bool frozen;
    public bool autoFreeze = true;
    public float restTime = 0.4f;   // bu kadar sure durursa donar
    public float restSpeed = 0.08f; // bu hizin altinda 'duruyor' sayilir

    Rigidbody rb;
    float rest;

    void Awake() { rb = GetComponent<Rigidbody>(); }

    void Update()
    {
        if (!placed || fell) return;
        var tr = BaleTracker.Instance;
        if (tr == null) return;
        if (transform.position.y < tr.fallY) { fell = true; tr.ReportFell(this); }
    }

    void FixedUpdate()
    {
        if (!placed || frozen || !autoFreeze || rb == null) return;
        bool still = rb.linearVelocity.sqrMagnitude < restSpeed * restSpeed
                  && rb.angularVelocity.sqrMagnitude < restSpeed * restSpeed;
        if (still)
        {
            rest += Time.fixedDeltaTime;
            if (rest >= restTime) Freeze();
        }
        else rest = 0f;
    }

    public void Freeze()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        placed = true;
        frozen = true;
        rest = 0f;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    public void Unfreeze()
    {
        if (GameManager.NetMode && !GameManager.NetIsServer)   // ag: islanma vb. sunucuda uygulanir
        {
            var no = GetComponent<Unity.Netcode.NetworkObject>();
            if (no != null && NetGame.Instance != null) NetGame.Instance.UnfreezeBale(no);
            return;
        }
        if (rb == null) rb = GetComponent<Rigidbody>();
        frozen = false;
        rest = 0f;
        rb.isKinematic = false;
        rb.WakeUp();
    }
}
