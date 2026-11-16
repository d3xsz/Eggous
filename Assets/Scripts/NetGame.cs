using System.Linq;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Oyun durumu (asama 2): sunucu karar verir. Host baslatinca roller dagitilir (her oyuncu SADECE kendi rolunu alir),
// kuluckaya varis / sure bitisi sunucuda karara baglanir, tur sonunda tum roller acilir.
public class NetGame : NetworkBehaviour
{
    public static NetGame Instance;
    public static bool Started;                         // tur basladi mi (tum bilgisayarlarda)
    readonly Dictionary<ulong, Role> serverRoles = new Dictionary<ulong, Role>();   // SADECE sunucuda tam liste

    public override void OnNetworkSpawn()
    {
        Instance = this;
        GameManager.NetMode = true;
        GameManager.NetIsServer = IsServer;
        Hatchery.Reached += OnHatcheryReached;
    }

    public override void OnNetworkDespawn()
    {
        Hatchery.Reached -= OnHatcheryReached;
        if (Instance == this) Instance = null;
        Started = false;
        GameManager.NetStarted = false;
    }

    static GameObject PlayerObj(ulong id)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return null;
        // Netcode: istemci (host olmayan) baska oyuncularin player nesnesini GetPlayerNetworkObject ile BULAMAZ (null doner).
        // Bu yuzden istemcide herkesi sahne icinden OwnerClientId ile ariyoruz.
        if (nm.IsServer || nm.LocalClientId == id)
        {
            var no = nm.SpawnManager.GetPlayerNetworkObject(id);
            if (no != null) return no.gameObject;
        }
        foreach (var np in FindObjectsByType<NetPlayer>(FindObjectsSortMode.None))
            if (np.OwnerClientId == id) return np.gameObject;
        return null;
    }

    // ---- Host: turu baslat ----
    public void HostStart()
    {
        if (!IsServer || Started) return;
        var ids = new List<ulong>();
        var objs = new List<GameObject>();
        foreach (var id in NetworkManager.ConnectedClientsIds)
        {
            var go = PlayerObj(id);
            if (go != null) { ids.Add(id); objs.Add(go); }
        }
        if (RoleManager.Instance == null || ids.Count == 0) return;

        var map = RoleManager.Instance.Compute(objs);
        serverRoles.Clear();
        for (int i = 0; i < ids.Count; i++) serverRoles[ids[i]] = map[objs[i]];
        for (int i = 0; i < ids.Count; i++) Debug.Log("[NetGame] Rol: oyuncu " + ids[i] + " = " + serverRoles[ids[i]]);

        StartRoundClientRpc(GameManager.Instance != null ? GameManager.Instance.roundSeconds : 900f);
        for (int i = 0; i < ids.Count; i++)
        {
            var p = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { ids[i] } } };
            SetMyRoleClientRpc((int)serverRoles[ids[i]], p);
        }
    }

    [ClientRpc]
    void StartRoundClientRpc(float seconds)
    {
        Started = true;
        GameManager.NetStarted = true;
        var gm = GameManager.Instance;
        if (gm != null) { gm.roundSeconds = seconds; gm.StartRound(); }
    }

    [ClientRpc]
    void SetMyRoleClientRpc(int role, ClientRpcParams p = default)
    {
        var me = PlayerController.Local;
        if (me == null || RoleManager.Instance == null) return;
        var map = new Dictionary<GameObject, Role>();
        map[me.gameObject] = (Role)role;
        RoleManager.Instance.NetSetRoles(map);
    }

    // ---- Kuluckaya varis: sahip bildirir, sunucu karar verir ----
    void OnHatcheryReached(GameObject who)
    {
        if (!Started) return;
        ReachedServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void ReachedServerRpc(ServerRpcParams p = default)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Tur) return;
        ulong id = p.Receive.SenderClientId;
        Role r;
        if (eliminatedIds.Contains(id) || !serverRoles.TryGetValue(id, out r)) return;   // elenen kuluckaya varamaz
        gm.hatcheryReacher = PlayerObj(id);
        gm.Finish(r == Role.Tirmanici ? Winner.Tirmanici : Winner.Safeler);
    }

    // ---- Tur bitti (sunucu): kazanan + tum roller herkese ----
    public void OnServerFinished(Winner w, GameObject reacher)
    {
        if (!IsServer) return;
        var ids = new List<ulong>();
        var rs = new List<int>();
        foreach (var kv in serverRoles) { ids.Add(kv.Key); rs.Add((int)kv.Value); }
        ulong reacherId = ulong.MaxValue;
        if (reacher != null) { var no = reacher.GetComponent<NetworkObject>(); if (no != null) reacherId = no.OwnerClientId; }
        FinishClientRpc((int)w, reacherId, ids.ToArray(), rs.ToArray());
    }

    [ClientRpc]
    void FinishClientRpc(int w, ulong reacherId, ulong[] ids, int[] rs)
    {
        var rm = RoleManager.Instance;
        var gm = GameManager.Instance;
        if (rm != null)
        {
            var map = new Dictionary<GameObject, Role>();
            for (int i = 0; i < ids.Length; i++)
            {
                var go = PlayerObj(ids[i]);
                if (go != null) map[go] = (Role)rs[i];
            }
            rm.NetSetRoles(map, false);
        }
        if (gm != null)
        {
            gm.hatcheryReacher = reacherId != ulong.MaxValue ? PlayerObj(reacherId) : null;
            gm.Finish((Winner)w, true);
        }
    }

    // ================= Toplanti / oylama (asama 3) =================
    readonly HashSet<ulong> eliminatedIds = new HashSet<ulong>();
    readonly List<ulong> meetingIds = new List<ulong>();
    readonly Dictionary<ulong, ulong> votes = new Dictionary<ulong, ulong>();   // oy veren -> hedef (MaxValue = oy kullanmadi)
    bool meetingActive;
    float meetingDeadline;

    void Update()
    {
        if (IsServer && meetingActive && Time.time >= meetingDeadline) ResolveMeeting();
    }

    // Zil calan (yerel) oyuncu cagirir
    public void RequestMeeting() { RequestMeetingServerRpc(); }

    [ServerRpc(RequireOwnership = false)]
    void RequestMeetingServerRpc(ServerRpcParams p = default)
    {
        var gm = GameManager.Instance;
        if (!Started || meetingActive || gm == null || gm.State != GameState.Tur) return;
        ulong caller = p.Receive.SenderClientId;
        if (eliminatedIds.Contains(caller) || !serverRoles.ContainsKey(caller)) return;
        meetingIds.Clear();
        foreach (var id in serverRoles.Keys)
            if (!eliminatedIds.Contains(id) && NetworkManager.ConnectedClientsIds.Contains(id)) meetingIds.Add(id);
        if (meetingIds.Count < 2) return;
        meetingActive = true;
        votes.Clear();
        var vs = FindFirstObjectByType<VoteSystem>();
        float secs = vs != null ? vs.meetingSeconds : 45f;
        float bell = vs != null ? vs.bellDelay : 2.5f;
        meetingDeadline = Time.time + bell + secs + 5f;   // sahne yukleme payi
        BeginMeetingClientRpc(caller, meetingIds.ToArray(), secs);
    }

    [ClientRpc]
    void BeginMeetingClientRpc(ulong caller, ulong[] ids, float secs)
    {
        var vs = FindFirstObjectByType<VoteSystem>();
        if (vs == null) return;
        var list = new List<GameObject>();
        foreach (var id in ids) { var go = PlayerObj(id); if (go != null) list.Add(go); }
        vs.NetBegin(PlayerObj(caller), list, secs);
    }

    public void SubmitVote(GameObject target)
    {
        ulong tid = ulong.MaxValue;
        if (target != null) { var no = target.GetComponent<NetworkObject>(); if (no != null) tid = no.OwnerClientId; }
        SubmitVoteServerRpc(tid);
    }

    [ServerRpc(RequireOwnership = false)]
    void SubmitVoteServerRpc(ulong target, ServerRpcParams p = default)
    {
        if (!meetingActive) return;
        ulong v = p.Receive.SenderClientId;
        if (!meetingIds.Contains(v) || votes.ContainsKey(v)) return;
        if (target != ulong.MaxValue && (!meetingIds.Contains(target) || target == v)) target = ulong.MaxValue;
        votes[v] = target;
        if (votes.Count >= meetingIds.Count) ResolveMeeting();
    }

    void ResolveMeeting()
    {
        if (!meetingActive) return;
        meetingActive = false;
        var tally = new Dictionary<ulong, int>();
        foreach (var kv in votes)
            if (kv.Value != ulong.MaxValue) { int n; tally.TryGetValue(kv.Value, out n); tally[kv.Value] = n + 1; }
        ulong best = ulong.MaxValue; int bestN = 0; bool tie = false;
        foreach (var kv in tally)
        {
            if (kv.Value > bestN) { best = kv.Key; bestN = kv.Value; tie = false; }
            else if (kv.Value == bestN) tie = true;
        }
        if (tie || bestN == 0) best = ulong.MaxValue;
        int role = -1;
        if (best != ulong.MaxValue) { eliminatedIds.Add(best); role = (int)serverRoles[best]; }
        var tIds = new List<ulong>(); var tCnt = new List<int>();
        foreach (var kv in tally) { tIds.Add(kv.Key); tCnt.Add(kv.Value); }
        MeetingResultClientRpc(best, role, tIds.ToArray(), tCnt.ToArray());
    }

    [ClientRpc]
    void MeetingResultClientRpc(ulong elim, int role, ulong[] tIds, int[] tCnt)
    {
        var vs = FindFirstObjectByType<VoteSystem>();
        if (vs == null) return;
        var tally = new Dictionary<GameObject, int>();
        for (int i = 0; i < tIds.Length; i++) { var go = PlayerObj(tIds[i]); if (go != null) tally[go] = tCnt[i]; }
        vs.NetResult(elim != ulong.MaxValue ? PlayerObj(elim) : null, role < 0 ? Role.Safe : (Role)role, tally);
    }

    // ================= Balyalar (asama 4) =================
    public GameObject baleNetPrefab;     // BaleNet.prefab (sunucu dogurur)

    public void PlaceBale(Vector3 pos, Quaternion rot) { PlaceBaleServerRpc(pos, rot, true); }
    public void DropBale(Vector3 pos, Quaternion rot) { PlaceBaleServerRpc(pos, rot, false); }

    [ServerRpc(RequireOwnership = false)]
    void PlaceBaleServerRpc(Vector3 pos, Quaternion rot, bool placed, ServerRpcParams p = default)
    {
        var gm = GameManager.Instance;
        if (baleNetPrefab == null || !Started || gm == null) return;
        if (placed && gm.State != GameState.Tur) return;
        var go = Instantiate(baleNetPrefab, pos, rot);
        go.GetComponent<NetworkObject>().Spawn();
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = false; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        if (!placed) return;   // birakilan balya: serbest fizik, sayilmaz
        var st = go.GetComponent<BaleState>();
        if (st == null) st = go.AddComponent<BaleState>();
        st.placedBy = PlayerObj(p.Receive.SenderClientId);
        if (BaleTracker.Instance != null) BaleTracker.Instance.RegisterPlaced(st);
        st.placed = true;
        if (rb != null) { rb.constraints = RigidbodyConstraints.FreezeRotation; StartCoroutine(SettleBale(rb)); }
    }

    System.Collections.IEnumerator SettleBale(Rigidbody r)
    {
        yield return new WaitForSeconds(0.4f);
        if (r != null) r.constraints = RigidbodyConstraints.None;
    }

    public void UnfreezeBale(NetworkObject no) { if (no != null) UnfreezeBaleServerRpc(new NetworkObjectReference(no)); }

    [ServerRpc(RequireOwnership = false)]
    void UnfreezeBaleServerRpc(NetworkObjectReference r)
    {
        NetworkObject no;
        if (!r.TryGet(out no)) return;
        var st = no.GetComponent<BaleState>();
        if (st != null) st.Unfreeze();
    }

    public void AnnounceFell(string who) { if (IsServer) FellClientRpc(who); }

    [ClientRpc]
    void FellClientRpc(string who)
    {
        if (BaleTracker.Instance != null) BaleTracker.Instance.AddMessage(who + " dustu!");
    }

    // ================= Vurma / itme / uyutma (asama 4) =================
    // Bir oyuncu uzaktaki baska oyuncuya vurunca (yerel kopyada) olay, asil sahibine iletilir
    public static void ForwardHit(PlayerCombat victim, int kind, Vector3 dir, PlayerCombat attacker, float a, float b, string what)
    {
        if (Instance == null || victim == null) return;
        var vno = victim.GetComponentInParent<NetworkObject>();
        if (vno == null) return;
        ulong aid = ulong.MaxValue;
        if (attacker != null) { var ano = attacker.GetComponentInParent<NetworkObject>(); if (ano != null) aid = ano.OwnerClientId; }
        Instance.HitServerRpc(vno.OwnerClientId, kind, dir, aid, a, b, what ?? "");
    }

    [ServerRpc(RequireOwnership = false)]
    void HitServerRpc(ulong victim, int kind, Vector3 dir, ulong attacker, float a, float b, string what)
    {
        var p = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { victim } } };
        HitClientRpc(kind, dir, attacker, a, b, what, p);
    }

    [ClientRpc]
    void HitClientRpc(int kind, Vector3 dir, ulong attacker, float a, float b, string what, ClientRpcParams p = default)
    {
        var me = PlayerController.Local;
        if (me == null) return;
        var pc = me.GetComponent<PlayerCombat>();
        if (pc == null) return;
        PlayerCombat att = null;
        if (attacker != ulong.MaxValue) { var ago = PlayerObj(attacker); if (ago != null) att = ago.GetComponent<PlayerCombat>(); }
        if (kind == 0) pc.ReceiveHit(dir, att, a, b, what);
        else if (kind == 1) pc.ReceivePush(dir, a);
        else pc.Sleep(a, att);
    }
}
