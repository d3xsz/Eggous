using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 4.7 Vurma: F tusu (insan) / arada bir (bot). Kisa menzil, geri itme + sersemleme + elindekini dusurme, cooldown.
// Herkes vurabilir. Gorsel: parcacik patlamasi + sok halkasi, kamera sarsintisi/FOV vurusu, kirmizi ekran flasi, bot squash.
// Sabotajci kova kullaniyorsa (golun yaninda ya da su dokerken) F su icin ayrilir, vurma tetiklenmez.
[RequireComponent(typeof(CharacterController))]
public class PlayerCombat : MonoBehaviour
{
    public static readonly List<PlayerCombat> All = new List<PlayerCombat>();
    public static event System.Action<GameObject, GameObject> Hit; // (vuran, vurulan)
    public static string announce;
    public static float announceUntil;

    [Header("Vurma")]
    public float hitRange = 3f;            // govdeler arasi yatay menzil (yaricap payi ayrica eklenir)
    public float hitCooldown = 5f;
    public float minDot = 0.25f;           // onunde sayilmasi icin yatay yon benzerligi (genis tolerans)
    public float knockbackSpeed = 15f;
    public float knockbackDecay = 4f;
    public float stunSeconds = 2f;
    public float stunSpeedMultiplier = 0.35f;
    [Header("Bot (nadiren vurur)")]
    public Vector2 botHitInterval = new Vector2(15f, 35f);

    CharacterController cc;
    BaleCarrier carrier;
    PlayerController pc;
    BotController bot;
    WaterBucket wb;
    float cooldownLeft, stunLeft, botTimer; public float CooldownLeft { get { return cooldownLeft; } }
    float handsFullAt = -10f; // el en son ne zaman doluydu (yerlestirme/firlatma/item kullanimi ayni karede yumruk atmasin)
    Vector3 knock;
    bool stunApplied;

    // gorsel
    Camera cam;
    Vector3 camBase;
    float baseFov;
    float shakeT, shakeDur = 0.3f, shakeAmp;
    float fovKick, flashA, squashT;
    Vector3 origScale;
    static Material fxMat;

    // devrilme: vurulunca yumurta yere yatar, sonra kalkar
    [Header("Devrilme")]
    public float fallAngle = 85f;
    public float fallTime = 0.25f;   // yatma suresi
    public float riseTime = 0.6f;    // kalkma suresi (sersemlemenin son kismi)
    float downK, rollSign = 1f;
    Vector3 fallAxis = Vector3.right;
    Transform pivot;

    bool IsBot { get { return carrier != null && carrier.isBot; } }
    public bool IsStunned { get { return stunLeft > 0f; } }
    public bool IsAsleep { get { return sleepLeft > 0f; } }
    public bool IsDown { get { return stunLeft > riseTime; } }   // yere yatmis (ag: NetPlayer uzak kopyaya yayar)
    float sleepLeft;

    // Uyku Tuyu: devrilir, hic hareket edemez/ziplayamaz/bakamaz
    public void Sleep(float seconds, PlayerCombat by)
    {
        if (GameManager.NetMode && !enabled) { NetGame.ForwardHit(this, 2, Vector3.zero, by, seconds, 0f, ""); return; }
        ReceiveHit(Vector3.zero, by, 0f, seconds, "Uyku Tuyu ile uyutuldu");
        sleepLeft = seconds;
        if (pc != null) pc.speedMultiplier = 0f;
    }

    // Gizli Itme: sadece hafif itme, sersemleme/efekt/duyuru yok
    public void ReceivePush(Vector3 dir, float speed)
    {
        if (GameManager.NetMode && !enabled) { NetGame.ForwardHit(this, 1, dir, null, speed, 0f, ""); return; }
        knock = dir * speed;
        if (pc != null) pc.AddVertical(3f);
    }

