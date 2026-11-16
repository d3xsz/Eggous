#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// GOL GORSELI KURUCU (Editor). LevelBuilder.Build() sonunda otomatik cagrilir; tek basina da: LakeBuilder.Build(4.8f).
// GolAlani altina 'GolGorsel' kurar: kum zemin, iki saydam dalgali su katmani, kiyi kopugu + LakeWater/WaterSurface.
public static class LakeBuilder
{
    const string TexDir = "Assets/Textures/Lake/";

    static float TileNoise(float u, float v, int W)
    {
        float s = u * W, t = v * W;
        float a = Mathf.PerlinNoise(s, t), b = Mathf.PerlinNoise(s - W, t), c = Mathf.PerlinNoise(s, t - W), d = Mathf.PerlinNoise(s - W, t - W);
        return (a * (W - s) * (W - t) + b * s * (W - t) + c * (W - s) * t + d * s * t) / (W * W);
    }

    static Texture2D SaveTex(string name, int w, int h, System.Func<float, float, Color> f, bool normal, bool alpha)
    {
        Directory.CreateDirectory(TexDir);
        string path = TexDir + name + ".png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f((float)x / w, (float)y / h);
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti != null)
        {
            if (normal) ti.textureType = TextureImporterType.NormalMap;
            ti.alphaIsTransparency = alpha;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material Mat(string path, string shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    static void Transparent(Material m)
    {
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
    }

    static UnityEngine.Mesh Save(UnityEngine.Mesh m, string path)
    {
        var ex = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
        if (ex != null) { EditorUtility.CopySerialized(m, ex); EditorUtility.SetDirty(ex); return ex; }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static UnityEngine.Mesh Disc(float r, string name)
    {
        const int rings = 8, seg = 56;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var t = new List<int>();
        v.Add(Vector3.zero); uv.Add(Vector2.zero); n.Add(Vector3.up);
        for (int i = 1; i <= rings; i++)
            for (int j = 0; j < seg; j++)
            {
                float th = j * Mathf.PI * 2f / seg, rr = r * i / rings;
                v.Add(new Vector3(Mathf.Cos(th) * rr, 0f, Mathf.Sin(th) * rr)); uv.Add(new Vector2(Mathf.Cos(th) * rr, Mathf.Sin(th) * rr)); n.Add(Vector3.up);
            }
        for (int j = 0; j < seg; j++) { t.Add(0); t.Add(1 + (j + 1) % seg); t.Add(1 + j); }
        for (int i = 1; i < rings; i++)
            for (int j = 0; j < seg; j++)
            {
                int a = 1 + (i - 1) * seg + j, b = 1 + (i - 1) * seg + (j + 1) % seg, c = 1 + i * seg + j, d = 1 + i * seg + (j + 1) % seg;
                t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c);
            }
        if (Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]).y < 0f)
            for (int k = 0; k < t.Count; k += 3) { int tmp = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = tmp; }
        var m = new UnityEngine.Mesh { name = name };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds();
        return m;
    }

