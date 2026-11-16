using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

// Oylama + eleme (4.4). V ile baslar (sure durur), insan bir kisi secer, botlar oy verir, en cok oy alan elenir.
// Palyaco elenirse BAGIMSIZ kazanir ama OYUN DEVAM EDER (md 4d). Sabotajci elenirse sabotaj biter (flag).
public class VoteSystem : MonoBehaviour
{
    public static readonly HashSet<GameObject> Eliminated = new HashSet<GameObject>();
    public static bool IsEliminated(GameObject g) { return Eliminated.Contains(g); }
    public static event System.Action<GameObject, Role> PlayerEliminated;

    public float cooldownSeconds = 30f;
    public float resultSeconds = 4f;
    public bool clownWon;
    public bool saboteurCaught;

    GameObject human;
    readonly List<GameObject> candidates = new List<GameObject>();
    bool voting, resolved;
    float nextAllowed, resultUntil;
    string resultText = "";
    public bool IsVoting { get { return voting; } }
    public GameObject CallerObject { get { return callerObj; } }
    public float MeetingLeft { get { return Mathf.Max(0f, meetingEndsAt - Time.time); } }
    public bool IsResolved { get { return resolved; } }
    public string ResultText { get { return resultText; } }
    public System.Collections.Generic.List<GameObject> CandidateList { get { return candidates; } }
    public GameObject HumanObject { get { return human; } }
    public void CastVote(GameObject c) { Vote(c); }

    void Start()
    {
        var pc = PlayerController.LocalOrFind();
        human = pc != null ? pc.gameObject : null;
    }

    void Update()
    {
        // DUZELTME: ag modunda 'human' HER ZAMAN bu bilgisayarin kendi oyuncusu olmali. LocalOrFind, yerel oyuncu henuz dogmamissa
        // baskasinin (host'un) oyuncusunu dondurup orada kalabiliyordu -> iki kisi ayni koltuga oturuyordu.
        if (GameManager.NetMode)
        {
            var loc = PlayerController.Local;
            if (loc != null && human != loc.gameObject) human = loc.gameObject;
        }
        else if (human == null) { var pcL = PlayerController.LocalOrFind(); if (pcL != null) human = pcL.gameObject; }
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (!voting)
        {
            var kb = Keyboard.current;
            if (kb == null) return; // oylama artik VoteStation (alan + E) ile baslar
        }
        else if (resolved && Time.time >= resultUntil) End();
        else if (!resolved && inMeeting) MeetingInput();
    }

    public bool CanStart()
    {
        var gm = GameManager.Instance;
        return gm != null && gm.IsPlaying && !voting && Time.time >= nextAllowed && human != null && !IsEliminated(human) && !callUsed.Contains(human);
    }

