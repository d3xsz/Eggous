using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DropArea
{
    public string name;
    public Vector3 center;
    public float radius = 5f;
}

// SIRA PLANI adim 5: rastgele item drop (airdrop). Rastgele zaman + rastgele alan; kutu gokten duser, SESLE ve ekran yazisiyla
// kendini belli eder. Tum sureler/alanlar/sesler Inspector'dan ayarlanir. Sesler gecici (uretilmis WAV), sonra degistirilebilir.
public class AirdropManager : MonoBehaviour
{
    public static AirdropManager Instance;

    [Header("Zamanlama (rastgele)")]
    public float firstDelayMin = 15f;
    public float firstDelayMax = 30f;
    public float minInterval = 30f;
    public float maxInterval = 60f;
    public int maxAlive = 2;

    [Header("Dusme")]
    public float dropHeight = 45f;
    public float fallSpeed = 9f;
    public float crateLifetime = 60f;   // yere indikten sonra kaybolma suresi

    [Header("Dusme alanlari")]
    public DropArea[] areas;

    [Header("Sesler (sonra degistirilebilir)")]
    public AudioClip alarmClip;
    public AudioClip fallClip;
    public AudioClip landClip;
    public AudioClip pickupClip;
    public AudioClip popClip;    // balon patlama
    public AudioClip openClip;   // sandik acilma
    [Range(0f, 1f)] public float alarmVolume = 0.7f;
    [Range(0f, 1f)] public float fallVolume = 1f;
    [Range(0f, 1f)] public float landVolume = 1f;
    [Range(0f, 1f)] public float pickupVolume = 0.8f;

    [Header("Alan sirasi")]
    public bool loopAfterLast = true;   // son alandan sonra basa don (kapaliysa son alanda kalir)

    float nextAt;
    int lastArea = -1;
    int nextArea;   // sira: areas[0] (baslangic) -> ileriye dogru
    AudioSource ui;
    readonly List<AirdropCrate> alive = new List<AirdropCrate>();

    void Awake()
    {
        Instance = this;
        ui = gameObject.AddComponent<AudioSource>();
        ui.spatialBlend = 0f;
        ui.playOnAwake = false;
    }

    void Start() { nextArea = 0; nextAt = Time.time + Random.Range(firstDelayMin, firstDelayMax); }

    public void PlayUI(AudioClip clip, float vol)
    {
        if (clip != null) ui.PlayOneShot(clip, vol);
    }

    float dbgAt; int dbgCount;

    void Update()
    {
        // TESHIS: oyun boyunca 20 sn'de bir durumu konsola yazar
        if (dbgCount < 300 && Time.time >= dbgAt)
        {
            dbgAt = Time.time + 20f; dbgCount++;
            Debug.Log("[Airdrop] ag=" + NetWorld.Active + " sunucu=" + NetWorld.IsServerNow + " tur-oynaniyor=" + (GameManager.Instance == null || GameManager.Instance.IsPlaying)
                + " sonraki-drop=" + (nextAt - Time.time).ToString("F0") + "sn alan-sayisi=" + (areas != null ? areas.Length : 0) + " yasayan=" + alive.Count);
        }
        if (NetWorld.Active && !NetWorld.IsServerNow) return;   // ag: dusme kararini sunucu verir
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) { nextAt += Time.deltaTime; return; }
        alive.RemoveAll(c => c == null);
        if (Time.time >= nextAt && alive.Count < maxAlive && areas != null && areas.Length > 0)
        {
            Spawn();
            nextAt = Time.time + Random.Range(minInterval, maxInterval);
        }
    }

    void Spawn()
    {
        int idx = Mathf.Clamp(nextArea, 0, areas.Length - 1);   // sirayla: once baslangic, sonra ileriye
        nextArea = loopAfterLast ? (idx + 1) % areas.Length : Mathf.Min(idx + 1, areas.Length - 1);
        lastArea = idx;
        var a = areas[idx];
        Debug.Log("[Airdrop] Spawn alan=" + a.name + " ag=" + NetWorld.Active);
        Vector2 o = Random.insideUnitCircle * a.radius;
        Vector3 p = new Vector3(a.center.x + o.x, a.center.y, a.center.z + o.y);
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out hit, 400f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point;
        if (NetWorld.Active) { NetWorld.AirdropSpawn(p); return; }   // herkeste SpawnAt calisir
        try { alive.Add(AirdropCrate.Create(this, p)); }
        catch (System.Exception e) { Debug.LogError("[Airdrop] Sandik olusturulamadi: " + e); }
        PlayUI(alarmClip, alarmVolume);
    }

    public void SpawnAt(Vector3 p, int netId)
    {
        Debug.Log("[Airdrop] SpawnAt konum=" + p + " id=" + netId);
        try
        {
            var c = AirdropCrate.Create(this, p);
            c.netId = netId;
            alive.Add(c);
            PlayUI(alarmClip, alarmVolume);
        }
        catch (System.Exception e) { Debug.LogError("[Airdrop] Sandik olusturulamadi: " + e); }
    }

    public AirdropCrate GetByNetId(int id)
    {
        foreach (var c in alive) if (c != null && c.netId == id) return c;
        return null;
    }

    // Gecici UI: en yakin kutunun yonu/mesafesi (Faz 6'da guzellestirilecek)
    void OnGUI()
    {
        var player = PlayerController.LocalObject();
        if (player == null) return;
        AirdropCrate best = null;
        float bd = 1e9f;
        foreach (var c in alive)
        {
            if (c == null || c.Opened) continue;
            float d = Vector3.Distance(player.transform.position, c.GroundPos);
            if (d < bd) { bd = d; best = c; }
        }
        if (best == null) return;
        Vector3 to = best.GroundPos - player.transform.position;
        to.y = 0f;
        Vector3 fwd = player.transform.forward;
        fwd.y = 0f;
        float ang = Vector3.SignedAngle(fwd, to, Vector3.up);
        string dir = Mathf.Abs(ang) < 35f ? "onunde" : (Mathf.Abs(ang) > 145f ? "arkanda" : (ang > 0f ? "saginda" : "solunda"));
        string title = best.Landed ? "ITEM KUTUSU YERDE" : "ITEM DUSUYOR!";
        GUI.Label(GameHUD.Air(title + "  -  " + Mathf.RoundToInt(bd) + " m, " + dir, new Rect(Screen.width / 2f - 200, 62, 420, 24)), title + "   " + Mathf.RoundToInt(bd) + " m, " + dir);
    }
}
