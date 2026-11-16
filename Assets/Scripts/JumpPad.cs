using UnityEngine;

// SIRA PLANI adim 6: Ziplatici (zipzip). Ustune basan insan oyuncu, pad'in ONUNE (transform.forward) dogru firlar.
// Boslugu gecmek / yuksege cikmak icin. Dinamik nesneler (nesne, serbest balya) de firlar. Bot'lar etkilenmez.
[RequireComponent(typeof(Collider))]
public class JumpPad : MonoBehaviour
{
    public float upSpeed = 14f;
    public float forwardSpeed = 3f;   // az: ileri tasimaz, dikey yardimci (level design'da yuksek yerlerde)
    public float cooldown = 0.4f;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;

    float lastT = -10f;

    void OnTriggerEnter(Collider other)
    {
        if (Time.time - lastT < cooldown) return;
        Vector3 dir = transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
        dir.Normalize();

        var pc = other.GetComponentInParent<PlayerController>();
        if (pc != null)
        {
            pc.Launch(dir * forwardSpeed, upSpeed);
        }
        else
        {
            var rb = other.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;
            rb.linearVelocity = dir * forwardSpeed * 0.5f + Vector3.up * upSpeed * 0.8f;
        }
        lastT = Time.time;
        if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }
}