    public void Begin() { Begin(human); }
    public void Begin(GameObject caller)
    {
        if (GameManager.NetMode) { if (NetGame.Instance != null && CanStart()) NetGame.Instance.RequestMeeting(); return; }   // ag: sunucu baslatir
        candidates.Clear();
        foreach (var c in FindObjectsByType<BaleCarrier>(FindObjectsSortMode.None))
            if (!IsEliminated(c.gameObject)) candidates.Add(c.gameObject);
        if (candidates.Count < 3) return;
        voting = true; resolved = false; meetingFailed = false; target = null; lastTally.Clear();
        callerObj = caller; if (caller != null) callUsed.Add(caller);
        var bell = FindFirstObjectByType<MeetingBell>(); if (bell != null) bell.Ring();
        if (caller == human && bell != null && human != null) { var db = bell.transform.position - human.transform.position; db.y = 0f; if (db.sqrMagnitude > 0.01f) human.transform.rotation = Quaternion.LookRotation(db); }
        GameManager.Instance.StartVote();
        StartCoroutine(EnterMeeting());
        var pc = human.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ---- Ag (asama 3): toplanti sunucudan baslar/biter ----
    public void NetBegin(GameObject caller, List<GameObject> cands, float secs)
    {
        if (voting || human == null) return;
        candidates.Clear();
        foreach (var c in cands) if (c != null) candidates.Add(c);
        meetingSeconds = secs;
        bool ghost = !candidates.Contains(human);
        voting = true; resolved = false; meetingFailed = false; target = null; voted = false; lastTally.Clear();
        callerObj = caller; if (caller != null) callUsed.Add(caller);
        var bell = FindFirstObjectByType<MeetingBell>(); if (bell != null) bell.Ring();
        GameManager.Instance.StartVote();
        if (ghost) return;   // elenmis oyuncu toplantiya girmez, sadece sonucu izler
        if (caller == human && bell != null) { var db = bell.transform.position - human.transform.position; db.y = 0f; if (db.sqrMagnitude > 0.01f) human.transform.rotation = Quaternion.LookRotation(db); }
        StartCoroutine(EnterMeeting());
        var pc = human.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void NetResult(GameObject elim, Role role, Dictionary<GameObject, int> tally)
    {
        if (!voting) return;
        lastTally.Clear();
        foreach (var kv in tally) lastTally[kv.Key] = kv.Value;
        if (elim == null) resultText = "Beraberlik: kimse elenmedi.";
        else Eliminate(elim, role);
        resolved = true;
        resultUntil = Time.time + resultSeconds;
    }

    // Ag: elenen uzak oyuncuyu SetActive(false) yapmak yerine gorunmez/carpismaz yap (NetworkObject bozulmasin)
    void HideBody(GameObject g)
    {
        if (!GameManager.NetMode) { g.SetActive(false); return; }
        foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (var c in g.GetComponentsInChildren<Collider>(true)) c.enabled = false;
    }


    void End()
    {
        voting = false; resolved = false;
        nextAllowed = Time.time + cooldownSeconds;
        var pc = human != null ? human.GetComponent<PlayerController>() : null;
        if (pc != null) pc.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        LeaveMeeting();
        GameManager.Instance.EndVote();
    }

    void Vote(GameObject humanChoice)
    {
        if (GameManager.NetMode)   // ag: oyu sunucuya gonder, sonuc sunucudan gelir
        {
            if (voted) return;
            voted = true;
            if (NetGame.Instance != null) NetGame.Instance.SubmitVote(humanChoice);
            return;
        }
        lastTally.Clear(); var tally = lastTally;
        if (humanChoice != null) tally[humanChoice] = 1;
        foreach (var bot in candidates)
        {
            if (bot == human) continue;
            bool saboteur = RoleManager.GetRole(bot) == Role.Sabotajci;
            var options = new List<GameObject>();
            foreach (var c in candidates)
            {
                if (c == bot) continue;
                if (saboteur && RoleManager.GetRole(c) == Role.Sabotajci) continue;
                options.Add(c);
            }
            if (options.Count == 0) continue;
            var pick = options[Random.Range(0, options.Count)];
            int n; tally.TryGetValue(pick, out n); tally[pick] = n + 1;
        }
        GameObject best = null; int bestN = 0; bool tie = false;
        foreach (var kv in tally)
        {
            if (kv.Value > bestN) { best = kv.Key; bestN = kv.Value; tie = false; }
            else if (kv.Value == bestN) tie = true;
        }
        if (best == null || tie) resultText = "Beraberlik: kimse elenmedi.";
        else Eliminate(best);
        resolved = true;
        resultUntil = Time.time + resultSeconds;
    }

    void Eliminate(GameObject p) { Eliminate(p, RoleManager.GetRole(p)); }
    void Eliminate(GameObject p, Role role)
    {
        Eliminated.Add(p);
        if (TurnManager.Instance != null) TurnManager.Instance.Unregister(p);
        var carrier = p.GetComponent<BaleCarrier>();
        if (carrier != null) { if (carrier.IsCarrying) carrier.Drop(); carrier.enabled = false; }
        var placer = p.GetComponent<PlacementPreview>();
        if (placer != null) placer.enabled = false;
        if (p != human) pendingHide.Add(p); // bot yok olur; insan yuruyebilir (basit hayalet), balya koyamaz

        string who = (p == human) ? "Sen" : p.name;
        if (role == Role.Palyaco) { clownWon = true; resultText = who + " elendi: Omlet (Palyaco) KAZANDI! Oyun digerleri icin devam ediyor."; }
        else if (role == Role.Sabotajci) { saboteurCaught = true; resultText = who + " elendi: Sabotajci yakalandi, sabotaj bitti!"; }
        else resultText = who + " elendi (" + RoleManager.DisplayName(role) + "): yanlis kisi, sabotajci avantajli.";
        resultText = (p == human) ? "Sen elendin." : p.name + " elendi.";   // rol aciklanmaz
        if (PlayerEliminated != null) PlayerEliminated(p, role);
    }

    // ---- Toplanti sahnesi (ayri sahne, additive yuklenir) ----
    public string meetingSceneName = "Toplanti";
    public float seatBackOffset = 1.1f;
    class SavedPose { public GameObject go; public Vector3 pos; public Quaternion rot; public Behaviour agent; public Behaviour bot; }
    readonly List<SavedPose> saved = new List<SavedPose>();
    Quaternion savedCamRot;
    Camera humanCam;
    Scene meetingScene;

    IEnumerator EnterMeeting()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, bellDelay - 0.5f));
        ScreenFader.FadeOut(0.5f);
        yield return new WaitForSecondsRealtime(0.5f);
        if (!voting) { ScreenFader.FadeIn(0.5f); yield break; }
        if (!Application.CanStreamedLevelBeLoaded(meetingSceneName)) { Debug.LogWarning("Toplanti sahnesi Build Settings'te yok"); ScreenFader.FadeIn(0.5f); yield break; }
        var op = SceneManager.LoadSceneAsync(meetingSceneName, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;
        meetingScene = SceneManager.GetSceneByName(meetingSceneName);
        if (!voting) { SceneManager.UnloadSceneAsync(meetingScene); ScreenFader.FadeIn(0.5f); yield break; }
        Transform seats = null;
        foreach (var r in meetingScene.GetRootGameObjects()) { var s = r.transform.Find("Koltuklar"); if (s != null) seats = s; }
        if (seats == null) { meetingFailed = true; Debug.LogWarning("Toplanti sahnesinde Koltuklar yok"); ScreenFader.FadeIn(0.5f); yield break; }
        saved.Clear();
        inMeeting = true; meetingEndsAt = Time.time + meetingSeconds; int i = 0;
        foreach (var c in candidates)
        {
            if (c == null) continue;
            var seat = seats.Find("Koltuk_" + (i + 1));
            if (seat == null) break;
            i++;
            if (GameManager.NetMode && c != human) continue;   // ag: digerleri kendi sahibinde oturur (NetworkTransform ile gelir)
            var bcar = c.GetComponent<BaleCarrier>(); if (bcar != null && bcar.IsCarrying) bcar.Drop();   // balyayla toplantiya gelme
            var sp = new SavedPose { go = c, pos = c.transform.position, rot = c.transform.rotation };
            sp.agent = c.GetComponent("NavMeshAgent") as Behaviour;
            sp.bot = c.GetComponent<BotController>();
            saved.Add(sp);
            Teleport(sp, SeatPos(seat), seat.rotation);
        }
        StartCoroutine(MeetingDebug());
        if (human != null)
        {
            humanCam = human.GetComponentInChildren<Camera>();
            if (humanCam != null) { savedCamRot = humanCam.transform.localRotation; humanCam.transform.localRotation = Quaternion.Euler(12f, 0f, 0f); }
            seatYaw = human.transform.eulerAngles.y; lookYaw = 0f; lookPitch = 12f;
            ScreenFader.FadeIn(0.8f);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
    }

    // TESHIS: toplantiya girdikten 3 sn sonra, bu bilgisayarin gordugu tum oyuncularin konumu / gorunurlugu
    IEnumerator MeetingDebug()
    {
        yield return new WaitForSecondsRealtime(3f);
        var sbd = new System.Text.StringBuilder("[Toplanti-Teshis] ben=" + (human != null ? human.name + " " + human.transform.position.ToString("F1") : "YOK") + " aday=" + candidates.Count + " toplantidayim=" + inMeeting + "\n");
        foreach (var np in FindObjectsByType<NetPlayer>(FindObjectsSortMode.None))
        {
            int on = 0, all = 0;
            foreach (var r in np.GetComponentsInChildren<Renderer>(true)) { all++; if (r.enabled && r.gameObject.activeInHierarchy) on++; }
            sbd.AppendLine("  " + np.name + " sahip=" + np.IsOwner + " pos=" + np.transform.position.ToString("F1") + " aktif=" + np.gameObject.activeInHierarchy + " renderer=" + on + "/" + all);
        }
        Debug.Log(sbd.ToString());
    }

    void Teleport(SavedPose sp, Vector3 pos, Quaternion rot)
    {
        var netp = sp.go.GetComponent<NetPlayer>();
        if (GameManager.NetMode && netp != null) { netp.TeleportTo(pos, Quaternion.Euler(0f, rot.eulerAngles.y, 0f)); return; }
        var cc = sp.go.GetComponent<CharacterController>();
        var rb = sp.go.GetComponent<Rigidbody>();
        if (sp.agent != null) sp.agent.enabled = false;
        if (sp.bot != null) sp.bot.enabled = false;
        if (cc != null) cc.enabled = false;
        sp.go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rot.eulerAngles.y, 0f));
        if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        if (cc != null) cc.enabled = true;
    }

    void LeaveMeeting()
    {
        foreach (var sp in saved)
        {
            if (sp.go == null || !sp.go.activeInHierarchy) continue;
            Teleport(sp, sp.pos, sp.rot);
            if (sp.agent != null) sp.agent.enabled = true;
            if (sp.bot != null && !IsEliminated(sp.go)) sp.bot.enabled = true;
        }
        saved.Clear();
        foreach (var hid in pendingHide) if (hid != null) HideBody(hid);
        pendingHide.Clear(); inMeeting = false; target = null; leaveFadeStarted = false; ScreenFader.FadeIn(0.8f);
        if (humanCam != null) humanCam.transform.localRotation = savedCamRot;
        if (meetingScene.IsValid() && meetingScene.isLoaded) SceneManager.UnloadSceneAsync(meetingScene);
    }

    // ---- Toplanti: koltuktan bakarak oy verme ----
        // ---- Toplanti cagirma hakki (kisi basi 1) ----
    public float botCallMinTime = 120f;
    public float botCallChancePerSecond = 0.004f;
    readonly HashSet<GameObject> callUsed = new HashSet<GameObject>();
    GameObject callerObj;
    float nextBotRoll;
    public bool HumanUsedCall { get { return human != null && callUsed.Contains(human); } }

    bool leaveFadeStarted;
    void LateUpdate()
    {
        if (!voting) BotCallRoll();
        else if (resolved && inMeeting && !leaveFadeStarted && Time.time >= resultUntil - 0.55f) { leaveFadeStarted = true; ScreenFader.FadeOut(0.5f); }   // cikmadan once yumusakca karar
    }

    void BotCallRoll()
    {
        if (Time.time < nextBotRoll) return;
        nextBotRoll = Time.time + 1f;
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsPlaying || Time.time < nextAllowed || Time.timeSinceLevelLoad < botCallMinTime) return;
        if (human == null || IsEliminated(human)) return;
        foreach (var b in FindObjectsByType<BotController>(FindObjectsSortMode.None))
        {
            var go = b.gameObject;
            if (!go.activeInHierarchy || IsEliminated(go) || callUsed.Contains(go)) continue;
            if (Random.value < botCallChancePerSecond) { Begin(go); return; }
        }
    }

    public float seatHeight = 0.38f;   // balya bulunamazsa
    public float seatSink = 0.12f;      // ayaklarin balyaya gomulme miktari
    Vector3 SeatPos(Transform seat)
    {
        float top = 0f; bool any = false;
        foreach (var r in seat.GetComponentsInChildren<Renderer>()) { top = any ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y; any = true; }
        Vector3 p = seat.position;
        p.y = any ? top - seatSink : p.y + seatHeight;
        return p;
    }   // balya ustunde oturma yuksekligi (ayaklar balyaya hafif gomulur)
    public float bellDelay = 2.5f;      // zil calarken toplantiya gecmeden bekleme
    public float meetingSeconds = 45f;  // oylama suresi
    float meetingEndsAt;
    public float lookSensitivity = 0.12f;
    public float maxLookYaw = 80f;
    bool inMeeting, meetingFailed;
    bool voted;   // ag: oyumu gonderdim mi
    float seatYaw, lookYaw, lookPitch;
    GameObject target;
    readonly Dictionary<GameObject, int> lastTally = new Dictionary<GameObject, int>();
    readonly List<GameObject> pendingHide = new List<GameObject>();
    public bool InMeeting { get { return inMeeting; } }
    public bool UseMeetingUI { get { return !meetingFailed && Application.CanStreamedLevelBeLoaded(meetingSceneName); } }
    public int VotesFor(GameObject g) { int n; return lastTally.TryGetValue(g, out n) ? n : 0; }

    void MeetingInput()
    {
        var mouse = Mouse.current; var kb = Keyboard.current;
        if (Time.time >= meetingEndsAt) { Vote(null); return; }   // sure doldu: oy kullanilmadi sayilir
        if (mouse == null || kb == null || human == null) return;
        Vector2 d = mouse.delta.ReadValue() * lookSensitivity;
        lookYaw = Mathf.Clamp(lookYaw + d.x, -maxLookYaw, maxLookYaw);
        lookPitch = Mathf.Clamp(lookPitch - d.y, -20f, 40f);
        human.transform.rotation = Quaternion.Euler(0f, seatYaw + lookYaw, 0f);
        if (humanCam != null) humanCam.transform.localRotation = Quaternion.Euler(lookPitch, 0f, 0f);

        var camT = humanCam != null ? humanCam.transform : human.transform;
        target = null;
        float best = Mathf.Cos(8f * Mathf.Deg2Rad);
        foreach (var c in candidates)
        {
            if (c == null || c == human || !c.activeInHierarchy) continue;
            Vector3 dir = (c.transform.position + Vector3.up * 0.7f) - camT.position;
            float dot = Vector3.Dot(camT.forward, dir.normalized);
            if (dot > best) { best = dot; target = c; }
        }
        if (kb.eKey.wasPressedThisFrame && target != null) Vote(target);
        else if (kb.qKey.wasPressedThisFrame) Vote(null);
    }

    GUIStyle sName, sBig, sTitle, sHint;
    void Styles()
    {
        if (sName != null) return;
        sName = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold };
        sBig = new GUIStyle(sName) { fontSize = 34 };
        sTitle = new GUIStyle(sName) { fontSize = 40 };
        sHint = new GUIStyle(sName) { fontSize = 18, fontStyle = FontStyle.Normal };
    }

    void Txt(Rect r, string s, GUIStyle st, Color col)
    {
        var o = st.normal.textColor;
        st.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), s, st);
        st.normal.textColor = col;
        GUI.Label(r, s, st);
        st.normal.textColor = o;
    }

    void OnGUI()
    {
        if (!voting || !inMeeting || ScreenFader.Alpha > 0.3f) return;   // gecis sirasinda IMGUI yazilari kapali
        Styles();
        var cam = humanCam != null ? humanCam : Camera.main;
        if (cam == null) return;
        var amber = new Color(1f, 0.78f, 0.25f);
        foreach (var c in candidates)
        {
            if (c == null || c == human || !c.activeInHierarchy) continue;
            var cc = c.GetComponent<CharacterController>();
            float hgt = cc != null ? cc.height : 1.6f;
            Vector3 sp = cam.WorldToScreenPoint(c.transform.position + Vector3.up * (hgt + 0.35f));
            if (sp.z <= 0f) continue;
            float y = Screen.height - sp.y;
            bool focus = !resolved && c == target;
            Txt(new Rect(sp.x - 120, y - 30, 240, 30), c.name.ToUpperInvariant(), sName, focus ? amber : Color.white);
            if (resolved)
            {
                int n = VotesFor(c);
                Txt(new Rect(sp.x - 120, y - 72, 240, 40), n + " OY", sBig, n > 0 ? amber : new Color(1f, 1f, 1f, 0.6f));
            }
        }
        float cx = Screen.width / 2f, cy = Screen.height / 2f;
        bool hud = GameHUD.Active;
        if (!resolved)
        {
            if (!hud) Txt(new Rect(cx - 300, 40, 600, 50), "OYLAMA", sTitle, amber);
            if (!hud && callerObj != null) Txt(new Rect(cx - 400, 124, 800, 26), callerObj == human ? "Sen toplanti cagirdin" : callerObj.name + " toplanti cagirdi", sHint, new Color(1f, 1f, 1f, 0.8f));
            float left = Mathf.Max(0f, meetingEndsAt - Time.time);
            if (!hud) Txt(new Rect(cx - 300, 150, 600, 30), "KALAN SURE: " + Mathf.CeilToInt(left), sName, left <= 10f ? new Color(1f, 0.35f, 0.3f) : Color.white);
            if (!hud) Txt(new Rect(cx - 400, 92, 800, 28), "Etrafina bak  •  E: oy ver  •  Q: oy kullanma", sHint, Color.white);
            Txt(new Rect(cx - 10, cy - 12, 20, 24), "+", sName, target != null ? amber : Color.white);
            if (voted) { string vt = "OY VERILDI - digerleri bekleniyor"; if (hud) GameHUD.Prompt(vt, Rect.zero); else Txt(new Rect(cx - 300, cy + 30, 600, 30), vt, sName, amber); } else if (target != null) { string pt = "E: " + target.name.ToUpperInvariant() + " ICIN OY VER"; if (hud) GameHUD.Prompt(pt, Rect.zero); else Txt(new Rect(cx - 250, cy + 30, 500, 30), pt, sName, amber); }
        }
        else
        {
            if (!hud) Txt(new Rect(cx - 500, 40, 1000, 36), resultText, sName, Color.white);
            if (!hud) Txt(new Rect(cx - 300, 80, 600, 28), "Sana verilen oy: " + VotesFor(human), sHint, amber);
        }
    }
}
