using UnityEngine;
using UnityEngine.InputSystem;

// Gokten BALONLA inen SANDIK. Yere inince balon patlar. Sandiga bakip E = sandigi ac -> rastgele item envantere (bos slot varsa).
// Sari isik sutunu YOK: sadece ses + ekran yazisi + renkli balon kendini belli eder.
public class AirdropCrate : MonoBehaviour
{
    public Vector3 GroundPos { get; private set; }
    public bool Landed { get; private set; }
    public bool Opened { get; private set; }
    public int netId = -1;   // ag: sunucunun verdigi kimlik

    AirdropManager mgr;
    float groundY, landedAt, openedAt, t0;
    Vector3 basePos;
    AudioSource src;
    Transform balloonRoot, lidPivot;
    GameObject gem; Light gemLight; ItemType gemType;
    Color balloonColor;
    bool canInteract;

    // Build'de LitShader() null donebilir (shader pakete girmediyse) -> new Material(null) hata verir.
    // Once Find, olmazsa sahnedeki herhangi bir URP malzemesinin shader'ini, o da olmazsa Sprites/Default kullan.
    static Shader litShader;
    static Shader LitShader()
    {
        if (litShader != null) return litShader;
        litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null)
        {
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var sm = r.sharedMaterial;
                if (sm != null && sm.shader != null && sm.HasProperty("_BaseColor")) { litShader = sm.shader; break; }
            }
        }
        if (litShader == null) litShader = Shader.Find("Sprites/Default");
        return litShader;
    }

    static Material M(Color c, float sm)
    {
        var m = new Material(LitShader());
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c; m.SetFloat("_Smoothness", sm);
        return m;
    }
    static GameObject P(Transform parent, PrimitiveType t, string nm, Vector3 lp, Vector3 sc, Material m)
    {
        var g = GameObject.CreatePrimitive(t); g.name = nm;
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = sc;
        var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    public static AirdropCrate Create(AirdropManager m, Vector3 ground)
    {
        var root = new GameObject("ItemDropSandik");
        root.transform.position = ground + Vector3.up * m.dropHeight;
        var wood = M(new Color(0.55f, 0.36f, 0.18f), 0.15f);
        var woodD = M(new Color(0.40f, 0.26f, 0.12f), 0.15f);
        var metal = M(new Color(0.26f, 0.28f, 0.32f), 0.55f);
        var gold = M(new Color(0.95f, 0.78f, 0.25f), 0.65f);
        Transform tr = root.transform;

        P(tr, PrimitiveType.Cube, "Govde", new Vector3(0f, 0.31f, 0f), new Vector3(1.0f, 0.62f, 0.72f), wood);
        P(tr, PrimitiveType.Cube, "BantSol", new Vector3(-0.33f, 0.31f, 0f), new Vector3(0.09f, 0.64f, 0.75f), metal);
        P(tr, PrimitiveType.Cube, "BantSag", new Vector3(0.33f, 0.31f, 0f), new Vector3(0.09f, 0.64f, 0.75f), metal);
        P(tr, PrimitiveType.Cube, "Kilit", new Vector3(0f, 0.55f, 0.375f), new Vector3(0.16f, 0.18f, 0.05f), gold);
        var pivot = new GameObject("KapakMentese").transform;                       // arka ust kenar
        pivot.SetParent(tr, false); pivot.localPosition = new Vector3(0f, 0.62f, -0.36f);
        P(pivot, PrimitiveType.Cube, "Kapak", new Vector3(0f, 0.08f, 0.36f), new Vector3(1.04f, 0.16f, 0.76f), woodD);
        P(pivot, PrimitiveType.Cube, "KapakBantSol", new Vector3(-0.33f, 0.08f, 0.36f), new Vector3(0.09f, 0.18f, 0.78f), metal);
        P(pivot, PrimitiveType.Cube, "KapakBantSag", new Vector3(0.33f, 0.08f, 0.36f), new Vector3(0.09f, 0.18f, 0.78f), metal);

        // balon
        Color[] cols = { new Color(0.88f, 0.18f, 0.20f), new Color(0.22f, 0.50f, 0.95f), new Color(0.62f, 0.28f, 0.88f), new Color(0.15f, 0.72f, 0.66f) };
        Color bc = cols[Random.Range(0, cols.Length)];
        var balloon = new GameObject("Balon").transform;
        balloon.SetParent(tr, false);
        P(balloon, PrimitiveType.Sphere, "BalonGovde", new Vector3(0f, 4.6f, 0f), new Vector3(2.3f, 2.8f, 2.3f), M(bc, 0.75f));
        P(balloon, PrimitiveType.Sphere, "BalonBoyun", new Vector3(0f, 3.15f, 0f), new Vector3(0.32f, 0.2f, 0.32f), M(bc * 0.8f, 0.6f));
        var rope = M(new Color(0.85f, 0.8f, 0.7f), 0.1f);
        Vector3 knot = new Vector3(0f, 3.1f, 0f);
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                Vector3 end = new Vector3(0.42f * sx, 0.64f, 0.30f * sz);
                Vector3 d = end - knot; float len = d.magnitude;
                var s = P(balloon, PrimitiveType.Cylinder, "Ip", knot + d * 0.5f, new Vector3(0.03f, len * 0.5f, 0.03f), rope);
                s.transform.localRotation = Quaternion.LookRotation(d) * Quaternion.Euler(90f, 0f, 0f);
            }

        var c = root.AddComponent<AirdropCrate>();
        c.mgr = m; c.GroundPos = ground; c.groundY = ground.y + 0.01f; c.basePos = root.transform.position; c.t0 = Time.time;
        c.balloonRoot = balloon; c.lidPivot = pivot; c.balloonColor = bc;
        c.src = root.AddComponent<AudioSource>();
        c.src.clip = m.fallClip; c.src.spatialBlend = 1f; c.src.rolloffMode = AudioRolloffMode.Linear;
        c.src.minDistance = 8f; c.src.maxDistance = 170f; c.src.volume = m.fallVolume;
        if (m.fallClip != null) c.src.Play();
        return c;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (!Landed)
        {
            basePos += Vector3.down * mgr.fallSpeed * dt;
            float t = Time.time - t0;
            float amp = Mathf.Clamp01((basePos.y - groundY) / 8f) * 0.9f;       // yere yaklastikca salinim biter
            transform.position = basePos + new Vector3(Mathf.Sin(t * 0.8f) * amp, 0f, Mathf.Cos(t * 0.6f) * amp);
            transform.rotation = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 3f * amp, t * 12f, Mathf.Sin(t * 1.3f) * 5f * amp);
            if (balloonRoot != null) balloonRoot.localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(t * 3f));
            if (transform.position.y <= groundY) Land();
            return;
        }
        if (!Opened)
        {
            if (Time.time - landedAt > mgr.crateLifetime) { Destroy(gameObject); return; }
            Interact();
            return;
        }
        // acilma animasyonu
        float k = Mathf.Clamp01((Time.time - openedAt) / 0.5f);
        float ease = 1f - Mathf.Pow(1f - k, 3f);
        lidPivot.localRotation = Quaternion.Euler(-105f * ease, 0f, 0f);
        float gt = Time.time - openedAt - 0.15f;
        if (gem != null)
        {
            if (gt > 0f)
            {
                gem.transform.position = transform.position + new Vector3(0f, 0.8f + 0.9f * Mathf.Min(1f, gt / 0.9f), 0f);
                float s = Mathf.Lerp(0.05f, 0.34f, Mathf.Clamp01(gt / 0.3f)) * Mathf.Clamp01((1.5f - gt) / 0.5f);
                gem.transform.localScale = Vector3.one * s;
                gem.transform.Rotate(0f, 240f * dt, 0f, Space.World);
                if (gemLight != null) gemLight.intensity = 4f * Mathf.Clamp01((1.5f - gt) / 0.5f);
            }
            if (gt > 1.5f) { Destroy(gem); gem = null; }
        }
        if (Time.time - openedAt > 6f) Destroy(gameObject);
    }

    void Land()
    {
        Landed = true; landedAt = Time.time;
        transform.position = new Vector3(GroundPos.x, groundY, GroundPos.z);
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (src != null) src.Stop();
        if (balloonRoot != null)
        {
            Vector3 bp = balloonRoot.position + Vector3.up * 4.6f;
            Burst(bp, balloonColor);
            if (mgr.popClip != null) AudioSource.PlayClipAtPoint(mgr.popClip, bp, mgr.landVolume);
            Destroy(balloonRoot.gameObject);
        }
        if (mgr.landClip != null) AudioSource.PlayClipAtPoint(mgr.landClip, transform.position, mgr.landVolume);
        var bc = gameObject.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 0.31f, 0f); bc.size = new Vector3(1.05f, 0.62f, 0.76f);
    }

    static ItemType RandomItem() { return (ItemType)Random.Range(1, 5); }
    static Color ItemCol(ItemType t)
    {
        switch (t)
        {
            case ItemType.CivcivRoketi: return new Color(1f, 0.35f, 0.25f);
            case ItemType.UykuTuyu: return new Color(0.7f, 0.85f, 1f);
            case ItemType.YagLekesi: return new Color(0.75f, 0.45f, 1f);
            default: return new Color(0.95f, 0.85f, 0.6f);
        }
    }

    void Interact()
    {
        canInteract = false;
        var it = ItemSystem.Local;
        if (it == null) return;
        var carrier = it.GetComponent<BaleCarrier>();
        if (carrier == null || carrier.cam == null) return;
        Vector3 center = transform.position + Vector3.up * 0.4f;
        float d = Vector3.Distance(it.transform.position, transform.position);
        Vector3 to = (center - carrier.cam.transform.position).normalized;
        canInteract = d < 3.6f && Vector3.Dot(carrier.cam.transform.forward, to) > 0.80f;
        var kb = Keyboard.current;
        if (canInteract && kb != null && kb.eKey.wasPressedThisFrame)
        {
            if (!it.HasFreeSlot) { it.Say("Envanter dolu!"); return; }
            Open(it);
        }
    }

    void Open(ItemSystem it)
    {
        Opened = true; openedAt = Time.time; canInteract = false;
        gemType = RandomItem();
        it.Give(gemType);
        NetWorld.AirdropOpen(netId, (int)gemType);
        if (mgr.openClip != null) AudioSource.PlayClipAtPoint(mgr.openClip, transform.position, mgr.landVolume);
        mgr.PlayUI(mgr.pickupClip, mgr.pickupVolume);
        gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(gem.GetComponent<Collider>());
        gem.name = "ItemIsigi";
        var mat = M(ItemCol(gemType), 0.9f);
        mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", ItemCol(gemType) * 1.6f);
        gem.GetComponent<Renderer>().sharedMaterial = mat;
        gem.transform.position = transform.position + Vector3.up * 0.8f; gem.transform.localScale = Vector3.one * 0.05f;
        var lg = new GameObject("L"); lg.transform.SetParent(gem.transform, false);
        gemLight = lg.AddComponent<Light>(); gemLight.type = LightType.Point; gemLight.color = ItemCol(gemType); gemLight.range = 6f; gemLight.intensity = 4f;
    }

    // Ag: baska oyuncu actiginda sadece animasyon/gorsel (item acana verilir)
    public void RemoteOpen(int type)
    {
        if (Opened) return;
        if (!Landed) { Destroy(gameObject); return; }
        Opened = true; openedAt = Time.time; canInteract = false;
        gemType = (ItemType)type;
        if (mgr.openClip != null) AudioSource.PlayClipAtPoint(mgr.openClip, transform.position, mgr.landVolume);
        gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(gem.GetComponent<Collider>());
        gem.name = "ItemIsigi";
        var mat = M(ItemCol(gemType), 0.9f);
        mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", ItemCol(gemType) * 1.6f);
        gem.GetComponent<Renderer>().sharedMaterial = mat;
        gem.transform.position = transform.position + Vector3.up * 0.8f; gem.transform.localScale = Vector3.one * 0.05f;
    }

    static void Burst(Vector3 pos, Color c)
    {
        var go = new GameObject("BalonPatlama"); go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.playOnAwake = false; main.loop = false; main.duration = 0.5f; main.startLifetime = 1.1f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f); main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.5f));
        main.gravityModifier = 0.9f; main.simulationSpace = ParticleSystemSimulationSpace.World; main.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)36) });
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 1.0f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        var rend = go.GetComponent<ParticleSystemRenderer>();
        var shd = Shader.Find("Universal Render Pipeline/Particles/Unlit"); if (shd == null) shd = Shader.Find("Sprites/Default");
        var mat = new Material(shd); mat.SetColor("_BaseColor", Color.white); rend.sharedMaterial = mat;
        ps.Play();
    }

    void OnGUI()
    {
        if (!Landed || Opened || !canInteract) return;
        var it = ItemSystem.Local;
        string pt = (it != null && !it.HasFreeSlot) ? "Envanter dolu" : "E: Sandigi ac";
        GUI.Label(GameHUD.Prompt(pt, new Rect(Screen.width / 2f - 90, Screen.height / 2f + 30, 220, 30)), pt);
    }
}
