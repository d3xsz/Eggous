using UnityEngine;

// Yumruk sesleri: kodla uretilir (asset gerekmez).
// Swing = savurma (her yumrukta), Impact = isabet (tok vurus).
public static class PunchSound
{
    static AudioClip whoosh, thud;

    public static void Swing(Vector3 pos)
    {
        if (whoosh == null) whoosh = MakeWhoosh();
        Play(whoosh, pos, 0.5f);
    }

    public static void Impact(Vector3 pos)
    {
        if (thud == null) thud = MakeCrack();
        Play(thud, pos, 1f);
    }

    static void Play(AudioClip c, Vector3 pos, float vol)
    {
        var go = new GameObject("YumrukSesi");
        go.transform.position = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip = c; a.volume = vol;
        a.spatialBlend = 1f; a.minDistance = 4f; a.maxDistance = 40f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.pitch = Random.Range(0.92f, 1.08f);
        a.Play();
        Object.Destroy(go, c.length / a.pitch + 0.1f);
    }

    static AudioClip MakeWhoosh()
    {
        int sr = 44100, n = (int)(sr * 0.2f);
        var d = new float[n];
        var rng = new System.Random(3);
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float env = Mathf.Sin(Mathf.PI * t); env *= env;
            float k = Mathf.Lerp(0.08f, 0.35f, t);           // yukselen parlaklik
            float x = (float)rng.NextDouble() * 2f - 1f;
            y += (x - y) * k;
            d[i] = y * env;
        }
        return Make("YumrukWhoosh", d, sr);
    }

    static AudioClip MakeThud()
    {
        int sr = 44100, n = (int)(sr * 0.32f);
        var d = new float[n];
        var rng = new System.Random(5);
        float phase = 0f, y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float f = 55f + 110f * Mathf.Exp(-t * 30f);        // dusen govde frekansi
            phase += 2f * Mathf.PI * f / sr;
            float body = Mathf.Sin(phase) * Mathf.Exp(-t * 14f);
            float x = (float)rng.NextDouble() * 2f - 1f;
            y += (x - y) * 0.5f;
            float crack = y * Mathf.Exp(-t * 90f) * 0.9f;       // carpma ani
            float mid = Mathf.Sin(2f * Mathf.PI * 220f * t) * Mathf.Exp(-t * 40f) * 0.35f;
            d[i] = (body * 1.1f + crack + mid) * Mathf.Min(1f, t * 1500f);
        }
        return Make("YumrukTok", d, sr);
    }

    // Yumurta kirilma sesi: kabuk catlama tiklari + kabuk carsirtisi + islak ezilme + kisa govde tok'u
    static AudioClip MakeCrack()
    {
        int sr = 44100, n = (int)(sr * 0.38f);
        var d = new float[n];
        var rng = new System.Random(11);
        float lp = 0f, lp2 = 0f, phase = 0f;
        float[] clickT = { 0f, 0.012f, 0.027f, 0.041f, 0.063f, 0.088f, 0.115f };
        float[] clickA = { 1f, 0.7f, 0.8f, 0.5f, 0.55f, 0.35f, 0.25f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float x = (float)rng.NextDouble() * 2f - 1f;
            lp += (x - lp) * 0.6f;
            float hp = x - lp;                                   // yuksek frekans: kabuk sesi
            float s = 0f;
            for (int k = 0; k < clickT.Length; k++)
            {
                float dt = t - clickT[k];
                if (dt >= 0f) s += hp * clickA[k] * Mathf.Exp(-dt * 400f);   // mikro catlaklar
            }
            s += hp * Mathf.Exp(-t * 35f) * 0.5f;                // kabuk carsirtisi
            lp2 += (x - lp2) * 0.12f;
            float sq = Mathf.Clamp01((t - 0.03f) * 40f) * Mathf.Exp(-(t - 0.03f) * 14f);
            float wob = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 28f * t);
            s += lp2 * sq * wob * 2.2f;                          // islak ezilme
            phase += 2f * Mathf.PI * (90f + 100f * Mathf.Exp(-t * 60f)) / sr;
            s += Mathf.Sin(phase) * Mathf.Exp(-t * 40f) * 0.4f;  // govde tok'u
            d[i] = s;
        }
        return Make("YumurtaKirilma", d, sr);
    }

    static AudioClip Make(string name, float[] d, int sr)
    {
        float peak = 0.0001f;
        for (int i = 0; i < d.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
        for (int i = 0; i < d.Length; i++) d[i] *= 0.9f / peak;
        var c = AudioClip.Create(name, d.Length, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }
}
