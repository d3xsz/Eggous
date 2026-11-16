using UnityEngine;

// Kova nesnesi (yerde duran/birakilan kova). Alininca gizlenir; birakilinca birakilan yerde belirir, icindeki su seviyesini gosterir.
// Su kovanin kendisinde kalir (water): tekrar alinca ayni su devam eder.
public class BucketStand : MonoBehaviour
{
    public float water;       // kovadaki su (birim)
    public int capacity = 3;

    Renderer[] rends;
    Collider[] cols;
    Transform su;

    const float H = 0.44f, Rb = 0.17f, Rt = 0.235f, Wall = 0.012f; // KovaGovde mesh'iyle ayni

    void Awake()
    {
        rends = GetComponentsInChildren<Renderer>();
        cols = GetComponentsInChildren<Collider>();
        su = transform.Find("Su");
        ApplyFill();
    }

    public void Hide()
    {
        foreach (var r in rends) r.enabled = false;
        foreach (var c in cols) c.enabled = false;
    }

    public void Show()
    {
        foreach (var r in rends) r.enabled = true;
        foreach (var c in cols) c.enabled = true;
        ApplyFill();
    }

    public void ApplyFill()
    {
        if (su == null) return;
        float f = capacity > 0 ? Mathf.Clamp01(water / capacity) : 0f;
        var r = su.GetComponent<Renderer>();
        if (r != null) r.enabled = f > 0.01f;
        float y = 0.03f + 0.31f * f;
        float rad = Mathf.Lerp(Rb - Wall, Rt - Wall, y / H) - 0.004f;
        su.localPosition = new Vector3(0f, y, 0f);
        su.localScale = new Vector3(rad, 1f, rad);
    }
}
