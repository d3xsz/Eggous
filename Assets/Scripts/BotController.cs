using UnityEngine;
using System.Collections.Generic;

// Basit bot: balya yigininden balya alir, rotadaki en ileri balyanin yanina koyar. Sira beklemez (TurnManager.enforceTurns kapali).
// Yerlestirme PlacementPreview.PlaceAt ile yapilir (BalePlaced eventi, BaleState vs. oyuncuyla ayni).
[RequireComponent(typeof(CharacterController))]
public class BotController : MonoBehaviour
{
    enum Mode { Stack, Extend }

    [Header("Hareket")]
    public float moveSpeed = 4f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;
    public float turnSpeed = 420f;

    [Header("Davranis")]
    public Vector2 thinkDelay = new Vector2(0.8f, 2.5f);
    public float pickupDistance = 3.6f;
    public float standoff = 3.2f;     // yerlestirirken siteden bu kadar uzakta dur
    public float placeReach = 4.5f;
    public float stuckTimeout = 8f;   // bu kadar sure ilerleyemezse elindekini birakip bastan dener

    [Header("Yerlestirme")]
    [Range(0f, 1f)] public float stackChance = 0.5f; // ilerideki balyanin USTUNE koyma ihtimali
    public float forwardStack = 0.35f;   // ustune koyarken ileri kayma (balya derinligi orani)
    public float forwardExtend = 1.05f;  // yanina koyarken ileri kayma
    public float lateralJitter = 0.3f;
    public float seedDistance = 8f;      // henuz balya yokken yiginin bu kadar ilerisine koyar
    public float rotationStep = 15f;
    public int maxFails = 5;

    CharacterController cc;
    BaleCarrier carrier;
    PlacementPreview placer;
    BaleSpawner pile;
    Checkpoint[] checkpoints;
    Hatchery hatchery;

    Vector3 moveDir;
    float yVel;
    float wait;
    bool jumpRequest;
    Vector3 lastCheckPos;
    float checkTimer;
    float blockedTime;