    // Vurus 'hit-stop': isabet aninda zaman kisa sure neredeyse durur (agirlik hissi)
    static bool hitStopping;
    System.Collections.IEnumerator HitStop(float seconds)
    {
        if (hitStopping) yield break;
        hitStopping = true;
        float old = Time.timeScale;
        Time.timeScale = 0.08f;
        yield return new WaitForSecondsRealtime(seconds);
        Time.timeScale = old;
        hitStopping = false;
    }

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        carrier = GetComponent<BaleCarrier>();
        pc = GetComponent<PlayerController>();
        bot = GetComponent<BotController>();
        wb = GetComponent<WaterBucket>();
        origScale = transform.localScale;
        if (IsBot) SetupPivot();
        if (!IsBot && carrier != null && carrier.cam != null)
        {
            cam = carrier.cam;
            camBase = cam.transform.localPosition;
            baseFov = cam.fieldOfView;
        }
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); EndStun(); }
    void Start() { botTimer = Random.Range(botHitInterval.x, botHitInterval.y); }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        UpdateVisuals(dt);
        UpdateTopple(dt);
        if (HandsFull()) handsFullAt = Time.time;

        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
        {
            knock = Vector3.zero;
            stunLeft = 0f;
            EndStun();
            return;
        }

        if (cooldownLeft > 0f) cooldownLeft -= dt;
        if (sleepLeft > 0f) sleepLeft -= dt;
        if (stunLeft > 0f) { stunLeft -= dt; if (stunLeft <= 0f) EndStun(); }

        if (knock.sqrMagnitude > 0.01f)
        {
            cc.Move(new Vector3(knock.x, 0f, knock.z) * dt);
            knock = Vector3.Lerp(knock, Vector3.zero, Mathf.Min(1f, knockbackDecay * dt));
        }

        if (!IsBot)
        {
            var kb = Keyboard.current;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !HandsFull() && Time.time - handsFullAt > 0.2f && Cursor.lockState == CursorLockMode.Locked
                && !IsStunned && cooldownLeft <= 0f && !VoteSystem.IsEliminated(gameObject))
                TryHit();
        }
        else
        {
            botTimer -= dt;
            if (botTimer <= 0f)
            {
                botTimer = Random.Range(botHitInterval.x, botHitInterval.y);
                if (!IsStunned && cooldownLeft <= 0f) TryHit();
            }
        }
    }

    // Bot: govde + gozleri govdenin ortasindaki bir pivota tasir, pivotu yatirir (CharacterController dik kalir)
    void SetupPivot()
    {
        pivot = new GameObject("GovdePivot").transform;
        pivot.SetParent(transform, false);
        pivot.localPosition = new Vector3(0f, 0.9f, 0f);
        var move = new List<Transform>();
        foreach (Transform c in transform)
            if (c != pivot && c.GetComponent<Renderer>() != null) move.Add(c);
        foreach (var c in move) c.SetParent(pivot, true);
    }

    void UpdateTopple(float dt)
    {
        float target = stunLeft > riseTime ? 1f : 0f;
        float spd = target > downK ? 1f / Mathf.Max(0.01f, fallTime) : 1f / Mathf.Max(0.01f, riseTime);
        downK = Mathf.MoveTowards(downK, target, dt * spd);
        float e = Mathf.SmoothStep(0f, 1f, downK);

        if (pivot != null)
        {
            pivot.localPosition = new Vector3(0f, Mathf.Lerp(0.9f, 0.52f, e), 0f);
            pivot.rotation = Quaternion.AngleAxis(fallAngle * e, fallAxis) * transform.rotation;
        }
        else if (cam != null && e > 0.001f)
        {
            // insan: kamera alcalir ve yana yatar
            cam.transform.localPosition += Vector3.down * Mathf.Max(0f, camBase.y - 0.35f) * e;
            cam.transform.localRotation = cam.transform.localRotation * Quaternion.Euler(0f, 0f, 80f * e * rollSign);
        }
    }

    void UpdateVisuals(float dt)
    {
        if (cam != null)
        {
            if (shakeT > 0f)
            {
                shakeT -= dt;
                cam.transform.localPosition = camBase + Random.insideUnitSphere * shakeAmp * Mathf.Clamp01(shakeT / shakeDur);
            }
            else cam.transform.localPosition = camBase;
            fovKick = Mathf.MoveTowards(fovKick, 0f, dt * 6f);
            cam.fieldOfView = baseFov + 7f * fovKick;
        }
        flashA = Mathf.MoveTowards(flashA, 0f, dt * 1.8f);

        if (IsBot)
        {
            if (squashT > 0f)
            {
                squashT -= dt;
                float k = Mathf.Sin(Mathf.Clamp01(squashT / 0.35f) * Mathf.PI) * 0.35f;
                transform.localScale = new Vector3(origScale.x * (1f + k), origScale.y * (1f - k), origScale.z * (1f + k));
            }
            else if (transform.localScale != origScale) transform.localScale = origScale;
        }
    }

    void Shake(float amp, float dur) { shakeAmp = amp; shakeDur = dur; shakeT = dur; }

    // El bos mu? (balya / nesne / item / kova tasirken yumruk yok)
    bool HandsFull() { return carrier != null && (carrier.IsCarrying || carrier.HandsBusy); }

    bool WaterWantsF()
    {
        bool w = wb != null && (wb.usedFrame == Time.frameCount || wb.WantsF());
        if (w && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) Debug.Log("[Vurma] F su icin ayrildi (sabotajci), vurma tetiklenmedi");
        return w;
    }

    void TryHit()
    {
        Transform look = (carrier != null && carrier.cam != null) ? carrier.cam.transform : transform;
        Vector3 flat = new Vector3(look.forward.x, 0f, look.forward.z);
        if (flat.sqrMagnitude < 0.001f) flat = transform.forward;
        flat.Normalize();

        PlayerCombat best = null;
        float bestD = 999f, nearestD = 999f, nearestDot = 0f;
        foreach (var o in All)
        {
            if (o == this) continue;
            Vector3 to = o.transform.position - transform.position;
            Vector3 toFlat = new Vector3(to.x, 0f, to.z);
            float d = toFlat.magnitude;
            float dot = d > 0.05f ? Vector3.Dot(flat, toFlat.normalized) : 1f;
            if (d < nearestD) { nearestD = d; nearestDot = dot; }
            if (Mathf.Abs(to.y) > 1.6f) continue;
            if (d > hitRange + 0.4f) continue;
            if (d > 0.4f && dot < minDot) continue;
            if (d < bestD) { best = o; bestD = d; }
        }

        cooldownLeft = hitCooldown;
        fovKick = 1f; // savurma hissi
        Vector3 swingPos = transform.position + Vector3.up * 1.1f + flat * 1.2f;
        
        if (best != null)
        {
            Shake(0.06f, 0.15f);
            PunchSound.Impact(best.transform.position + Vector3.up * 1f);   // isabet sesi
            Debug.Log("[Vurma] " + name + " -> " + best.name + " (mesafe " + bestD.ToString("F1") + ") | vuran balya tasiyor: " + (carrier != null && carrier.IsCarrying) + ", vurulan balya tasiyor: " + (best.carrier != null && best.carrier.IsCarrying));
            best.ReceiveHit(flat, this);
        }
        else
        {
            Burst(swingPos, 8, 2.5f, 0.08f, new Color(0.9f, 0.9f, 0.9f), new Color(0.7f, 0.7f, 0.7f));
            PunchSound.Swing(swingPos);   // iskalama: sadece savurma sesi // iskalama: kucuk toz
            Debug.Log("[Vurma] iskaladi (balya tasiyor: " + (carrier != null && carrier.IsCarrying) + "). En yakin: " + (nearestD < 900f ? nearestD.ToString("F1") + " m, yon benzerligi " + nearestDot.ToString("F2") : "kimse yok"));
        }
    }

    public void ReceiveHit(Vector3 dir, PlayerCombat attacker) { ReceiveHit(dir, attacker, 1f, stunSeconds, null); }

    public void ReceiveHit(Vector3 dir, PlayerCombat attacker, float knockMult, float stun, string what)
    {
        if (GameManager.NetMode && !enabled) { NetGame.ForwardHit(this, 0, dir, attacker, knockMult, stun, what); return; }
        knock = dir * knockbackSpeed * knockMult;
        if (pc != null && knockMult > 0.01f) pc.AddVertical(5f * Mathf.Min(knockMult, 1.5f)); // havaya hafif firlama
        if (knockMult > 0.01f && (!IsBot || (attacker != null && !attacker.IsBot))) StartCoroutine(HitStop(0.07f));
        stunLeft = stun;
        if (carrier != null && carrier.IsCarrying)
        {
            try { carrier.Drop(); } // yerlestirilmis sayilmaz, dustu sayilmaz
            catch (System.Exception e) { Debug.LogError("[Vurma] balya birakilamadi: " + e.Message); }
        }
        var ot = GetComponent<ObjectThrower>();
        if (ot != null && ot.Held != null) ot.Drop();
        var its = GetComponent<ItemSystem>();
        if (its != null) its.Unequip();
        var wbk = GetComponent<WaterBucket>();
        if (wbk != null) wbk.Unequip();
        BeginStun();

        Vector3 chest = transform.position + Vector3.up * 1.0f;
        Burst(chest, 40, 7f, 0.26f, new Color(1f, 0.95f, 0.5f), new Color(1f, 0.55f, 0.15f));
        Ring(transform.position + Vector3.up * 0.25f);
        rollSign = Random.value < 0.5f ? 1f : -1f;
        if (IsBot)
        {
            fallAxis = Vector3.Cross(Vector3.up, dir); // vurulan, vurus yonune dogru (arkaya) devrilir
            if (fallAxis.sqrMagnitude < 0.01f) fallAxis = Vector3.right;
            fallAxis.Normalize();
        }
        else { Shake(0.32f, 0.45f); flashA = 1f; }

        announce = (attacker != null ? attacker.name : "?") + "  ->  " + name + (what == null ? " (vurdu!)" : " (" + what + ")");
        announceUntil = Time.time + 3f;
        if (Hit != null && attacker != null) Hit(attacker.gameObject, gameObject);
    }

    void BeginStun()
    {
        if (stunApplied) return;
        stunApplied = true;
        if (pc != null) pc.speedMultiplier = stunSpeedMultiplier;
        if (bot != null) bot.enabled = false;
    }

    void EndStun()
    {
        if (!stunApplied) return;
        stunApplied = false;
        if (pc != null) pc.speedMultiplier = 1f;
        if (bot != null) bot.enabled = true;
    }

    // ---- Efektler (kod ile uretilen parcaciklar, asset gerekmez) ----

    static ParticleSystem MakePS(Vector3 pos, float lifetime)
    {
        var go = new GameObject("VurusEfekti");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = lifetime;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var colLife = ps.colorOverLifetime;
        colLife.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colLife.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var rend = go.GetComponent<ParticleSystemRenderer>();
        if (fxMat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            fxMat = new Material(sh);
            fxMat.SetColor("_BaseColor", Color.white);
        }
        rend.sharedMaterial = fxMat;
        return ps;
    }

    static void Burst(Vector3 pos, int count, float speed, float size, Color a, Color b)
    {
        var ps = MakePS(pos, 0.5f);
        var main = ps.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.gravityModifier = 1.2f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;
        ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        ps.Play();
    }

    static void Ring(Vector3 pos)
    {
        var ps = MakePS(pos, 0.35f);
        var main = ps.main;
        main.startSpeed = 7f;
        main.startSize = 0.22f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 0.8f), new Color(1f, 0.8f, 0.4f));
        main.gravityModifier = 0f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;
        shape.radiusThickness = 0f;
        shape.rotation = new Vector3(90f, 0f, 0f);
        ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)32) });
        ps.Play();
    }

    // Gecici UI (Faz 6 da guzellestirilecek)
    void OnGUI()
    {
        if (IsBot) return;
        if (flashA > 0f)
        {
            GUI.color = new Color(1f, 0.1f, 0.05f, flashA * 0.35f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        string cd = cooldownLeft > 0f ? ("Vurma: " + cooldownLeft.ToString("F1") + " sn") : "Vurma: hazir (bos elle sol tik)";
        GUI.Label(GameHUD.Active ? Rect.zero : new Rect(Screen.width / 2f - 70, Screen.height - 36, 200, 24), cd);
        if (IsStunned) GUI.Label(GameHUD.Active ? Rect.zero : new Rect(Screen.width / 2f - 60, Screen.height / 2f - 60, 200, 24), "SERSEMLEDIN!");
        if (Time.time < announceUntil) GUI.Label(new Rect(10, 70, 500, 24), announce);
    }
}
