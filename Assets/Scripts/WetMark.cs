using System.Collections.Generic;
using UnityEngine;

// Islak balya (4.3): balya KAYMAZ, ustundekiler kayar (SlipperyMover).
// Gorsel (yuzeye YAPISIK, havada parcacik yok): hafif koyulasma + parlak ince su filmi + yuzeyde duran su boncuklari.
// Belirirken buyur, kurumaya yakin (dryTime) kuculup kaybolur.
public class WetMark : MonoBehaviour
{
    public float until;
    public float dryTime = 4f;
    public float growTime = 0.6f;
    public int topBeads = 9;
    public int sideBeadsPerFace = 3;

    Renderer rend;
    Color orig, dark;
    bool tinted;
    float start;
    GameObject fx, film;
    Renderer filmR;
    Color filmCol;
    readonly List<Transform> beads = new List<Transform>();
    readonly List<Vector3> beadScale = new List<Vector3>();

    public static void Apply(GameObject bale, float seconds)
    {
        var w = bale.GetComponent<WetMark>();
        if (w == null) w = bale.AddComponent<WetMark>();
        w.until = Time.time + seconds;
        w.Tint();
    }

    void Tint()
    {
        if (tinted) return;
        rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;
        orig = rend.sharedMaterial.GetColor("_BaseColor");
        dark = orig * 0.75f;
        dark.a = orig.a;
        tinted = true;
        start = Time.time;
        BuildFx();
    }

    void BuildFx()
    {
        var mf = rend.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        var tr = rend.transform;
        var matFilm = Resources.Load<Material>("SuFilmi");
        var matBead = Resources.Load<Material>("SuDamla");

        // Su filmi: ayni mesh, hafif buyuk (balya ile ayni transform zincirinde -> tam oturur)
        film = new GameObject("SuFilmi");
        film.transform.SetParent(tr, false);
        film.transform.localScale = Vector3.one * 1.012f;
        film.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        filmR = film.AddComponent<MeshRenderer>();
        if (matFilm != null) filmR.sharedMaterial = matFilm;
        filmR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        filmCol = matFilm != null ? matFilm.GetColor("_BaseColor") : new Color(1, 1, 1, 0.2f);

        // Boncuklar: birim olcekli kapta (skew olmasin), balyayi her kare takip eder
        fx = new GameObject("IslakBoncuk");
        fx.transform.SetPositionAndRotation(tr.position, tr.rotation);
        var mb = mf.sharedMesh.bounds;
        Vector3 c = mb.center, e = mb.extents;

        for (int i = 0; i < topBeads; i++)
            AddBead(tr, matBead, new Vector3(c.x + Random.Range(-e.x, e.x) * 0.9f, c.y + e.y, c.z + Random.Range(-e.z, e.z) * 0.9f), Vector3.up);
        for (int s = -1; s <= 1; s += 2)
            for (int i = 0; i < sideBeadsPerFace; i++)
            {
                AddBead(tr, matBead, new Vector3(c.x + s * e.x, c.y + Random.Range(-e.y, e.y) * 0.8f, c.z + Random.Range(-e.z, e.z) * 0.9f), new Vector3(s, 0, 0));
                AddBead(tr, matBead, new Vector3(c.x + Random.Range(-e.x, e.x) * 0.9f, c.y + Random.Range(-e.y, e.y) * 0.8f, c.z + s * e.z), new Vector3(0, 0, s));
            }
    }

    void AddBead(Transform tr, Material mat, Vector3 localPos, Vector3 localNormal)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (mat != null) g.GetComponent<Renderer>().sharedMaterial = mat;
        Vector3 n = tr.TransformDirection(localNormal).normalized;
        g.transform.SetParent(fx.transform, true);
        g.transform.position = tr.TransformPoint(localPos) + n * 0.002f;
        g.transform.rotation = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        float sz = Random.Range(0.035f, 0.09f);
        Vector3 sc = new Vector3(sz * Random.Range(0.9f, 1.4f), sz * 0.4f, sz);
        g.transform.localScale = Vector3.zero;
        beads.Add(g.transform);
        beadScale.Add(sc);
    }

    void LateUpdate()
    {
        if (fx != null && rend != null) fx.transform.SetPositionAndRotation(rend.transform.position, rend.transform.rotation);
    }

    void Update()
    {
        if (rend == null) { Destroy(this); return; }
        float left = until - Time.time;
        if (left <= 0f) { rend.material.SetColor("_BaseColor", orig); Destroy(this); return; }

        float k = Mathf.Min(Mathf.Clamp01(left / dryTime), Mathf.Clamp01((Time.time - start) / growTime));
        rend.material.SetColor("_BaseColor", Color.Lerp(orig, dark, k));
        if (filmR != null)
        {
            Color fc = filmCol; fc.a = filmCol.a * k;
            filmR.material.SetColor("_BaseColor", fc);
        }
        for (int i = 0; i < beads.Count; i++)
            if (beads[i] != null) beads[i].localScale = beadScale[i] * k;
    }

    void OnDestroy()
    {
        if (fx != null) Destroy(fx);
        if (film != null) Destroy(film);
    }
}