    bool hasSite;
    Mode mode;
    float lateral;
    Vector3 site;
    Quaternion siteRot;
    int fails;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        carrier = GetComponent<BaleCarrier>();
        placer = GetComponent<PlacementPreview>();
        cc.stepOffset = Mathf.Min(climbStep, cc.height + cc.radius - 0.02f);
    }

    void Start()
    {
        UpdateSafePoint();
        checkpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
        hatchery = FindFirstObjectByType<Hatchery>();
        wait = Random.Range(0.5f, 3f);
        lastCheckPos = transform.position;
    }

    void Update()
    {
        moveDir = Vector3.zero;
        // DUSME KONTROLU: bulundugu adanin yuzeyinin fallMargin altina inerse (altta bir zemin olsa bile) dogma noktasina isinla
        if (RouteOk && respawn != null && transform.position.y < islands[stage].position.y - fallMargin)
        {
            DumpHistory("DUSME-KONTROLU");
            respawn.Respawn();
            yVel = 0f;
        }
        // Isinlanma (respawn) algilandi: dusme hizini sifirla, temiz basla
        if (posInit && (transform.position - lastFramePos).sqrMagnitude > 25f)
        {
            DumpHistory("ISINLANMA");
            yVel = 0f;
            hasSite = false;
            blockedTime = 0f;
            checkTimer = 0f;
            lastCheckPos = transform.position;
            wait = 0.5f;
        }
        bool playing = GameManager.Instance == null || GameManager.Instance.IsPlaying;
        if (playing)
        {
            UpdateStage();
            if (RouteOk && stage >= islands.Length - 1)
            {
                // Kulucka adasindayiz: kuluckaya yuru
                if (carrier.IsCarrying) carrier.Drop();
                hasSite = false;
                branch = "Kulucka"; if (hatchery != null) Walk(hatchery.transform.position - transform.position);
            }
            else if (RouteOk && GetInfo().done)
            {
                // Yurunebilir bir kopru siradaki adaya ulasti: balyalarin uzerinden yuru
                if (carrier.IsCarrying) carrier.Drop();
                hasSite = false;
                branch = "Cross"; Cross(GetInfo());
            }
            else if (wait > 0f) { branch = "Bekle"; wait -= Time.deltaTime; }
            else if (!carrier.IsCarrying) { branch = "Fetch"; Fetch(); }
            else { branch = "Build"; Build(); }
        }
        TrackStuck();
        Apply();
        lastFramePos = transform.position;
        posInit = true;
        DebugTick();
    }

    // ---- Davranis ----

    void Fetch()
    {
        pile = NearestPile();
        if (pile == null) return;
        Vector3 to = pile.transform.position - transform.position;
        to.y = 0f;
        if (to.magnitude > pickupDistance) { WalkTo(pile.transform.position, false); return; }
        carrier.Pickup(pile.Take());
        hasSite = false;
        fails = 0;
        wait = Random.Range(thinkDelay.x, thinkDelay.y);
    }

    void Build()
    {
        if (!hasSite)
        {
            mode = Random.value < StackProb() ? Mode.Stack : Mode.Extend;
            lateral = Random.Range(-lateralJitter, lateralJitter);
            if (!ComputeSite())
            {
                if (mode != Mode.Extend) { Fail(); return; }
                mode = Mode.Stack;                       // yanda destek yok (bosluk): ust uste koy
                if (!ComputeSite()) { Fail(); return; }
            }
            hasSite = true;
        }
        Vector3 to = site - transform.position;
        to.y = 0f;
        float d = to.magnitude;
        if (d > placeReach - 0.5f) { WalkTo(site, true); return; }
        if (d < standoff - 1.4f) { Walk(-to); return; }

        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to, Vector3.up), turnSpeed * Time.deltaTime);

        // Yerlestirmeden hemen once yeniden hesapla (baskalari balya koymus olabilir)
        if (!ComputeSite() || !SiteFree())
        {
            if (mode != Mode.Extend) { Fail(); return; }
            mode = Mode.Stack;
            if (!ComputeSite() || !SiteFree()) { Fail(); return; }
        }
        placer.PlaceAt(site, siteRot);
        hasSite = false;
        fails = 0;
        wait = Random.Range(thinkDelay.x, thinkDelay.y);
    }

    void Fail()
    {
        hasSite = false;
        fails++;
        wait = 0.3f;
        if (fails >= maxFails)
        {
            if (carrier.IsCarrying) carrier.Drop();
            fails = 0;
            wait = 1f;
        }
    }

    void OnStuck()
    {
        if (carrier.IsCarrying) carrier.Drop();
        hasSite = false;
        fails = 0;
        blockedTime = 0f;
        wait = 1f;
    }

    // ---- Yerlestirme noktasi ----

    bool ComputeSite()
    {
        if (!carrier.IsCarrying) return false;
        Vector3 goal = GoalPos();
        Vector3 size = carrier.HeldBale.transform.localScale;
        Vector3 half = size * 0.5f;

        BaleState fr = Frontier(goal);
        Vector3 basePos;
        if (fr != null) basePos = fr.transform.position;
        else
        {
            Vector3 pp = pile != null ? pile.transform.position : transform.position;
            Vector3 dp = goal - pp; dp.y = 0f;
            if (dp.sqrMagnitude < 0.01f) dp = transform.forward;
            Vector3 ed;
            if (TryEdgePoint(pp, goal, out ed)) basePos = ed - dp.normalized * 0.6f;
            else basePos = pp + dp.normalized * seedDistance;
        }

        Vector3 dir = goal - basePos;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) { dir = transform.forward; dir.y = 0f; }
        dir.Normalize();

        float yaw = Mathf.Round(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg / rotationStep) * rotationStep;
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        float fwd = (mode == Mode.Stack ? forwardStack : forwardExtend) * size.z;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        Vector3 xz = basePos + dir * fwd + side * lateral;

        Vector3 start = new Vector3(xz.x, basePos.y + 4f, xz.z);
        if (!Physics.BoxCast(start, half * 0.98f, Vector3.down, out var hit, rot, 40f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        site = start + Vector3.down * hit.distance + Vector3.up * 0.005f;
        siteRot = rot;
        return true;
    }

    bool SiteFree()
    {
        Vector3 half = carrier.HeldBale.transform.localScale * 0.5f;
        return !Physics.CheckBox(site, half * 0.98f, siteRot, ~0, QueryTriggerInteraction.Ignore);
    }

    // ---- Ada rotasi: Baslangic -> Ada1 -> Ada2 -> Kulucka (sirayla, atlama yok) ----
    [Header("Ada rotasi")]
    public float islandReach = 11f;      // kenar aramada kullanilir
    public float corridorWidth = 8f;     // iki ada arasindaki dogruya bu kadar yakin balyalar koprunun parcasi sayilir
    public float bridgeHeightTol = 1.5f; // (eski, kullanilmiyor)
    public float finalStepTol = 1.0f;    // kopru ucu, hedef ada yuzeyinden en fazla bu kadar asagida olabilir
    public float linkExtra = 0.6f;       // iki balya arasi yurunebilir bag icin ek tolerans
    public float maxStepUp = 1.1f;       // bir balyadan digerine cikilabilecek en fazla yukseklik
    public float maxStepDown = 2.5f;
    public float heightWeight = 1.0f;    // kopru ucu secerken yukseklik eksiginin agirligi

    static readonly string[] IslandNames = { "Isla_Baslangic", "Isla_Ada1", "Isla_Ada2", "Isla_Kulucka" };
    Transform[] islands;
    float[] islandRadius;
    int stage;               // botun en son uzerinde durdugu ada (0 = baslangic)
    bool safeSet;
    PlayerRespawn respawn;
    Vector3 lastFramePos;
    bool posInit;

    class BridgeInfo
    {
        public float time = -999f;
        public BaleState frontier;      // kopruyu uzatmak icin en iyi (ulasilabilir) balya
        public float frontierTop;
        public bool done;               // yurunerek siradaki adaya ulasilabiliyor mu
        public List<Vector3> path = new List<Vector3>();
        public List<Vector3> frontierPath = new List<Vector3>();   // adadan kopru ucuna giden zincir
    }
    static readonly Dictionary<int, BridgeInfo> infos = new Dictionary<int, BridgeInfo>();

    void EnsureIslands()
    {
        if (islands == null) { islands = new Transform[IslandNames.Length]; islandRadius = new float[IslandNames.Length]; }
        for (int i = 0; i < islands.Length; i++)
        {
            if (islands[i] != null) continue;
            var g = GameObject.Find(IslandNames[i]);
            if (g == null) continue;
            islands[i] = g.transform;
            float r = 10f;
            var rd = g.GetComponent<Renderer>();
            if (rd != null) r = Mathf.Max(rd.bounds.extents.x, rd.bounds.extents.z);
            islandRadius[i] = r;
        }
    }

    bool RouteOk
    {
        get
        {
            if (islands == null) return false;
            for (int i = 0; i < islands.Length; i++) if (islands[i] == null) return false;
            return true;
        }
    }

    bool HasNext { get { return RouteOk && stage + 1 < islands.Length; } }
    Transform NextIsland { get { return HasNext ? islands[stage + 1] : null; } }

    void UpdateStage()
    {
        EnsureIslands();
        if (!RouteOk) return;
        float y = transform.position.y;
        for (int i = islands.Length - 1; i >= 0; i--)
        {
            Vector3 d = transform.position - islands[i].position; d.y = 0f;
            if (d.magnitude <= islandRadius[i] + 1.5f && Mathf.Abs(y - islands[i].position.y) <= 2.5f)
            {
                if (stage != i || !safeSet) { stage = i; hasSite = false; safeSet = true; UpdateSafePoint(); }
                return;
            }
        }
    }

    // Dusunce dogulacak nokta: sabit (inspector'dan degistirilebilir)
    [Header("Dogma noktasi")]
    public Vector3 respawnPoint = new Vector3(3f, 0.1f, -1f);

    void UpdateSafePoint()
    {
        if (respawn == null) respawn = GetComponent<PlayerRespawn>();
        if (respawn != null) respawn.SetRespawnPoint(respawnPoint);
    }

    static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        p.y = 0f; a.y = 0f; b.y = 0f;
        Vector3 ab = b - a;
        float l2 = ab.sqrMagnitude;
        float t = l2 < 0.0001f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2);
        return Vector3.Distance(p, a + ab * t);
    }

    BridgeInfo GetInfo()
    {
        BridgeInfo info;
        if (!infos.TryGetValue(stage, out info)) { info = new BridgeInfo(); infos[stage] = info; }
        if (info.time > Time.time || Time.time - info.time > 0.3f) { info.time = Time.time; ComputeBridge(info); }
        return info;
    }

    // Koprunun GERCEK durumu: adadan baslayip birbirine yurunebilir sekilde bagli balyalar (BFS).
    // Baglantisiz / yanlis yere konmus balyalar yok sayilir. Ada'ya ulasan en kisa zincir = yurunecek yol.
    void ComputeBridge(BridgeInfo info)
    {
        info.frontier = null; info.done = false; info.frontierTop = 0f;
        info.path = new List<Vector3>();
        info.frontierPath = new List<Vector3>();
        if (!HasNext) return;
        Vector3 A = islands[stage].position, B = islands[stage + 1].position;
        float rA = islandRadius[stage] + 1.5f, rB = islandRadius[stage + 1] + 1.5f;
        float lowY = A.y - 1.5f, highY = B.y + 3f;

        var nodes = new List<BaleState>();
        var pos = new List<Vector3>();
        var half = new List<float>();
        foreach (var bl in FindObjectsByType<BaleState>(FindObjectsSortMode.None))
        {
            if (!bl.placed || bl.fell || !bl.frozen) continue;
            var col = bl.GetComponent<Collider>();
            if (col == null) continue;
            Bounds bd = col.bounds;
            Vector3 top = new Vector3(bd.center.x, bd.max.y, bd.center.z);
            if (top.y < lowY || top.y > highY + 1f || DistToSegment(top, A, B) > corridorWidth) continue;
            nodes.Add(bl); pos.Add(top); half.Add(Mathf.Max(bd.extents.x, bd.extents.z));
        }
        int n = nodes.Count;
        if (n == 0) return;

        int[] parent = new int[n];
        bool[] seen = new bool[n];
        var q = new Queue<int>();
        for (int i = 0; i < n; i++)
        {
            parent[i] = -1;
            Vector3 d = pos[i] - A; d.y = 0f;
            if (d.magnitude <= rA && Mathf.Abs(pos[i].y - A.y) <= 1.6f) { seen[i] = true; q.Enqueue(i); }   // adanin uzerinde duran balya
        }

        int target = -1;
        while (q.Count > 0)
        {
            int i = q.Dequeue();
            Vector3 dd = pos[i] - B; dd.y = 0f;
            if (dd.magnitude <= rB && pos[i].y >= B.y - finalStepTol) { target = i; break; }
            for (int j = 0; j < n; j++)
            {
                if (seen[j]) continue;
                Vector3 dj = pos[j] - pos[i];
                float dy = dj.y; dj.y = 0f;
                if (dj.magnitude > half[i] + half[j] + linkExtra) continue;
                if (dy > maxStepUp || dy < -maxStepDown) continue;
                seen[j] = true; parent[j] = i; q.Enqueue(j);
            }
        }

        if (target >= 0)
        {
            info.done = true;
            info.frontier = nodes[target];
            info.frontierTop = pos[target].y;
            var rev = new List<Vector3>();
            for (int k = target; k >= 0; k = parent[k]) rev.Add(pos[k]);
            rev.Reverse();
            info.path = rev;
            return;
        }

        // Ulasilabilir balyalar arasindan en iyi uc: hedefe yatay yakinlik + yukseklik eksigi
        float bestC = float.MaxValue;
        int bi = -1;
        for (int i = 0; i < n; i++)
        {
            if (!seen[i]) continue;
            Vector3 f = pos[i] - B; f.y = 0f;
            float c = f.magnitude + heightWeight * Mathf.Max(0f, B.y - pos[i].y);
            if (c < bestC) { bestC = c; bi = i; }
        }
        if (bi >= 0)
        {
            info.frontier = nodes[bi]; info.frontierTop = pos[bi].y;
            var rp = new List<Vector3>();
            for (int k = bi; k >= 0; k = parent[k]) rp.Add(pos[k]);
            rp.Reverse();
            info.frontierPath = rp;
        }
    }

    BaleState Frontier(Vector3 goal) { return GetInfo().frontier; }

    // Kopru yoluna sadik kal: balya balya yuru, son balyadan sonra adaya gec
    void Cross(BridgeInfo info)
    {
        var path = info.path;
        Vector3 me = transform.position;
        if (path.Count == 0) { Walk(NextIsland.position - me); return; }
        int n = 0;
        float bd = float.MaxValue;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 d = path[i] - me;
            float dist = new Vector2(d.x, d.z).magnitude + Mathf.Abs(d.y) * 0.5f;
            if (dist < bd) { bd = dist; n = i; }
        }
        Vector3 hd = path[n] - me; hd.y = 0f;
        Vector3 wp;
        if (hd.magnitude < 0.9f) wp = (n + 1 < path.Count) ? path[n + 1] : NextIsland.position;
        else wp = path[n];
        Walk(wp - me);
    }

    // Ihtiyaca gore ust uste mi (yuksel) yoksa yan yana mi (uzat) koyacagini secer: kalan yukseklik / kalan mesafe
    float StackProb()
    {
        var info = GetInfo();
        if (info.frontier == null || !HasNext || carrier.HeldBale == null) return 0f;
        Vector3 size = carrier.HeldBale.transform.localScale;
        Vector3 goal = NextIsland.position;
        Vector3 f = info.frontier.transform.position - goal; f.y = 0f;
        float remH = Mathf.Max(0.5f, f.magnitude - islandRadius[stage + 1]);
        float deficit = Mathf.Max(0f, (goal.y - finalStepTol * 0.5f) - info.frontierTop);
        float stepsH = remH / Mathf.Max(0.1f, size.z * forwardExtend);
        float stepsV = deficit / Mathf.Max(0.1f, size.y);
        return Mathf.Clamp01(stepsV / (stepsV + stepsH + 0.001f));
    }

    // Siradaki hedef: bulundugu adanin bir sonraki adasi; son adadaysa kulucka
    Vector3 GoalPos()
    {
        EnsureIslands();
        if (HasNext) return NextIsland.position;
        if (hatchery != null) return hatchery.transform.position;
        return transform.position + transform.forward * 20f;
    }

    BaleSpawner NearestPile() { return NearestPileTo(transform.position); }

    BaleSpawner NearestPileTo(Vector3 from)
    {
        BaleSpawner best = null;
        float bestD = float.MaxValue;
        foreach (var s in FindObjectsByType<BaleSpawner>(FindObjectsSortMode.None))
        {
            float d = (s.transform.position - from).sqrMagnitude;
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    // ---- Koprunun baslangici: bulundugu adanin siradaki adaya bakan UCU ----
    public float edgeSearchMax = 60f;
    int edgeStage = -1;
    Vector3 edgePoint;
    bool edgeFound;

    bool TryEdgePoint(Vector3 from, Vector3 goal, out Vector3 ed)
    {
        if (edgeStage == stage) { ed = edgePoint; return edgeFound; }
        edgeStage = stage; edgeFound = false; edgePoint = from;
        float top = RouteOk ? islands[stage].position.y : from.y;
        Vector3 dir = goal - from; dir.y = 0f;
        float total = dir.magnitude;
        if (total < 0.5f) { ed = edgePoint; return false; }
        dir /= total;
        float last = 0f; int miss = 0;
        float maxD = Mathf.Min(edgeSearchMax, Mathf.Max(0f, total - islandReach * 0.5f));
        for (float d = 0f; d <= maxD; d += 0.5f)
        {
            if (HasGround(from + dir * d, top)) { last = d; miss = 0; }
            else if (++miss >= 4) break;   // 2 m bosluk = ada bitti
        }
        edgePoint = from + dir * last; edgePoint.y = top;
        edgeFound = true; ed = edgePoint;
        return true;
    }

    bool HasGround(Vector3 p, float top)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, top + 3f, p.z), Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<BaleState>() != null) continue;   // balyalari zemin sayma
            if (Mathf.Abs(h.point.y - top) <= 0.7f) return true;
        }
        return false;
    }

    // ---- Guvenli yurume: kopruyse balyalarin uzerinden, adadaysa duz; ucurumdan dusme ----
    [Header("Guvenlik")]
    public float fallMargin = 5f;         // adanin yuzeyinin bu kadar altina inerse dustu sayilir
    public float climbStep = 1.3f;       // zipalmadan cikilabilen basamak yuksekligi (balya yuksekligi kadar olmali)
    public float edgeLookahead = 0.9f;   // bu kadar onunde zemin yoksa dur
    public float edgeDropLimit = 2.2f;   // ayak seviyesinin bu kadar altina kadar zemin varsa gecilebilir

    // Ayagin onunde/altinda basilabilir zemin var mi? Isin ayak seviyesinin 1.6 ustunden atilir
    // (balya basamagi ayak seviyesinden yuksekse isin onun ICINDEN baslamasin diye), cok yuksek engeller zemin sayilmaz.
    bool GroundAt(Vector3 xz, float feetY, float down)
    {
        var hits = Physics.RaycastAll(new Vector3(xz.x, feetY + 1.6f, xz.z), Vector3.down, 1.6f + down, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform)) continue;   // kendisi / elindeki balya zemin degil
            if (hits[i].point.y > feetY + climbStep + 0.1f) continue;        // tirmanilamayacak kadar yuksek: zemin degil
            return true;
        }
        return false;
    }

    static int NearestIdx(List<Vector3> path, Vector3 p)
    {
        int n = 0;
        float bd = float.MaxValue;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 d = path[i] - p;
            float dist = new Vector2(d.x, d.z).magnitude + Mathf.Abs(d.y) * 0.5f;
            if (dist < bd) { bd = dist; n = i; }
        }
        return n;
    }

    // forward=true: kopru ucuna dogru (balya koymak icin), false: adadaki hedefe (yigin) geri
    void WalkTo(Vector3 target, bool forward)
    {
        Vector3 me = transform.position;
        if (!HasNext) { Walk(target - me); return; }
        var info = GetInfo();
        var path = info.done ? info.path : info.frontierPath;
        if (path == null || path.Count == 0) { Walk(target - me); return; }

        int n = NearestIdx(path, me);
        Vector3 hd = path[n] - me; hd.y = 0f;
        bool onBridge = me.y > islands[stage].position.y + 0.5f && hd.magnitude < 2.2f;
        Vector3 wp;
        if (forward)
        {
            if (hd.magnitude < 0.9f) wp = (n + 1 < path.Count) ? path[n + 1] : target;
            else wp = path[n];
        }
        else
        {
            if (!onBridge) { Walk(target - me); return; }
            if (hd.magnitude < 0.9f) wp = (n > 0) ? path[n - 1] : target;
            else wp = path[n];
        }
        Walk(wp - me);
    }

    // ---- Teshis (konsola yazar): bot duserse son ~6 sn'lik durumu, 8 sn'den fazla yerinde dururse anlik durumu ----
    [Header("Teshis")]
    public bool debugLog = true;
    string branch = "";
    readonly List<string> hist = new List<string>();
    float histAt, idleSince, idleLogAt, lastDumpAt = -10f;
    Vector3 idlePos;

    string Sample()
    {
        Vector3 p = transform.position;
        string s = "t=" + Time.time.ToString("F1") + " p=(" + p.x.ToString("F1") + "," + p.y.ToString("F1") + "," + p.z.ToString("F1") + ") " + branch
            + " stg=" + stage + " mv=" + (moveDir.sqrMagnitude > 0.01f ? "evet" : "yok") + " gr=" + cc.isGrounded + " yv=" + yVel.ToString("F1")
            + " tasir=" + carrier.IsCarrying + " fail=" + fails + " wait=" + wait.ToString("F1") + " hasSite=" + hasSite;
        if (hasSite) { Vector3 d = site - p; d.y = 0f; s += " siteD=" + d.magnitude.ToString("F1") + " mode=" + mode; }
        if (HasNext) { var info = GetInfo(); s += " fr=" + (info.frontier != null) + " done=" + info.done; }
        return s;
    }

    void DebugTick()
    {
        if (!debugLog) return;
        if (Time.time - histAt >= 0.25f) { histAt = Time.time; hist.Add(Sample()); if (hist.Count > 48) hist.RemoveAt(0); }
        if ((transform.position - idlePos).sqrMagnitude > 0.25f) { idlePos = transform.position; idleSince = Time.time; }
        else if (Time.time - idleSince > 8f && Time.time - idleLogAt > 8f) { idleLogAt = Time.time; Debug.Log("[BOT-DURUYOR] " + name + " " + Sample()); }
    }

    void DumpHistory(string why)
    {
        if (!debugLog || Time.time - lastDumpAt < 1f) return;
        lastDumpAt = Time.time;
        var sbd = new System.Text.StringBuilder("[BOT-DUSTU] " + name + " (" + why + ") son anlar:\n");
        for (int i = Mathf.Max(0, hist.Count - 24); i < hist.Count; i++) sbd.AppendLine(hist[i]);
        Debug.Log(sbd.ToString());
    }

    // ---- Hareket ----

    void Walk(Vector3 dir)
    {
        dir.y = 0f;
        moveDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
    }

    void TrackStuck()
    {
        if (moveDir.sqrMagnitude < 0.01f)
        {
            blockedTime = 0f;
            checkTimer = 0f;
            lastCheckPos = transform.position;
            return;
        }
        checkTimer += Time.deltaTime;
        if (checkTimer < 0.5f) return;
        Vector3 d = transform.position - lastCheckPos;
        d.y = 0f;
        if (d.magnitude < 0.15f) { blockedTime += checkTimer; jumpRequest = true; }
        else blockedTime = 0f;
        lastCheckPos = transform.position;
        checkTimer = 0f;
        if (blockedTime >= stuckTimeout) OnStuck();
    }

    void Apply()
    {
        // UCURUM KORUMASI: onunde zemin yoksa (yatayda edgeLookahead ileride, edgeDropLimit'ten derin) yuruyup/ziplayip dusme
        if (yVel <= 0.5f && moveDir.sqrMagnitude > 0.01f)
        {
            float feetY = cc.bounds.min.y;
            if (!GroundAt(transform.position + moveDir * edgeLookahead, feetY, edgeDropLimit)) { moveDir = Vector3.zero; jumpRequest = false; }
            else if (jumpRequest && !GroundAt(transform.position + moveDir * 1.6f, feetY, edgeDropLimit + 1f)) jumpRequest = false;
        }
        float facing = 0f;
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            facing = Mathf.Clamp01(Vector3.Dot(transform.forward, moveDir));
        }
        if (cc.isGrounded && yVel < 0f) yVel = -2f;
        if (cc.isGrounded && jumpRequest) { yVel = Mathf.Sqrt(jumpHeight * -2f * gravity); jumpRequest = false; }
        yVel += gravity * Time.deltaTime;
        Vector3 v = transform.forward * (moveSpeed * facing) + Vector3.up * yVel;
        cc.Move(v * Time.deltaTime);
    }
}