    static UnityEngine.Mesh Foam(float r0, float r1, string name)
    {
        const int seg = 72;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var t = new List<int>();
        for (int j = 0; j <= seg; j++)
        {
            float th = j * Mathf.PI * 2f / seg;
            float w = (Mathf.PerlinNoise(Mathf.Cos(th) * 2.2f + 5f, Mathf.Sin(th) * 2.2f + 5f) - 0.5f) * 0.5f;
            float a = r0 + w, b = r1 + w * 0.6f;
            v.Add(new Vector3(Mathf.Cos(th) * a, 0f, Mathf.Sin(th) * a)); uv.Add(new Vector2((float)j / seg * 9f, 0f)); n.Add(Vector3.up);
            v.Add(new Vector3(Mathf.Cos(th) * b, 0f, Mathf.Sin(th) * b)); uv.Add(new Vector2((float)j / seg * 9f, 1f)); n.Add(Vector3.up);
        }
        for (int j = 0; j < seg; j++) { int i = j * 2; t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i + 1); t.Add(i + 3); t.Add(i + 2); }
        if (Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]).y < 0f)
            for (int k = 0; k < t.Count; k += 3) { int tmp = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = tmp; }
        var m = new UnityEngine.Mesh { name = name };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds();
        return m;
    }

    public static string Build(float lakeR)
    {
        if (EditorApplication.isPlaying) return "Gol: PLAY MODE ACIK, atlandi\n";
        var gol = GameObject.Find("GolAlani");
        if (gol == null) return "Gol: GolAlani yok\n";
        float edgeR = 1.07f * lakeR;

        // ---- dokular ----
        var normalTex = SaveTex("SuNormal", 256, 256, (u, v) =>
        {
            System.Func<float, float, float> H = (a, b) => 0.5f * TileNoise(a, b, 4) + 0.3f * TileNoise(a, b, 12) + 0.2f * TileNoise(a, b, 32) + 0.05f * Mathf.Sin((a * 6f + b * 2f) * Mathf.PI * 2f);
            float e = 1f / 256f;
            float dx = H(u + e, v) - H(u - e, v), dy = H(u, v + e) - H(u, v - e);
            Vector3 nn = new Vector3(-dx * 14f, -dy * 14f, 1f).normalized;
            return new Color(nn.x * 0.5f + 0.5f, nn.y * 0.5f + 0.5f, nn.z * 0.5f + 0.5f, 1f);
        }, true, false);
        var sandTex = SaveTex("GolKum", 128, 128, (u, v) =>
        {
            float n = 0.6f * TileNoise(u, v, 8) + 0.4f * TileNoise(u, v, 32);
            return Color.Lerp(new Color(0.62f, 0.56f, 0.38f), new Color(0.88f, 0.80f, 0.55f), n);
        }, false, false);
        var foamTex = SaveTex("GolKopuk", 128, 64, (u, v) =>
        {
            float n = TileNoise(u, v, 8);
            float a = Mathf.Pow(Mathf.Clamp01(v), 1.4f) * Mathf.Clamp01(0.5f + 0.9f * n);
            if (TileNoise(u * 0.99f, v, 16) < 0.36f) a *= 0.4f;     // kopuk delikleri
            if (v > 0.86f) a = Mathf.Max(a, 0.55f * Mathf.Clamp01(n + 0.25f));
            return new Color(0.96f, 1f, 1f, Mathf.Clamp01(a));
        }, false, true);

        // ---- materyaller ----
        var mBed = Mat("Assets/Materials/GolTaban.mat", "Universal Render Pipeline/Lit");
        mBed.SetTexture("_BaseMap", sandTex); mBed.SetColor("_BaseColor", new Color(0.85f, 0.95f, 1f)); mBed.SetFloat("_Smoothness", 0.1f);
        mBed.SetTextureScale("_BaseMap", new Vector2(0.4f, 0.4f));
        var mA = Mat("Assets/Materials/GolSuA.mat", "Universal Render Pipeline/Lit");
        var mB = Mat("Assets/Materials/GolSuB.mat", "Universal Render Pipeline/Lit");
        foreach (var m in new[] { mA, mB })
        {
            Transparent(m);
            m.SetTexture("_BumpMap", normalTex); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.9f);
            m.SetFloat("_Smoothness", 0.93f); m.SetFloat("_Metallic", 0f);
        }
        mA.SetColor("_BaseColor", new Color(0.12f, 0.50f, 0.68f, 0.62f)); mA.SetTextureScale("_BumpMap", new Vector2(0.33f, 0.33f));
        mB.SetColor("_BaseColor", new Color(0.30f, 0.75f, 0.88f, 0.30f)); mB.SetTextureScale("_BumpMap", new Vector2(0.6f, 0.6f));
        var mF = Mat("Assets/Materials/GolKopuk.mat", "Universal Render Pipeline/Unlit");
        Transparent(mF); mF.SetTexture("_BaseMap", foamTex); mF.SetColor("_BaseColor", Color.white);
        foreach (var m in new[] { mBed, mA, mB, mF }) EditorUtility.SetDirty(m);

        // ---- mesh'ler ----
        var disc = Save(Disc(edgeR + 0.3f, "GolSuDisk"), "Assets/Meshes/GolSuDisk.asset");
        var bedMesh = Save(Disc(lakeR * 0.62f, "GolTabanDisk"), "Assets/Meshes/GolTabanDisk.asset");
        var foamMesh = Save(Foam(edgeR - 0.95f, edgeR + 0.12f, "GolKopukHalka"), "Assets/Meshes/GolKopukHalka.asset");

        // ---- sahne ----
        var old = gol.transform.Find("GolGorsel"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var su = gol.transform.Find("SuYuzeyi"); if (su != null) su.gameObject.SetActive(false);
        var root = new GameObject("GolGorsel").transform;
        root.SetParent(gol.transform, false);
        root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity; root.localScale = Vector3.one;

        System.Func<string, UnityEngine.Mesh, Material, float, Renderer> Layer = (nm, mesh, mat, y) =>
        {
            var g = new GameObject(nm); g.transform.SetParent(root, false); g.transform.localPosition = new Vector3(0f, y, 0f);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        };
        Layer("GolTaban", bedMesh, mBed, -0.742f);
        var rA = Layer("GolSuA", disc, mA, -0.35f);
        var rB = Layer("GolSuB", disc, mB, -0.34f);
        Layer("GolKopuk", foamMesh, mF, -0.325f);

        var ws = root.gameObject.AddComponent<WaterSurface>(); ws.layerA = rA; ws.layerB = rB;
        var lw = gol.GetComponent<LakeWater>(); if (lw == null) lw = gol.AddComponent<LakeWater>();
        lw.edgeRadius = edgeR; lw.waterY = -0.35f;
        lw.splashClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/water_splash.wav");
        EditorUtility.SetDirty(gol);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return "Gol: kum zemin + 2 dalgali su katmani + kiyi kopugu + LakeWater (kenar yaricapi " + edgeR.ToString("F2") + ")\n";
    }
}
#endif
