using System.Linq;
using Unity.Netcode;
using UnityEngine;

// Asama 4 (ag senkronu): sahne nesneleri (firlatilabilir, kova, item kutusu, airdrop, islak balya, item etkileri) icin olay yayini.
// Mantik: her istemcide AYNI sahne nesneleri var -> nesneler siralanmis liste indeksiyle taninir. Olay, sahibinde yerelde
// uygulanir; sunucu uzerinden DIGER istemcilere yayinlanir (gonderen atlanir). Firlatilan nesnenin isabeti SADECE firlatanda hesaplanir.
// Ag yokken tum statik cagrilar sessizce bos gecer (tek oyunculu mod bozulmaz).
public class NetWorld : NetworkBehaviour
{
    public static NetWorld Instance;
    public static bool Active { get { return Instance != null && Instance.IsSpawned; } }
    public static bool IsServerNow { get { return Instance != null && Instance.IsServer; } }

    Throwable[] throwables; BucketStand[] stands; ItemPickup[] boxes;
    int crateSeq;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        throwables = Sorted<Throwable>(); stands = Sorted<BucketStand>(); boxes = Sorted<ItemPickup>();
    }

    public override void OnNetworkDespawn() { if (Instance == this) Instance = null; }

    static string Key(Component c) { var p = c.transform.position; return c.name + "|" + Mathf.RoundToInt(p.x * 10f) + "|" + Mathf.RoundToInt(p.y * 10f) + "|" + Mathf.RoundToInt(p.z * 10f); }
    static T[] Sorted<T>() where T : Component { return FindObjectsByType<T>(FindObjectsSortMode.None).OrderBy(x => Key(x), System.StringComparer.Ordinal).ToArray(); }
    static int Id<T>(T[] a, T x) where T : class { if (a != null) for (int i = 0; i < a.Length; i++) if (a[i] == x) return i; return -1; }
    static T Get<T>(T[] a, int id) where T : class { return a != null && id >= 0 && id < a.Length ? a[id] : null; }

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

    // ================= Firlatilabilir nesneler =================
    public static void ThrowPick(Throwable t) { if (Active && t != null) Instance.PickServerRpc(Id(Instance.throwables, t)); }
    public static void ThrowDrop(Throwable t) { if (Active && t != null) Instance.DropServerRpc(Id(Instance.throwables, t), t.transform.position, t.transform.rotation); }
    public static void ThrowThrow(Throwable t, Vector3 vel, Vector3 ang) { if (Active && t != null) Instance.ThrowServerRpc(Id(Instance.throwables, t), t.transform.position, t.transform.rotation, vel, ang); }

    [ServerRpc(RequireOwnership = false)]
    void PickServerRpc(int id, ServerRpcParams p = default) { if (id >= 0) PickClientRpc(id, p.Receive.SenderClientId); }

    [ClientRpc]
    void PickClientRpc(int id, ulong by)
    {
        if (by == NetworkManager.LocalClientId) return;
        var t = Get(throwables, id); var po = PlayerObj(by);
        if (t == null || po == null) return;
        t.OnPicked();
        var rb = t.GetComponent<Rigidbody>();
        rb.isKinematic = true; rb.detectCollisions = false;
        foreach (var c in t.GetComponentsInChildren<Collider>()) c.enabled = false;
        t.transform.SetParent(po.transform, false);
        t.transform.localPosition = new Vector3(0.45f, 1.25f, 0.9f);
        t.transform.localRotation = Quaternion.identity;
    }

    static Rigidbody ReleaseRemote(Throwable t, Vector3 pos, Quaternion rot)
    {
        t.transform.SetParent(null, true);
        t.transform.SetPositionAndRotation(pos, rot);
        foreach (var c in t.GetComponentsInChildren<Collider>()) c.enabled = true;
        var rb = t.GetComponent<Rigidbody>();
        rb.isKinematic = false; rb.detectCollisions = true; rb.linearVelocity = Vector3.zero;
        return rb;
    }

    [ServerRpc(RequireOwnership = false)]
    void DropServerRpc(int id, Vector3 pos, Quaternion rot, ServerRpcParams p = default) { if (id >= 0) DropClientRpc(id, pos, rot, p.Receive.SenderClientId); }

    [ClientRpc]
    void DropClientRpc(int id, Vector3 pos, Quaternion rot, ulong by)
    {
        if (by == NetworkManager.LocalClientId) return;
        var t = Get(throwables, id); if (t == null) return;
        ReleaseRemote(t, pos, rot);
        t.OnDropped();
    }

    [ServerRpc(RequireOwnership = false)]
    void ThrowServerRpc(int id, Vector3 pos, Quaternion rot, Vector3 vel, Vector3 ang, ServerRpcParams p = default) { if (id >= 0) ThrowClientRpc(id, pos, rot, vel, ang, p.Receive.SenderClientId); }

    [ClientRpc]
    void ThrowClientRpc(int id, Vector3 pos, Quaternion rot, Vector3 vel, Vector3 ang, ulong by)
    {
        if (by == NetworkManager.LocalClientId) return;
        var t = Get(throwables, id); if (t == null) return;
        var rb = ReleaseRemote(t, pos, rot);
        rb.linearVelocity = vel; rb.angularVelocity = ang;
        t.OnThrownRemote();
    }

    // ================= Kova + islak balya =================
    public static void BucketTake(BucketStand s) { if (Active && s != null) Instance.TakeServerRpc(Id(Instance.stands, s)); }
    public static void BucketDrop(BucketStand s) { if (Active && s != null) Instance.BucketDropServerRpc(Id(Instance.stands, s), s.transform.position, s.transform.rotation, s.water); }

    [ServerRpc(RequireOwnership = false)]
    void TakeServerRpc(int id, ServerRpcParams p = default) { if (id >= 0) TakeClientRpc(id, p.Receive.SenderClientId); }

    [ClientRpc]
    void TakeClientRpc(int id, ulong by)
    {
        if (by == NetworkManager.LocalClientId) return;
        var s = Get(stands, id); if (s != null) s.Hide();
    }

    [ServerRpc(RequireOwnership = false)]
    void BucketDropServerRpc(int id, Vector3 pos, Quaternion rot, float water, ServerRpcParams p = default) { if (id >= 0) BucketDropClientRpc(id, pos, rot, water, p.Receive.SenderClientId); }

    [ClientRpc]
    void BucketDropClientRpc(int id, Vector3 pos, Quaternion rot, float water, ulong by)
    {
        if (by == NetworkManager.LocalClientId) return;
        var s = Get(stands, id); if (s == null) return;
        s.transform.SetPositionAndRotation(pos, rot);
        s.water = water;
        s.Show();
    }

    public static void Wet(NetworkObject bale, float seconds) { if (Active && bale != null) Instance.WetServerRpc(new NetworkObjectReference(bale), seconds); }

    [ServerRpc(RequireOwnership = false)]
    void WetServerRpc(NetworkObjectReference r, float seconds) { WetClientRpc(r, seconds); }

    [ClientRpc]
    void WetClientRpc(NetworkObjectReference r, float seconds)
    {
        NetworkObject no;
        if (r.TryGet(out no)) WetMark.Apply(no.gameObject, seconds);
    }

    // ================= Item etkileri + kutular =================
    public static void Oil(Vector3 pos, float radius, float seconds) { if (Active) Instance.OilServerRpc(pos, radius, seconds); }
    public static void Dust(Vector3 pos, float radius, float seconds) { if (Active) Instance.DustServerRpc(pos, radius, seconds); }
    public static void BoxTaken(ItemPickup b) { if (Active && b != null) Instance.BoxServerRpc(Id(Instance.boxes, b)); }

    [ServerRpc(RequireOwnership = false)]
    void OilServerRpc(Vector3 pos, float radius, float seconds, ServerRpcParams p = default) { OilClientRpc(pos, radius, seconds, p.Receive.SenderClientId); }

    [ClientRpc]
    void OilClientRpc(Vector3 pos, float radius, float seconds, ulong by)
    {
        if (by == NetworkManager.LocalClientId) return;
        OilSpot.Create(pos, radius, seconds);
    }

    [ServerRpc(RequireOwnership = false)]
    void DustServerRpc(Vector3 pos, float radius, float seconds, ServerRpcParams p = default) { DustClientRpc(pos, radius, seconds, p.Receive.SenderClientId); }

    [ClientRpc]
    void DustClientRpc(Vector3 pos, float radius, float seconds, ulong by)
    {
        if (by == NetworkManager.LocalClientId) return;
        DustCloud.Create(pos, radius, seconds);
    }

    [ServerRpc(RequireOwnership = false)]
    void BoxServerRpc(int id, ServerRpcParams p = default) { if (id >= 0) BoxClientRpc(id, p.Receive.SenderClientId); }

    [ClientRpc]
    void BoxClientRpc(int id, ulong by)
    {
        if (by == NetworkManager.LocalClientId) return;
        var b = Get(boxes, id); if (b != null) b.NetTaken();
    }

    // ================= Airdrop: dusme kararini sunucu verir =================
    public static void AirdropSpawn(Vector3 ground) { if (Active && Instance.IsServer) Instance.AirdropClientRpc(ground, ++Instance.crateSeq); }
    public static void AirdropOpen(int crateId, int itemType) { if (Active && crateId >= 0) Instance.AirdropOpenServerRpc(crateId, itemType); }

    [ClientRpc]
    void AirdropClientRpc(Vector3 ground, int id)
    {
        if (AirdropManager.Instance != null) AirdropManager.Instance.SpawnAt(ground, id);
    }

    [ServerRpc(RequireOwnership = false)]
    void AirdropOpenServerRpc(int id, int type, ServerRpcParams p = default) { AirdropOpenClientRpc(id, type, p.Receive.SenderClientId); }

    [ClientRpc]
    void AirdropOpenClientRpc(int id, int type, ulong by)
    {
        if (by == NetworkManager.LocalClientId || AirdropManager.Instance == null) return;
        var c = AirdropManager.Instance.GetByNetId(id);
        if (c != null) c.RemoteOpen(type);
    }
}
