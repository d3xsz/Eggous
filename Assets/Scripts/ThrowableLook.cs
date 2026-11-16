#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// FIRLATILABILIR NESNE GORUNUMLERI (Editor): misir kocani, elma, kabak (prosedural mesh + doku), tas (paketteki Rock mesh'i).
// Nesne_* objelerinin mesh/materyali degisir; boyut ve collider ayni birim uzayinda (kapsul/kure ilkel sekilleriyle uyumlu).
// Kullanim: ThrowableLook.Apply();
public static class ThrowableLook
{
    const string Dir = "Assets/Textures/Nesne/";
    const string Pack = "Assets/TooMooseGames/Nature Biomes Pack - Low Poly/Prefabs/";

    static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
    static float TileNoise(float u, float v, int W)
    {
        float s = u * W, t = v * W;
        float a = Mathf.PerlinNoise(s, t), b = Mathf.PerlinNoise(s - W, t), c = Mathf.PerlinNoise(s, t - W), d = Mathf.PerlinNoise(s - W, t - W);
        return (a * (W - s) * (W - t) + b * s * (W - t) + c * (W - s) * t + d * s * t) / (W * W);
    }

    static Texture2D Tex(string name, int w, int h, System.Func<float, float, Color> f)
    {
        Directory.CreateDirectory(Dir);
        string path = Dir + name + ".png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f((float)x / w, (float)y / h);
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material Mat(string name, Texture2D tex, Color c, float sm, bool doubleSided)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", sm);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        if (doubleSided) m.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    class MB
    {
        public List<Vector3> v = new List<Vector3>(); public List<Vector3> n = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>();
        public List<int>[] t;
        public MB(int subs) { t = new List<int>[subs]; for (int i = 0; i < subs; i++) t[i] = new List<int>(); }
        public UnityEngine.Mesh Build(string name)
        {
            var m = new UnityEngine.Mesh { name = name };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.subMeshCount = t.Length;
            for (int i = 0; i < t.Length; i++) m.SetTriangles(t[i], i);
            m.RecalculateBounds();
            return m;
        }
    }

    // parametrik yuzey (u,v in 0..1), normal = sonlu fark, 'refP' ic referans noktasi (normal dis yone baksin)
    static void Grid(MB b, int sub, int nu, int nv, System.Func<float, float, Vector3> P, System.Func<float, float, Vector2> UV, System.Func<float, float, Vector3> Ref)
    {
        int baseIdx = b.v.Count; float e = 0.004f;
        var raw = new List<Vector3>(); bool flip = false;
        for (int i = 0; i <= nu; i++)
            for (int j = 0; j <= nv; j++)
            {
                float u = (float)i / nu, v = (float)j / nv;
                Vector3 p = P(u, v);
                float vv = Mathf.Clamp(v, 0.03f, 0.97f);
                Vector3 nn = Vector3.Cross(P(u + e, vv) - P(u - e, vv), P(u, vv + e) - P(u, vv - e));
                if (nn.sqrMagnitude < 1e-14f) nn = p - Ref(u, v);
                nn.Normalize();
                if (float.IsNaN(nn.x) || nn.sqrMagnitude < 0.5f) { Vector3 rf = p - Ref(u, v); nn = rf.sqrMagnitude > 1e-8f ? rf.normalized : Vector3.up; }
                if (i == nu / 2 && j == nv / 2) flip = Vector3.Dot(nn, p - Ref(u, v)) < 0f;
                b.v.Add(p); b.uv.Add(UV(u, v)); raw.Add(nn);
            }
        foreach (var r in raw) b.n.Add(flip ? -r : r);
        for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                int a = baseIdx + i * (nv + 1) + j, c = a + nv + 1;
                if (!flip) { b.t[sub].Add(a); b.t[sub].Add(c); b.t[sub].Add(a + 1); b.t[sub].Add(a + 1); b.t[sub].Add(c); b.t[sub].Add(c + 1); }
                else { b.t[sub].Add(a); b.t[sub].Add(a + 1); b.t[sub].Add(c); b.t[sub].Add(a + 1); b.t[sub].Add(c + 1); b.t[sub].Add(c); }
            }
    }

    static UnityEngine.Mesh SaveMesh(UnityEngine.Mesh m, string path)
    {
        var ex = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
        if (ex != null) { EditorUtility.CopySerialized(m, ex); EditorUtility.SetDirty(ex); return ex; }
        AssetDatabase.CreateAsset(m, path); return m;
    }

