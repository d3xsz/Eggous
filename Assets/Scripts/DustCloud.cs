using System.Collections.Generic;
using UnityEngine;

// 5.2 Toz bulutu: icindeki insan oyuncunun gorusu kapanir (ItemSystem gri perde cizer). dustSeconds sonra solar.
public class DustCloud : MonoBehaviour
{
    static readonly List<DustCloud> all = new List<DustCloud>();
    float radius, until, life;
    Material mat;

    public static bool IsInside(Vector3 p)
    {
        foreach (var c in all) if ((p - c.transform.position).magnitude < c.radius) return true;
        return false;
    }

    public static DustCloud Create(Vector3 pos, float r, float seconds)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = "TozBulutu";
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.position = pos;
        g.transform.localScale = Vector3.one * r * 2f;
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", 0);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        m.color = new Color(0.72f, 0.7f, 0.65f, 0.6f);
        var rend = g.GetComponent<Renderer>();
        rend.sharedMaterial = m;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var c = g.AddComponent<DustCloud>();
        c.radius = r;
        c.life = seconds;
        c.until = Time.time + seconds;
        c.mat = m;
        return c;
    }

    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    void Update()
    {
        float left = until - Time.time;
        if (left <= 0f) { Destroy(gameObject); return; }
        float a = left < 1.5f ? left / 1.5f : 1f;
        mat.color = new Color(0.72f, 0.7f, 0.65f, 0.6f * a);
        if (left < 1.5f) radius = Mathf.Min(radius, transform.localScale.x * 0.5f);
    }
}
