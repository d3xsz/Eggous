#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// LEVEL KURUCU (sadece Editor). Duzeni degistirmek icin Layout() ve Build() icindeki sayilari duzenle, sonra LevelBuilder.Build() calistir.
// Her calistirmada 'Level' ve 'Cevre' objeleri bastan kurulur. Eski level (Harita/Arena, Harita/Dag, Koy, Dekor, CevreDekor) KAPALI kalir.
public static class LevelBuilder
{
    const string Pack = "Assets/TooMooseGames/Nature Biomes Pack - Low Poly/Prefabs/";
    const string NestPack = "Assets/Low Poly Bird Nests/";

    public class Isle
    {
        public string name; public float top, rx, rz, depth; public int seed;
        public float segAngle, segGap;           // onceki adadan gelis acisi (derece, +x'ten +z'ye) ve kenar-kenar bosluk
        public Vector2 pos;                      // dunya xz merkezi
        public Vector2 lakeC; public float lakeR, lakeDepth; // sadece baslangic adasi (yerel)
        public Vector2[] rim;
        public float R { get { return (rx + rz) * 0.5f; } }
    }
    class Zone { public Vector2 c; public float r; public Zone(Vector2 cc, float rr) { c = cc; r = rr; } }

    static Vector2 Dir(float deg) { return new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)); }
    static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
    static float Rim(float th, int seed)
    {
        float cx = Mathf.Cos(th), sz = Mathf.Sin(th);
        float n1 = Mathf.PerlinNoise(cx * 1.6f + seed * 3.1f + 10f, sz * 1.6f + seed * 1.7f + 10f) - 0.5f;
        float n2 = Mathf.PerlinNoise(cx * 4.5f + seed * 5.3f + 10f, sz * 4.5f + seed * 2.9f + 10f) - 0.5f;
        return 1f + 0.45f * n1 + 0.15f * n2;
    }

    // ================= DUZEN (sagli sollu: KD -> GD -> KD) =================
    static List<Isle> Layout()
    {
        var l = new List<Isle>
        {
            new Isle { name = "Baslangic", top = 0f,  rx = 17.5f, rz = 17.5f, depth = 26f, seed = 1 },
            new Isle { name = "Ada1",      top = 10f, rx = 9f,    rz = 9f,    depth = 15f, seed = 2, segAngle = 55f,  segGap = 3f },
            new Isle { name = "Ada2",      top = 22f, rx = 10f,   rz = 9f,    depth = 16f, seed = 3, segAngle = -28f, segGap = 10f },
            new Isle { name = "Kulucka",   top = 34f, rx = 9f,    rz = 9f,    depth = 15f, seed = 4, segAngle = 52f,  segGap = 13.7f },
        };
        for (int i = 1; i < l.Count; i++)
            l[i].pos = l[i - 1].pos + Dir(l[i].segAngle) * (l[i - 1].R + l[i].R + l[i].segGap);
        l[0].lakeR = 4.8f; l[0].lakeDepth = 0.75f; l[0].lakeC = -Dir(l[1].segAngle) * 9.5f;
        return l;
    }

    // ================= ADA MESH'I =================
    static float LakeY(Isle s, float x, float z)
    {
        if (s.lakeR <= 0f) return 0f;
        float d = Vector2.Distance(new Vector2(x, z), s.lakeC);
        return -s.lakeDepth * (1f - Smooth(s.lakeR * 0.6f, s.lakeR * 1.5f, d));
    }

    static UnityEngine.Mesh BuildIsland(Isle s)
    {
        const int Na = 64, Nr = 14;
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var topTris = new List<int>();
        verts.Add(new Vector3(0f, LakeY(s, 0f, 0f), 0f)); uvs.Add(Vector2.zero);
        for (int i = 1; i <= Nr; i++)
        {
            float f = (float)i / Nr;
            for (int j = 0; j < Na; j++)
            {
                float th = j * Mathf.PI * 2f / Na; float rim = Rim(th, s.seed);
                float x = s.rx * f * rim * Mathf.Cos(th), z = s.rz * f * rim * Mathf.Sin(th);
                verts.Add(new Vector3(x, LakeY(s, x, z), z)); uvs.Add(new Vector2(x / 7f, z / 7f));
            }
        }
        for (int j = 0; j < Na; j++) { topTris.Add(0); topTris.Add(1 + j); topTris.Add(1 + (j + 1) % Na); }
        for (int i = 1; i < Nr; i++)
            for (int j = 0; j < Na; j++)
            {
                int a = 1 + (i - 1) * Na + j, b = 1 + (i - 1) * Na + (j + 1) % Na, c = 1 + i * Na + j, d = 1 + i * Na + (j + 1) % Na;
                topTris.Add(a); topTris.Add(c); topTris.Add(b); topTris.Add(b); topTris.Add(c); topTris.Add(d);
            }
        {
            Vector3 p0 = verts[topTris[0]], p1 = verts[topTris[1]], p2 = verts[topTris[2]];
            if (Vector3.Cross(p1 - p0, p2 - p0).y < 0f)
                for (int k = 0; k < topTris.Count; k += 3) { int tmp = topTris[k + 1]; topTris[k + 1] = topTris[k + 2]; topTris[k + 2] = tmp; }
        }
        float[] yF = { 0f, -0.12f, -0.30f, -0.55f, -0.80f, -1f };
        float[] sF = { 1f, 0.985f, 0.86f, 0.62f, 0.34f, 0f };
        int K = yF.Length;
        System.Func<int, int, Vector3> P = (k, j) =>
        {
            j = ((j % Na) + Na) % Na;
            float th = j * Mathf.PI * 2f / Na; float rim = Rim(th, s.seed);
            float bx = s.rx * Mathf.Cos(th) * rim, bz = s.rz * Mathf.Sin(th) * rim;
            float rr = 1f, yj = 0f;
            if (k > 0 && k < K - 1)
            {
                rr = 1f + (Mathf.PerlinNoise(j * 1.37f + s.seed * 11.3f, k * 2.71f + s.seed * 5.1f) - 0.5f) * 0.2f;
                yj = (Mathf.PerlinNoise(j * 0.93f + s.seed * 7.7f, k * 3.17f + s.seed * 2.3f) - 0.5f) * 0.10f * s.depth;
            }
            return new Vector3(bx * sF[k] * rr, yF[k] * s.depth + yj, bz * sF[k] * rr);
        };
        var sideTris = new List<int>();
        System.Action<Vector3, Vector3, Vector3, float> AddTri = (v0, v1, v2, uRef) =>
        {
            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0); Vector3 cen = (v0 + v1 + v2) / 3f;
            if (Vector3.Dot(n, new Vector3(cen.x, 0f, cen.z)) < 0f) { var t = v1; v1 = v2; v2 = t; }
            int bi = verts.Count; verts.Add(v0); verts.Add(v1); verts.Add(v2);
            foreach (var v in new[] { v0, v1, v2 }) uvs.Add(new Vector2(uRef, -v.y / 5f));
            sideTris.Add(bi); sideTris.Add(bi + 1); sideTris.Add(bi + 2);
        };
        for (int k = 0; k < K - 1; k++)
            for (int j = 0; j < Na; j++)
            {
                Vector3 A = P(k, j), B = P(k, j + 1), C = P(k + 1, j), D = P(k + 1, j + 1);
                float u = (float)j / Na * 4f;
                AddTri(A, C, B, u);
                if (k + 1 < K - 1) AddTri(B, C, D, u);
            }
        var m = new UnityEngine.Mesh();
        m.name = "Isla_" + s.name;
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(verts); m.SetUVs(0, uvs); m.subMeshCount = 2;
        m.SetTriangles(topTris, 0); m.SetTriangles(sideTris, 1);
        m.RecalculateNormals(); m.RecalculateBounds();
        s.rim = new Vector2[Na];
        for (int j = 0; j < Na; j++)
        {
            float th = j * Mathf.PI * 2f / Na; float rim = Rim(th, s.seed);
            s.rim[j] = new Vector2(s.pos.x + s.rx * Mathf.Cos(th) * rim, s.pos.y + s.rz * Mathf.Sin(th) * rim);
        }
        return m;
    }

    static float MinGap(Isle a, Isle b)
    {
        float best = 1e9f;
        foreach (var p in a.rim) foreach (var q in b.rim) { float d = Vector2.Distance(p, q); if (d < best) best = d; }
        return best;
    }

    // ================= DEKOR YARDIMCILARI =================
    static Bounds BB(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(g.transform.position, Vector3.zero); bool first = true;
        foreach (var r in rs) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        return b;
    }

    static GameObject Place(string path, Vector3 pos, float yaw, float target, bool useWidth, Transform parent, float sink)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return null;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        inst.transform.position = pos; inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Bounds b = BB(inst);
        float basis = useWidth ? Mathf.Max(b.size.x, b.size.z) : b.size.y;
        if (basis < 0.001f) return inst;
        inst.transform.localScale = Vector3.one * (target / basis);
        b = BB(inst);
        inst.transform.position += Vector3.up * (pos.y - sink - b.min.y);
        return inst;
    }

    static void StripColliders(GameObject g) { if (g == null) return; foreach (var c in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c); }

    static Material MatLit(string path, Color c, float sm, bool emit)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", sm);
        if (emit) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(1f, 0.78f, 0.35f) * 0.35f); }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Decorate(Isle s, int ii, List<Isle> all, List<Zone> zs, int[] cnt, Transform level)
    {
        var rnd = new System.Random(100 + ii);
        var parent = new GameObject("Dekor_" + s.name).transform; parent.SetParent(level, false);
        var dirs = new List<float>();
        if (ii > 0) dirs.Add(all[ii].segAngle + 180f);
        if (ii < all.Count - 1) dirs.Add(all[ii + 1].segAngle);
        string[] trees = { "Tree 2 G1", "Tree 3 G1", "Tree 4 G1", "Tree 2 G2", "Tree 3 G2", "Tree 4 G2", "Tree 2 O1" };
        string[] rocks = { "Rock 1", "Rock 2", "Rock 3", "Rock 4", "Rock 1 Moss", "Rock 3 Moss" };
        string[] grass = { "Grass 1 G1", "Grass 1 G2" };
        string[] mush = { "Mushroom 1 Red", "Mushroom 1 White", "Mushroom 1 Brown" };
        string[] logs = { "Log 2", "Log 3", "Log 5" };
        var placedTrees = new List<Vector2>();

        System.Func<float, float, Vector2?> Pick = (fmin, fmax) =>
        {
            for (int a = 0; a < 70; a++)
            {
                float th = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float f = fmin + (float)rnd.NextDouble() * (fmax - fmin);
                float rim = Rim(th, s.seed);
                float x = s.pos.x + s.rx * f * rim * Mathf.Cos(th) * 0.97f, z = s.pos.y + s.rz * f * rim * Mathf.Sin(th) * 0.97f;
                float ang = Mathf.Atan2(z - s.pos.y, x - s.pos.x) * Mathf.Rad2Deg;
                bool cone = false;
                if (f > 0.5f) foreach (var d in dirs) if (Mathf.Abs(Mathf.DeltaAngle(ang, d)) < 32f) cone = true;
                if (cone) continue;
                bool bad = false;
                foreach (var zz in zs) if (Vector2.Distance(new Vector2(x, z), zz.c) < zz.r) { bad = true; break; }
                if (bad) continue;
                return new Vector2(x, z);
            }
            return null;
        };

        for (int i = 0; i < cnt[0]; i++)
        {
            var p = Pick(0.62f, 0.90f); if (p == null) continue;
            bool close = false; foreach (var q in placedTrees) if (Vector2.Distance(p.Value, q) < 3f) close = true;
            if (close) continue;
            placedTrees.Add(p.Value);
            var g = Place(Pack + trees[rnd.Next(trees.Length)] + ".prefab", new Vector3(p.Value.x, s.top, p.Value.y), (float)rnd.NextDouble() * 360f, 5.5f + (float)rnd.NextDouble() * 2.5f, false, parent, 0.05f);
            if (g == null) continue;
            var b = BB(g); StripColliders(g);
            var cc = g.AddComponent<CapsuleCollider>();
            cc.center = g.transform.InverseTransformPoint(new Vector3(p.Value.x, b.min.y + 1.5f, p.Value.y));
            cc.radius = 0.45f / g.transform.lossyScale.x; cc.height = 3f / g.transform.lossyScale.y;
        }
        for (int i = 0; i < cnt[1]; i++)
        {
            var p = Pick(0.55f, 0.93f); if (p == null) continue;
            Place(Pack + rocks[rnd.Next(rocks.Length)] + ".prefab", new Vector3(p.Value.x, s.top, p.Value.y), (float)rnd.NextDouble() * 360f, 1.0f + (float)rnd.NextDouble() * 1.3f, false, parent, 0.15f);
        }
        for (int i = 0; i < cnt[2]; i++)
        {
            var p = Pick(0.12f, 0.97f); if (p == null) continue;
            StripColliders(Place(Pack + grass[rnd.Next(grass.Length)] + ".prefab", new Vector3(p.Value.x, s.top, p.Value.y), (float)rnd.NextDouble() * 360f, 0.55f + (float)rnd.NextDouble() * 0.3f, false, parent, 0f));
        }
        for (int i = 0; i < cnt[3]; i++)
        {
            var p = Pick(0.2f, 0.92f); if (p == null) continue;
            StripColliders(Place(Pack + mush[rnd.Next(mush.Length)] + ".prefab", new Vector3(p.Value.x, s.top, p.Value.y), (float)rnd.NextDouble() * 360f, 0.3f + (float)rnd.NextDouble() * 0.2f, false, parent, 0f));
        }
        for (int i = 0; i < cnt[4]; i++)
        {
            var p = Pick(0.6f, 0.9f); if (p == null) continue;
            StripColliders(Place(Pack + logs[rnd.Next(logs.Length)] + ".prefab", new Vector3(p.Value.x, s.top, p.Value.y), (float)rnd.NextDouble() * 360f, 2.2f, true, parent, 0.05f));
        }
        for (int i = 0; i < cnt[5]; i++) // kus yuvalari (yeni asset)
        {
            var p = Pick(0.3f, 0.9f); if (p == null) continue;
            StripColliders(Place(NestPack + "Nest_" + rnd.Next(1, 26).ToString("00") + ".prefab", new Vector3(p.Value.x, s.top, p.Value.y), (float)rnd.NextDouble() * 360f, 0.9f + (float)rnd.NextDouble() * 0.7f, true, parent, 0f));
        }
    }

    // ================= ANA KURULUM =================
    public static string Build()
    {
        if (EditorApplication.isPlaying) return "PLAY MODE ACIK: islem atlandi, Play'i durdurun.";
        var log = new StringBuilder();
        var isles = Layout();
        var grassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/IslaCim.mat");
        var rockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/IslaKaya.mat");
        if (grassMat == null || rockMat == null) return "IslaCim.mat / IslaKaya.mat bulunamadi";

        foreach (var n in new[] { "Harita/Arena", "Harita/Dag", "Koy", "Dekor", "CevreDekor" }) { var g = GameObject.Find(n); if (g != null) g.SetActive(false); }
        foreach (var n in new[] { "Level", "Cevre" }) { var o = GameObject.Find(n); if (o != null) Object.DestroyImmediate(o); }
        var level = new GameObject("Level").transform;

        // ---- adalar ----
        foreach (var s in isles)
        {
            var mesh = BuildIsland(s);
            string mp = "Assets/Meshes/Isla_" + s.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(mp);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); mesh = existing; EditorUtility.SetDirty(mesh); }
            else AssetDatabase.CreateAsset(mesh, mp);
            var go = new GameObject("Isla_" + s.name);
            go.transform.SetParent(level, false);
            go.transform.position = new Vector3(s.pos.x, s.top, s.pos.y);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { grassMat, rockMat };
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        AssetDatabase.SaveAssets();
        log.Append("ADA MERKEZLERI + ARALAR:\n");
        for (int i = 0; i < isles.Count; i++)
        {
            log.Append(isles[i].name).Append(" pos=(").Append(isles[i].pos.x.ToString("F1")).Append(", ").Append(isles[i].pos.y.ToString("F1")).Append(") y=").Append(isles[i].top).Append("\n");
            if (i + 1 < isles.Count) log.Append("  -> bosluk ").Append(MinGap(isles[i], isles[i + 1]).ToString("F1")).Append(" m, yukseklik +").Append((isles[i + 1].top - isles[i].top).ToString("F0")).Append(" m\n");
        }

        var S = isles[0]; var A1 = isles[1]; var A2 = isles[2]; var K = isles[3];
        Vector2 exitS = Dir(A1.segAngle), exitA1 = Dir(A2.segAngle), exitA2 = Dir(K.segAngle), arriveK = Dir(K.segAngle + 180f);

        // ---- yerlesim (yerel kurallar) ----
        Vector2 spawn = new Vector2(3f, -1f);
        Vector2 lakeW = S.pos + S.lakeC;
        Vector2 bucket = lakeW + new Vector2(0.72f, 0.72f) * (S.lakeR * 1.5f + 0.9f);
        Vector2 pileS = new Vector2(4f, -10.5f);
        Vector2 vote = new Vector2(-9f, 10f);
        Vector2 pileA1 = A1.pos - exitA1 * 3.8f + new Vector2(-exitA1.y, exitA1.x) * 1.5f;
        Vector2 pileA2 = A2.pos - exitA2 * 3.8f + new Vector2(-exitA2.y, exitA2.x) * 1.5f;
        Vector2 pileK = K.pos + arriveK * 5.5f;

        System.Action<string, Vector3> Move = (n, p) => { var g = GameObject.Find(n); if (g != null) g.transform.position = p; else log.Append("BULUNAMADI: ").Append(n).Append("\n"); };
        var player = GameObject.Find("Oyuncu"); if (player != null) player.transform.position = new Vector3(spawn.x, 0.1f, spawn.y);
        var gol = GameObject.Find("GolAlani");
        if (gol != null)
        {
            gol.transform.position = new Vector3(lakeW.x, 0f, lakeW.y);
            var bc = gol.GetComponent<BoxCollider>();
            if (bc != null) { bc.size = new Vector3(14f, 3f, 14f); bc.center = new Vector3(0f, 0.5f, 0f); }
            var su = gol.transform.Find("SuYuzeyi");
            if (su != null)
            {
                su.position = new Vector3(lakeW.x, -0.35f, lakeW.y);
                var r = su.GetComponent<Renderer>();
                if (r != null && r.bounds.size.x > 0.01f) { float f = (2f * 1.15f * S.lakeR) / r.bounds.size.x; var ls = su.localScale; su.localScale = new Vector3(ls.x * f, ls.y, ls.z * f); }
            }
        }
        Move("Kova", new Vector3(bucket.x, 0f, bucket.y));
        Move("OylamaAlani", new Vector3(vote.x, 0f, vote.y));
        Move("BalyaYigini", new Vector3(pileS.x, 0f, pileS.y));
        Move("Yigin_Ada1", new Vector3(pileA1.x, A1.top, pileA1.y));
        Move("Yigin_Ada2", new Vector3(pileA2.x, A2.top, pileA2.y));
        Move("Yigin_Kuluca", new Vector3(pileK.x, K.top, pileK.y));

        var drop = Object.FindFirstObjectByType<AirdropManager>();
        if (drop != null)
        {
            drop.areas = new DropArea[]
            {
                new DropArea { name = "Baslangic", center = new Vector3(3f, 0f, 3f), radius = 9f },
                new DropArea { name = "Ada1", center = new Vector3(A1.pos.x, A1.top, A1.pos.y), radius = 5.5f },
                new DropArea { name = "Ada2", center = new Vector3(A2.pos.x, A2.top, A2.pos.y), radius = 6f },
                new DropArea { name = "Kulucka", center = new Vector3(K.pos.x, K.top, K.pos.y), radius = 5f }
            };
            EditorUtility.SetDirty(drop);
        }

        // nesneler (15): baslangic 6, her ada 3
        var throwPts = new List<Vector3>();
        Vector2[] sl = { new Vector2(-12f, 6f), new Vector2(10f, -8f), new Vector2(-3f, 13f), new Vector2(12f, -2f), new Vector2(-14f, 0f), new Vector2(5f, 12f) };
        foreach (var v in sl) throwPts.Add(new Vector3(S.pos.x + v.x, 0.3f, S.pos.y + v.y));
        Vector2[] exits = { exitA1, exitA2, arriveK * -1f };
        for (int i = 1; i <= 3; i++)
            foreach (float da in new[] { 80f, -80f, 0f })
            {
                Vector2 e = exits[i - 1]; float ang = Mathf.Atan2(e.y, e.x) * Mathf.Rad2Deg + da;
                Vector2 p = isles[i].pos + Dir(ang) * 4.5f;
                throwPts.Add(new Vector3(p.x, isles[i].top + 0.3f, p.y));
            }
        var nes = GameObject.Find("Nesneler");
        if (nes != null) for (int i = 0; i < nes.transform.childCount && i < throwPts.Count; i++) nes.transform.GetChild(i).position = throwPts[i];

        // ---- dekor ----
        var zonesS = new List<Zone> { new Zone(spawn, 4.5f), new Zone(lakeW, S.lakeR * 1.5f + 1.2f), new Zone(pileS, 4.5f), new Zone(bucket, 2.8f), new Zone(vote, 5f) };
        foreach (var v in throwPts) zonesS.Add(new Zone(new Vector2(v.x, v.z), 1.8f));
        var zonesOther = new List<Zone>[3];
        Vector2[] piles = { pileA1, pileA2, pileK };
        for (int i = 0; i < 3; i++)
        {
            zonesOther[i] = new List<Zone> { new Zone(piles[i], 4.2f) };
            foreach (var v in throwPts) zonesOther[i].Add(new Zone(new Vector2(v.x, v.z), 1.8f));
        }
        zonesOther[2].Add(new Zone(K.pos, 6f)); // yuva
        int[][] counts = { new[] { 14, 6, 45, 8, 2, 4 }, new[] { 5, 3, 14, 3, 1, 2 }, new[] { 6, 3, 16, 3, 1, 2 }, new[] { 6, 3, 14, 4, 1, 2 } };
        Decorate(S, 0, isles, zonesS, counts[0], level);
        for (int i = 1; i <= 3; i++) Decorate(isles[i], i, isles, zonesOther[i - 1], counts[i], level);

        // ---- kuluçka: buyuk kus yuvasi + yumurta + bayrak ----
        var hatch = GameObject.Find("KuluckaHedefi");
        if (hatch != null)
        {
            for (int i = hatch.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(hatch.transform.GetChild(i).gameObject);
            hatch.transform.position = new Vector3(K.pos.x, K.top, K.pos.y);
            var trig = hatch.GetComponent<BoxCollider>();
            if (trig != null) { trig.isTrigger = true; trig.size = new Vector3(5.5f, 4.5f, 5.5f); trig.center = new Vector3(0f, 2.2f, 0f); }
            var nest = Place(NestPack + "Nest_11.prefab", hatch.transform.position, 20f, 4.6f, true, hatch.transform, 0f);
            if (nest != null) StripColliders(nest); else log.Append("Nest_11 bulunamadi\n");
            var eggMat = MatLit("Assets/Materials/YuvaYumurta.mat", new Color(1f, 0.97f, 0.88f), 0.55f, true);
            var egg = GameObject.CreatePrimitive(PrimitiveType.Sphere); egg.name = "Yumurta"; Object.DestroyImmediate(egg.GetComponent<Collider>());
            egg.transform.SetParent(hatch.transform, false);
            var eggMesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>("Assets/Meshes/Yumurta.asset");
            if (eggMesh != null) egg.GetComponent<MeshFilter>().sharedMesh = eggMesh;
            egg.transform.localScale = Vector3.one * 0.7f; egg.transform.localPosition = new Vector3(0f, 0.45f, 0f); egg.transform.localRotation = Quaternion.Euler(5f, 20f, 4f);
            egg.GetComponent<Renderer>().sharedMaterial = eggMat;
            var lg = new GameObject("YuvaIsigi"); lg.transform.SetParent(hatch.transform, false); lg.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.85f, 0.45f); l.intensity = 6f; l.range = 14f;
            var poleMat = MatLit("Assets/Materials/YuvaDirek.mat", new Color(0.35f, 0.25f, 0.15f), 0.1f, false);
            var flagMat = MatLit("Assets/Materials/YuvaBayrak.mat", new Color(0.6f, 0.25f, 0.85f), 0.2f, false);
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pole.name = "BayrakDirek"; Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.transform.SetParent(hatch.transform, false); pole.transform.localPosition = new Vector3(3.6f, 1.9f, -1.6f); pole.transform.localScale = new Vector3(0.1f, 1.9f, 0.1f);
            pole.GetComponent<Renderer>().sharedMaterial = poleMat;
            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube); flag.name = "Bayrak"; Object.DestroyImmediate(flag.GetComponent<Collider>());
            flag.transform.SetParent(hatch.transform, false); flag.transform.localPosition = new Vector3(4.25f, 3.25f, -1.6f); flag.transform.localScale = new Vector3(1.3f, 0.8f, 0.04f);
            flag.GetComponent<Renderer>().sharedMaterial = flagMat;
        }

        // ---- cevre: adalar arasi ucan kayalar (+yuva, agac) ve alt bulut denizi ----
        var cevre = new GameObject("Cevre").transform;
        var rr = new System.Random(900);
        string[] fr = { "Rock 3 Moss", "Rock 1 Moss", "Rock 2", "Rock 4", "Rock 5", "Rock 3" };
        int rockCount = 0, nestCount = 0;
        for (int gi = 0; gi < isles.Count - 1; gi++)
        {
            Isle a = isles[gi], b = isles[gi + 1];
            Vector2 dv = (b.pos - a.pos).normalized; Vector2 perp = new Vector2(-dv.y, dv.x);
            float D = Vector2.Distance(a.pos, b.pos);
            int n = gi == 0 ? 5 : 7;
            for (int i = 0; i < n; i++)
            {
                float side = (i % 2 == 0) ? 1f : -1f;
                float lateral = 10f + (float)rr.NextDouble() * 12f;
                float along = 0.15f + (float)rr.NextDouble() * 0.7f;
                Vector2 p2 = a.pos + dv * (D * along) + perp * (side * lateral);
                bool inside = false;
                foreach (var isl in isles) if (Vector2.Distance(p2, isl.pos) < isl.R * 1.35f + 4f) inside = true;
                if (inside) continue;
                float y = Mathf.Lerp(a.top, b.top, along) + Mathf.Lerp(-18f, -3f, (float)rr.NextDouble());
                var rock = Place(Pack + fr[rr.Next(fr.Length)] + ".prefab", new Vector3(p2.x, y, p2.y), (float)rr.NextDouble() * 360f, 4f + (float)rr.NextDouble() * 4.5f, true, cevre, 0f);
                if (rock == null) continue;
                StripColliders(rock); rockCount++;
                var rb = BB(rock);
                if (rr.NextDouble() < 0.7)
                {
                    var nst = Place(NestPack + "Nest_" + rr.Next(1, 26).ToString("00") + ".prefab", new Vector3(rb.center.x, rb.max.y - 0.2f, rb.center.z), (float)rr.NextDouble() * 360f, 1.0f + (float)rr.NextDouble() * 0.8f, true, cevre, 0f);
                    StripColliders(nst); nestCount++;
                }
                else
                {
                    var tr = Place(Pack + "Tree 3 G1.prefab", new Vector3(rb.center.x, rb.max.y - 0.2f, rb.center.z), (float)rr.NextDouble() * 360f, 3.5f, false, cevre, 0f);
                    StripColliders(tr);
                }
            }
        }
        string[] clouds = { "Cloud 1 White 1", "Clouds 2 White 1", "Clouds 3 White 1" };
        float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
        foreach (var isl in isles) { minX = Mathf.Min(minX, isl.pos.x - isl.rx); maxX = Mathf.Max(maxX, isl.pos.x + isl.rx); minZ = Mathf.Min(minZ, isl.pos.y - isl.rz); maxZ = Mathf.Max(maxZ, isl.pos.y + isl.rz); }
        for (int i = 0; i < 30; i++)
        {
            float cx = Mathf.Lerp(minX - 40f, maxX + 40f, (float)rr.NextDouble()), cz = Mathf.Lerp(minZ - 40f, maxZ + 40f, (float)rr.NextDouble());
            StripColliders(Place(Pack + clouds[rr.Next(clouds.Length)] + ".prefab", new Vector3(cx, -34f - (float)rr.NextDouble() * 12f, cz), (float)rr.NextDouble() * 360f, 18f + (float)rr.NextDouble() * 14f, true, cevre, 0f));
        }
        log.Append("Cevre: ").Append(rockCount).Append(" ucan kaya, ").Append(nestCount).Append(" yuva (kayalarin ustunde), 30 bulut\n");

        // ---- sayisal dogrulama ----
        Physics.SyncTransforms();
        log.Append("YUZEY KONTROLU:\n");
        System.Action<string, Vector2, float> Chk = (nm, p, expect) =>
        {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(p.x, expect + 40f, p.y), Vector3.down, out hit, 120f, ~0, QueryTriggerInteraction.Ignore))
                log.Append("  ").Append(nm).Append(": beklenen ").Append(expect.ToString("F1")).Append(" / bulunan ").Append(hit.point.y.ToString("F2")).Append(Mathf.Abs(hit.point.y - expect) > 0.3f ? "  <-- FARKLI (" + hit.collider.name + ")" : "").Append("\n");
            else log.Append("  ").Append(nm).Append(": ZEMIN YOK!\n");
        };
        Chk("Oyuncu", spawn, 0f); Chk("Bale yigini Baslangic", pileS, 0f); Chk("Kova", bucket, 0f); Chk("Oylama", vote, 0f);
        Chk("Ada1 yigin", pileA1, A1.top); Chk("Ada2 yigin", pileA2, A2.top); Chk("Kulucka yigin", pileK, K.top); Chk("Kulucka yuva", K.pos, K.top);
        Chk("Gol ortasi", lakeW, -S.lakeDepth);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        log.Append(LakeBuilder.Build(S.lakeR));
        return log.ToString();
    }
}
#endif
