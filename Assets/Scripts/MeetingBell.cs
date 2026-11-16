using UnityEngine;

// Toplanti zili: biri toplanti cagirinca calar (sallanma animasyonu + prosedural zil sesi).
public class MeetingBell : MonoBehaviour
{
    public Transform swing;   // sallanan zil govdesi (pivot: aski noktasi)
    [Tooltip("Bos birakilirsa Resources/ToplantiZili, o da yoksa uretilmis zil sesi calar.")]
    public AudioClip customClip;
    AudioSource src;
    static AudioClip clip;
    float ringStart = -10f;
    const float RingTime = 2.8f;

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false; src.spatialBlend = 1f;
        src.minDistance = 8f; src.maxDistance = 90f;
        src.rolloffMode = AudioRolloffMode.Linear; src.volume = 0.9f;
    }

    public void Ring()
    {
        ringStart = Time.time;
        if (clip == null) clip = customClip != null ? customClip : Resources.Load<AudioClip>("ToplantiZili");
        if (clip == null) clip = MakeClip();
        src.PlayOneShot(clip);
    }

    void Update()
    {
        if (swing == null) return;
        float t = Time.time - ringStart;
        if (t >= RingTime) { swing.localRotation = Quaternion.identity; return; }
        float a = Mathf.Sin(t * 16f) * 28f * (1f - t / RingTime);
        swing.localRotation = Quaternion.Euler(0f, 0f, a);
    }

    // Bronz zil: temel ton + eslesmeyen ust tonlar (hum, prime, tierce, quint, nominal ...)
    static AudioClip MakeClip()
    {
        int sr = 44100;
        float len = 3.2f;
        int n = (int)(sr * len);
        var d = new float[n];
        float f0 = 330f;
        float[] ratio = { 0.5f, 1f, 1.19f, 1.5f, 2f, 2.51f, 3.01f, 4.17f };
        float[] amp   = { 0.30f, 0.60f, 0.35f, 0.30f, 0.35f, 0.15f, 0.10f, 0.06f };
        float[] decay = { 1.1f, 1.5f, 1.9f, 2.3f, 3.0f, 4.0f, 5.5f, 8.0f };
        float[] hits = { 0f, 0.6f, 1.2f };
        var rng = new System.Random(7);
        for (int h = 0; h < hits.Length; h++)
        {
            int start = (int)(hits[h] * sr);
            float gain = h == 0 ? 1f : 0.75f;
            for (int i = start; i < n; i++)
            {
                float t = (i - start) / (float)sr;
                float s = 0f;
                for (int k = 0; k < ratio.Length; k++)
                    s += amp[k] * Mathf.Sin(2f * Mathf.PI * f0 * ratio[k] * t + k * 0.7f) * Mathf.Exp(-t * decay[k]);
                if (t < 0.02f) s += ((float)rng.NextDouble() * 2f - 1f) * 0.4f * (1f - t / 0.02f);
                d[i] += s * gain * Mathf.Min(1f, t * 800f);
            }
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
        int fade = sr / 4;
        for (int i = 0; i < n; i++)
        {
            float g = 0.85f / peak;
            if (i > n - fade) g *= (n - i) / (float)fade;
            d[i] *= g;
        }
        var c = AudioClip.Create("ZilSesi", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }
}