    // ---- MISIR: kapsul uzayi (yaricap 0.5, y -1..1) ----
    static UnityEngine.Mesh CornMesh()
    {
        var b = new MB(2);
        Grid(b, 0, 32, 40, (u, v) =>
        {
            float th = u * Mathf.PI * 2f, f;
            if (v < 0.55f) f = Mathf.Lerp(0.65f, 1f, Smooth(0f, 0.3f, v));
            else { float t = (v - 0.55f) / 0.45f; f = Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)); }
            f *= Smooth(0f, 0.04f, v);
            float r = 0.46f * f;
            return new Vector3(Mathf.Cos(th) * r, -1f + 2f * v, Mathf.Sin(th) * r);
        }, (u, v) => new Vector2(u * 2f, v * 1.5f), (u, v) => new Vector3(0f, -1f + 2f * v, 0f));
        // 4 yaprak (kabuk): tabandan yukari, ucta disari acilir
        for (int k = 0; k < 4; k++)
        {
            float phi = k * Mathf.PI * 0.5f + 0.25f;
            Grid(b, 1, 6, 12, (u, v) =>
            {
                float y = -0.95f + 1.15f * v;
                float R = 0.34f + 0.12f * v + 0.38f * v * v * v;
                float hw = 0.55f * (1f - v * v) + 0.04f;
                float a = phi + (u - 0.5f) * 2f * hw;
                return new Vector3(Mathf.Cos(a) * R, y, Mathf.Sin(a) * R);
            }, (u, v) => new Vector2(u, v), (u, v) => new Vector3(0f, -0.95f + 1.15f * v, 0f));
        }
        return b.Build("Nesne_Misir");
    }

    // ---- ELMA: kure uzayi (yaricap 0.5) ----
    static UnityEngine.Mesh AppleMesh()
    {
        var b = new MB(3);
        Grid(b, 0, 36, 28, (u, v) =>
        {
            float th = u * Mathf.PI * 2f, ph = v * Mathf.PI;
            float r = 0.5f * Mathf.Sin(ph) * (1f + 0.08f * Mathf.Cos(ph)) * (1f + 0.03f * Mathf.Cos(5f * th) * (1f - Mathf.Cos(ph)) * 0.5f);
            float y = 0.45f * Mathf.Cos(ph) - 0.14f * Mathf.Exp(-Mathf.Pow(ph / 0.38f, 2f)) + 0.05f * Mathf.Exp(-Mathf.Pow((Mathf.PI - ph) / 0.35f, 2f));
            return new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r);
        }, (u, v) => new Vector2(u, v), (u, v) => Vector3.zero);
        Grid(b, 1, 10, 6, (u, v) =>
        {
            float th = u * Mathf.PI * 2f, r = 0.035f * (1f - 0.3f * v);
            return new Vector3(Mathf.Cos(th) * r + 0.04f * v * v, 0.28f + 0.26f * v, Mathf.Sin(th) * r);
        }, (u, v) => new Vector2(u, v), (u, v) => new Vector3(0.04f * v * v, 0.28f + 0.26f * v, 0f));
        Grid(b, 2, 6, 10, (u, v) =>
        {
            float c = (u - 0.5f) * 2f, w = 0.075f * Mathf.Pow(Mathf.Sin(Mathf.PI * v), 0.8f);
            Vector3 d = new Vector3(0.9f, 0.35f, 0f).normalized;
            Vector3 p = new Vector3(0.02f, 0.46f, 0f) + d * (0.3f * v) + new Vector3(0f, -0.08f * v * v, c * w);
            return p;
        }, (u, v) => new Vector2(u, v), (u, v) => new Vector3(0.02f, 0.40f, 0f));
        return b.Build("Nesne_Elma");
    }

    // ---- KABAK: kure uzayi, 10 dilimli, ustte sap ----
    static UnityEngine.Mesh PumpkinMesh()
    {
        var b = new MB(2);
        Grid(b, 0, 60, 28, (u, v) =>
        {
            float th = u * Mathf.PI * 2f, ph = v * Mathf.PI;
            float rib = 1f + 0.09f * Mathf.Cos(10f * th);
            float r = 0.5f * Mathf.Sin(ph) * rib;
            float y = 0.43f * Mathf.Cos(ph) - 0.16f * Mathf.Exp(-Mathf.Pow(ph / 0.30f, 2f)) + 0.07f * Mathf.Exp(-Mathf.Pow((Mathf.PI - ph) / 0.30f, 2f));
            return new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r);
        }, (u, v) => new Vector2(u, v), (u, v) => Vector3.zero);
        Grid(b, 1, 16, 8, (u, v) =>
        {
            float th = u * Mathf.PI * 2f;
            float r = (0.07f + 0.05f * Smooth(0.75f, 1f, v)) * (1f + 0.12f * Mathf.Cos(5f * th));
            return new Vector3(Mathf.Cos(th) * r + 0.05f * v * v, 0.26f + 0.24f * v, Mathf.Sin(th) * r);
        }, (u, v) => new Vector2(u, v), (u, v) => new Vector3(0.05f * v * v, 0.26f + 0.24f * v, 0f));
        return b.Build("Nesne_Kabak");
    }

    public static string Apply()
    {
        if (EditorApplication.isPlaying) return "PLAY MODE ACIK, atlandi";
        Directory.CreateDirectory("Assets/Meshes");

        var kernel = Tex("MisirTane", 128, 128, (u, v) =>
        {
            float cu = u * 16f, cv = v * 16f; int ix = (int)cu, iy = (int)cv;
            float lx = (cu - ix) * 2f - 1f, ly = (cv - iy) * 2f - 1f;
            float d = Mathf.Sqrt(lx * lx + ly * ly * 0.8f);
            float n = Mathf.PerlinNoise(ix * 0.7f + 3f, iy * 0.7f + 5f);
            Color k = Color.Lerp(new Color(0.93f, 0.72f, 0.10f), new Color(1f, 0.88f, 0.25f), n);
            k *= 1f + 0.18f * (1f - d);
            Color gap = new Color(0.55f, 0.40f, 0.04f);
            return Color.Lerp(k, gap, Smooth(0.78f, 1.0f, d)) ;
        });
        var appleT = Tex("ElmaDoku", 64, 64, (u, v) =>
        {
            float streak = TileNoise(u * 1.0f, v * 0.2f, 6);
            Color red = Color.Lerp(new Color(0.70f, 0.05f, 0.06f), new Color(0.90f, 0.16f, 0.10f), streak);
            Color yellow = new Color(0.85f, 0.72f, 0.20f);
            Color c = Color.Lerp(yellow, red, Smooth(0.08f, 0.35f, v));
            float dot = Mathf.PerlinNoise(u * 40f, v * 40f) > 0.80f ? 1.12f : 1f;
            return new Color(c.r * dot, c.g * dot, c.b * dot, 1f);
        });
        var pumpT = Tex("KabakDoku", 128, 128, (u, v) =>
        {
            float rib = Mathf.Cos(u * Mathf.PI * 2f * 10f);
            float n = TileNoise(u, v, 8) * 0.12f;
            Color c = Color.Lerp(new Color(0.74f, 0.30f, 0.04f), new Color(0.98f, 0.55f, 0.12f), Mathf.Clamp01(0.5f + 0.5f * rib * 0.8f - n));
            return c;
        });

        var mCorn = Mat("NesneMisir", kernel, Color.white, 0.35f, false);
        var mHusk = Mat("NesneMisirKabuk", null, new Color(0.40f, 0.65f, 0.20f), 0.15f, true);
        var mApple = Mat("NesneElma", appleT, Color.white, 0.55f, false);
        var mStem = Mat("NesneSap", null, new Color(0.38f, 0.25f, 0.12f), 0.1f, false);
        var mLeaf = Mat("NesneYaprak", null, new Color(0.22f, 0.58f, 0.18f), 0.2f, true);
        var mPump = Mat("NesneKabak", pumpT, Color.white, 0.25f, false);
        var mPStem = Mat("NesneKabakSap", null, new Color(0.40f, 0.48f, 0.18f), 0.1f, false);

        var corn = SaveMesh(CornMesh(), "Assets/Meshes/Nesne_Misir.asset");
        var apple = SaveMesh(AppleMesh(), "Assets/Meshes/Nesne_Elma.asset");
        var pump = SaveMesh(PumpkinMesh(), "Assets/Meshes/Nesne_Kabak.asset");

        string[] rockNames = { "Rock 1", "Rock 2", "Rock 3", "Rock 4", "Rock 6" };
        int rockIdx = 0, cnt = 0;
        foreach (var t in Object.FindObjectsByType<Throwable>(FindObjectsSortMode.None))
        {
            var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
            if (mf == null || mr == null) continue;
            switch (t.kind)
            {
                case ThrowKind.Misir: mf.sharedMesh = corn; mr.sharedMaterials = new[] { mCorn, mHusk }; break;
                case ThrowKind.Elma: mf.sharedMesh = apple; mr.sharedMaterials = new[] { mApple, mStem, mLeaf }; break;
                case ThrowKind.Kabak: mf.sharedMesh = pump; mr.sharedMaterials = new[] { mPump, mPStem }; break;
                default:
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + rockNames[rockIdx++ % rockNames.Length] + ".prefab");
                    if (prefab == null) break;
                    UnityEngine.Mesh best = null; Material[] bm = null;
                    foreach (var f in prefab.GetComponentsInChildren<MeshFilter>())
                        if (f.sharedMesh != null && (best == null || f.sharedMesh.vertexCount > best.vertexCount))
                        { best = f.sharedMesh; var r = f.GetComponent<MeshRenderer>(); bm = r != null ? r.sharedMaterials : null; }
                    if (best == null) break;
                    mf.sharedMesh = best; if (bm != null) mr.sharedMaterials = bm;
                    float s = 0.30f / Mathf.Max(best.bounds.size.x, best.bounds.size.z);
                    t.transform.localScale = Vector3.one * s;
                    var col = t.GetComponent<SphereCollider>();
                    if (col != null) { col.center = best.bounds.center; col.radius = Mathf.Max(best.bounds.extents.x, best.bounds.extents.z) * 0.85f; }
                    break;
                }
            }
            EditorUtility.SetDirty(t.gameObject); cnt++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return cnt + " nesnenin gorunumu guncellendi (misir/elma/kabak prosedural, tas = paket kayasi)";
    }
}
#endif
