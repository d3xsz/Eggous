using System.Collections.Generic;
using UnityEngine;

// 5.2 Yag lekesi: ustune basan kayar (SlipperyMover.OnWet bunu da sayar). oilSeconds sonra solar ve silinir.
public class OilSpot : MonoBehaviour
{
    static readonly List<OilSpot> all = new List<OilSpot>();
    static Material mat;
    float radius, curRadius, until;

    public static bool IsOn(Vector3 p)
    {
        foreach (var s in all)
        {
            Vector3 d = p - s.transform.position;
            if (Mathf.Abs(d.y) > 1.2f) continue;
            d.y = 0f;
            if (d.magnitude < s.curRadius) return true;
        }
        return false;
    }

    public static OilSpot Create(Vector3 pos, float r, float seconds)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = "YagLekesi";
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.position = pos + Vector3.up * 0.02f;
        g.transform.localScale = new Vector3(r * 2f, 0.01f, r * 2f);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.05f, 0.05f, 0.07f);
            mat.SetFloat("_Smoothness", 0.95f);
        }
        var rend = g.GetComponent<Renderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var s = g.AddComponent<OilSpot>();
        s.radius = r;
        s.curRadius = r;
        s.until = Time.time + seconds;
        return s;
    }

    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    void Update()
    {
        float left = until - Time.time;
        if (left <= 0f) { Destroy(gameObject); return; }
        float k = left < 2f ? left / 2f : 1f;
        curRadius = radius * k;
        transform.localScale = new Vector3(curRadius * 2f, 0.01f, curRadius * 2f);
    }
}
