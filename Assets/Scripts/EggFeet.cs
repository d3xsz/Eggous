using UnityEngine;

// Yumurta ayak animasyonu: yururken ayaklar sirayla one atilir/kalkar (kayiyormus gibi gorunmesin).
// 'Ayak' isimli iki cocuk transform'u (x<0 sol, x>0 sag) kullanir. Durunca ayaklar yerine doner.
public class EggFeet : MonoBehaviour
{
    public float cyclesPerMeter = 0.6f;  // metre basina adim dongusu
    public float stride = 0.2f;          // ayagin one/arkaya savrulma mesafesi
    public float lift = 0.1f;            // ayagin yerden kalkma yuksekligi
    public float fullSpeed = 3f;         // bu hizda animasyon tam genlikte
    public float returnSpeed = 12f;

    Transform left, right;
    Vector3 lBase, rBase;
    float phase, speed;
    Vector3 last, dirLocal = Vector3.forward;

    void Start()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "Ayak") continue;
            if (t.localPosition.x < 0f) left = t; else right = t;
        }
        if (left != null) lBase = left.localPosition;
        if (right != null) rBase = right.localPosition;
        last = transform.position;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector3 d = transform.position - last;
        d.y = 0f;
        last = transform.position;
        float v = d.magnitude / dt;
        speed = Mathf.Lerp(speed, v, 12f * dt);
        if (v > 0.2f)
        {
            Vector3 dl = transform.InverseTransformDirection(d.normalized);
            dl.y = 0f;
            if (dl.sqrMagnitude > 0.01f) dirLocal = Vector3.Lerp(dirLocal, dl.normalized, 10f * dt).normalized;
        }
        float k = Mathf.Clamp01(speed / fullSpeed);
        phase += speed * cyclesPerMeter * 2f * Mathf.PI * dt;
        Animate(left, lBase, phase, k, dt);
        Animate(right, rBase, phase + Mathf.PI, k, dt);
    }

    void Animate(Transform f, Vector3 b, float ph, float k, float dt)
    {
        if (f == null) return;
        Vector3 target = b;
        if (k > 0.05f)
        {
            target += dirLocal * (Mathf.Sin(ph) * stride * k);
            target.y += Mathf.Max(0f, Mathf.Cos(ph)) * lift * k;
        }
        f.localPosition = Vector3.Lerp(f.localPosition, target, Mathf.Min(1f, returnSpeed * dt));
    }
}
