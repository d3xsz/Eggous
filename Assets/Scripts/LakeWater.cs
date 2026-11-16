using System.Collections.Generic;
using UnityEngine;

// Suya girince: yurume yavaslar (sig=1 yuruyus, derin=yuzme), ziplama zayiflar, sicrama sesi + parcacik.
// Insan (PlayerController.envSpeed/envJump) ve botlar (BotController.moveSpeed) etkilenir.
public class LakeWater : MonoBehaviour
{
    public float edgeRadius = 5.1f;      // suyun karaya degdigi yaricap (LakeBuilder hesaplar)
    public float waterY = -0.35f;
    public float shallowSpeed = 0.70f, deepSpeed = 0.40f;
    public float shallowJump = 0.80f, deepJump = 0.50f;
    public float deepRange = 0.40f;      // yuzeyin bu kadar altinda 'derin' sayilir
    public AudioClip splashClip;
    [Range(0f, 1f)] public float volume = 0.8f;
    public float loopSeconds = 1f;       // su icindeyken dongude calan ses kismi (klibin ilk kac sn'si)

    PlayerController human;
    bool humanIn;
    BotController[] bots = new BotController[0];
    readonly Dictionary<BotController, float> baseSpeed = new Dictionary<BotController, float>();
    readonly Dictionary<BotController, bool> botIn = new Dictionary<BotController, bool>();
    float refresh;
    static Material fxMat;

    void Start() { human = PlayerController.LocalOrFind(); BuildLoop(); }

    float Depth01(Vector3 p)
    {
        Vector2 d = new Vector2(p.x - transform.position.x, p.z - transform.position.z);
        if (d.magnitude >= edgeRadius || p.y >= waterY + 0.05f) return -1f;     // suda degil
        return Mathf.Clamp01((waterY - p.y) / deepRange);
    }

    void Update()
    {
        if (human == null) human = PlayerController.LocalOrFind();
        UpdateLoop();
        if (human != null)
        {
            float d = Depth01(human.transform.position);
            bool inW = d >= 0f;
            human.envSpeed = inW ? Mathf.Lerp(shallowSpeed, deepSpeed, d) : 1f;
            human.envJump = inW ? Mathf.Lerp(shallowJump, deepJump, d) : 1f;
            if (inW != humanIn) { humanIn = inW; Splash(human.transform.position, false); }
        }
        refresh -= Time.deltaTime;
        if (refresh <= 0f)
        {
            refresh = 1f;
            bots = FindObjectsByType<BotController>(FindObjectsSortMode.None);
        }
        foreach (var b in bots)
        {
            if (b == null) continue;
            if (!baseSpeed.ContainsKey(b)) { baseSpeed[b] = b.moveSpeed; botIn[b] = false; }
            float d = Depth01(b.transform.position);
            bool inW = d >= 0f;
            b.moveSpeed = baseSpeed[b] * (inW ? Mathf.Lerp(shallowSpeed, deepSpeed, d) : 1f);
            if (inW != botIn[b]) { botIn[b] = inW; Splash(b.transform.position, inW); }
        }
    }

    // Uzun klibin sadece ilk 'loopSeconds' saniyesini calar (son 0.4 sn yumusak kapanir)
    System.Collections.IEnumerator PlayShort(Vector3 pos)
    {
        var g = new GameObject("SuSesi");
        g.transform.position = pos;
        Destroy(g, loopSeconds + 1f);
        var a = g.AddComponent<AudioSource>();
        a.clip = splashClip; a.volume = volume; a.loop = false; a.spatialBlend = 1f; a.minDistance = 3f; a.maxDistance = 60f;
        a.Play();
        float fade = Mathf.Min(0.4f, loopSeconds * 0.5f), t = 0f;
        while (t < loopSeconds && a != null)
        {
            t += Time.unscaledDeltaTime;
            float rem = loopSeconds - t;
            a.volume = rem < fade ? volume * Mathf.Clamp01(rem / fade) : volume;
            yield return null;
        }
        if (g != null) Destroy(g);
    }

    // ---- Su icindeyken sesin ilk 'loopSeconds' saniyesi dongude calar, sudan cikinca kisilip susar ----
    AudioSource loopSrc; bool manualLoop;

    void BuildLoop()
    {
        if (splashClip == null) return;
        var go = new GameObject("SuSesiLoop");
        go.transform.SetParent(transform, false);
        loopSrc = go.AddComponent<AudioSource>();
        loopSrc.playOnAwake = false; loopSrc.spatialBlend = 0f; loopSrc.volume = 0f;
        int ch = splashClip.channels, freq = splashClip.frequency;
        int n = Mathf.Min(splashClip.samples, Mathf.RoundToInt(loopSeconds * freq));
        int xf = Mathf.Min(Mathf.RoundToInt(0.08f * freq), n / 4);
        int total = Mathf.Min(splashClip.samples, n + xf);
        var d = new float[total * ch];
        bool ok = false;
        try { ok = splashClip.GetData(d, 0); } catch { ok = false; }
        if (ok && total == n + xf)
        {
            // pürüzsüz döngü: başın üstüne, kesilen yerden devam eden kısım çapraz geçişle karıştırılır
            var o = new float[n * ch];
            for (int i = 0; i < n * ch; i++) o[i] = d[i];
            for (int k = 0; k < xf; k++)
            {
                float a = (float)k / xf;
                for (int c = 0; c < ch; c++) o[k * ch + c] = d[k * ch + c] * a + d[(n + k) * ch + c] * (1f - a);
            }
            var loopClip = AudioClip.Create("SuLoop", n, ch, freq, false);
            loopClip.SetData(o, 0);
            loopSrc.clip = loopClip; loopSrc.loop = true;
        }
        else
        {
            // klibin verisi okunamadi (Load Type): yedek yontem, sure dolunca basa sar
            loopSrc.clip = splashClip; loopSrc.loop = false; manualLoop = true;
        }
    }

    void UpdateLoop()
    {
        if (loopSrc == null) return;
        bool want = humanIn;
        if (want && !loopSrc.isPlaying) { loopSrc.time = 0f; loopSrc.Play(); }
        float step = Time.unscaledDeltaTime * volume / (want ? 0.1f : 0.2f);
        loopSrc.volume = Mathf.MoveTowards(loopSrc.volume, want ? volume : 0f, step);
        if (!want && loopSrc.isPlaying && loopSrc.volume <= 0.001f) loopSrc.Stop();
        if (manualLoop && loopSrc.isPlaying && loopSrc.time >= loopSeconds) loopSrc.time = 0f;
    }

    void Splash(Vector3 pos, bool sound)
    {
        pos.y = waterY;
        if (sound && splashClip != null) StartCoroutine(PlayShort(pos));
        var go = new GameObject("Sicrama");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false; main.loop = false; main.duration = 0.5f; main.startLifetime = 0.7f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.92f, 1f), new Color(1f, 1f, 1f));
        main.gravityModifier = 1.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission; em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)22) });
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 28f; sh.radius = 0.25f;
        sh.rotation = new Vector3(-90f, 0f, 0f);   // yukari
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        var rend = go.GetComponent<ParticleSystemRenderer>();
        if (fxMat == null)
        {
            var shd = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shd == null) shd = Shader.Find("Sprites/Default");
            fxMat = new Material(shd);
            fxMat.SetColor("_BaseColor", Color.white);
        }
        rend.sharedMaterial = fxMat;
        ps.Play();
    }
}
