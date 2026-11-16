using System.Collections;
using Unity.Netcode.Components;
using Unity.Netcode;
using UnityEngine;

// Ag oyuncusu (Oyuncu.prefab). Sahip: kendi oyuncusunu surer (birinci sahis, govde yok).
// Diger oyuncular: sadece gorsel (yumurta govdesi) + NetworkTransform ile gelen konum/donus.
public class NetPlayer : NetworkBehaviour
{
    public GameObject bodyPrefab;                          // EggBody.prefab (uzaktaki oyuncular icin)
    public Vector3 spawnBase = new Vector3(3f, 0.1f, -1f);
    public float spawnRadius = 2.5f;
    public Color[] colors = new Color[]
    {
        new Color(0.95f, 0.95f, 0.9f), new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.95f), new Color(0.3f, 0.8f, 0.4f),
        new Color(0.95f, 0.8f, 0.2f), new Color(0.7f, 0.4f, 0.9f), new Color(0.95f, 0.55f, 0.2f), new Color(0.3f, 0.85f, 0.85f)
    };

    // Balya tasiyor mu (sahip yazar, herkes okur) -> uzak oyuncuda el gorunumu + kol pozu
    public NetworkVariable<bool> carrying = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    GameObject heldVis;
    public void SetCarrying(bool c) { if (IsSpawned && IsOwner) carrying.Value = c; }

    // Vurulup yere yatti mi (sahip yazar, herkes okur) -> uzak oyuncuda devrilme gorseli
    public NetworkVariable<bool> down = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    Transform bodyRoot; float downK;

    void OnCarryChanged(bool o, bool n) { ApplyCarry(n); }

    void ApplyCarry(bool c)
    {
        if (IsOwner) return;
        var bcar = GetComponent<BaleCarrier>();
        if (bcar != null) bcar.NetCarrying = c;
        if (c && heldVis == null)
        {
            var sp = FindFirstObjectByType<BaleSpawner>();
            if (sp != null && sp.balePrefab != null)
            {
                heldVis = Instantiate(sp.balePrefab, transform);
                foreach (var col in heldVis.GetComponentsInChildren<Collider>()) Destroy(col);
                var rb = heldVis.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);
                heldVis.transform.localPosition = new Vector3(0f, 0.9f, 0.95f);
                heldVis.transform.localRotation = Quaternion.identity;
            }
        }
        else if (!c && heldVis != null) { Destroy(heldVis); heldVis = null; }
    }


    // Sadece sahibinde calisacak bilesenler (girdi/oyun mantigi); uzaktaki kopyada kapatilir
    static readonly System.Type[] OwnerOnly =
    {
        typeof(PlayerController), typeof(BaleCarrier), typeof(PlacementPreview), typeof(PlayerRespawn), typeof(WaterBucket),
        typeof(SlipperyMover), typeof(PlayerCombat), typeof(ObjectThrower), typeof(ItemSystem), typeof(SecretPush), typeof(GhostNudge)
    };

    public override void OnNetworkSpawn()
    {
        gameObject.name = "Oyuncu " + (OwnerClientId + 1);
        var cam = GetComponentInChildren<Camera>(true);
        var lis = GetComponentInChildren<AudioListener>(true);
        if (IsOwner)
        {
            PlayerController.Local = GetComponent<PlayerController>();
            var myItems = GetComponent<ItemSystem>(); if (myItems != null) ItemSystem.Local = myItems;   // envanter: sadece kendi oyuncun
            StartCoroutine(Place());
        }
        else
        {
            foreach (var t in OwnerOnly) { var c = GetComponent(t) as Behaviour; if (c != null) c.enabled = false; }
            if (cam != null) cam.enabled = false;
            if (lis != null) lis.enabled = false;
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.height = 1.7f; col.radius = 0.4f; col.center = new Vector3(0f, 0.88f, 0f);
            // Uzak oyuncunun PlayerCombat'i kapali (OnDisable listeden cikariyor); vurulabilmesi icin 'All' listesine geri ekle.
            // Vurus, PlayerCombat.ReceiveHit icindeki NetMode && !enabled dali ile NetGame.ForwardHit uzerinden asil sahibine iletilir.
            var rpcb = GetComponent<PlayerCombat>();
            if (rpcb != null && !PlayerCombat.All.Contains(rpcb)) PlayerCombat.All.Add(rpcb);
            BuildBody();
            carrying.OnValueChanged += OnCarryChanged;
            ApplyCarry(carrying.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        carrying.OnValueChanged -= OnCarryChanged;
        var pc = GetComponent<PlayerController>();
        var dpcb = GetComponent<PlayerCombat>();
        if (dpcb != null) PlayerCombat.All.Remove(dpcb);
        if (pc != null && PlayerController.Local == pc) PlayerController.Local = null;
        var dis = GetComponent<ItemSystem>(); if (dis != null && ItemSystem.Local == dis) ItemSystem.Local = null;
    }

    void Update()
    {
        if (!IsSpawned) return;
        var pcb = GetComponent<PlayerCombat>();
        if (IsOwner)
        {
            bool d = pcb != null && pcb.IsDown;
            if (down.Value != d) down.Value = d;
            return;
        }
        if (bodyRoot == null) return;
        // Uzak oyuncu: vurulunca govde yatar, sonra kalkar (PlayerCombat'in bot devrilmesiyle ayni sure/aci)
        float fallAngle = pcb != null ? pcb.fallAngle : 85f;
        float fallTime = pcb != null ? pcb.fallTime : 0.25f;
        float riseTime = pcb != null ? pcb.riseTime : 0.6f;
        float target = down.Value ? 1f : 0f;
        float spd = target > downK ? 1f / Mathf.Max(0.01f, fallTime) : 1f / Mathf.Max(0.01f, riseTime);
        downK = Mathf.MoveTowards(downK, target, Time.deltaTime * spd);
        float e = Mathf.SmoothStep(0f, 1f, downK);
        Quaternion rot = Quaternion.AngleAxis(fallAngle * e, Vector3.left);   // arkaya devrilir
        bodyRoot.localRotation = rot;
        bodyRoot.localPosition = new Vector3(0f, Mathf.Lerp(0.9f, 0.52f, e), 0f) - rot * new Vector3(0f, 0.9f, 0f);
    }

    IEnumerator Place()
    {
        yield return null;
        if (!IsSpawned) yield break;
        int idx = (int)(OwnerClientId % 8);
        float ang = Mathf.Deg2Rad * 45f * idx;
        Vector3 p = spawnBase + (idx == 0 ? Vector3.zero : new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * spawnRadius);
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = p;
        if (cc != null) cc.enabled = true;
        var rs = GetComponent<PlayerRespawn>();
        if (rs != null) rs.SetRespawnPoint(p);
    }

    // Toplanti vb. icin: sahibi kendi oyuncusunu aninda isinlar (digerlerinde yumusak kayma olmasin)
    public void TeleportTo(Vector3 pos, Quaternion rot)
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        var nt = GetComponent<NetworkTransform>();
        Debug.Log("[Toplanti] TeleportTo " + pos.ToString("F1") + " sahip=" + IsOwner + " nt=" + (nt != null) + " spawned=" + IsSpawned);
        if (nt != null && IsSpawned && IsOwner) nt.Teleport(pos, rot, transform.localScale);
        transform.SetPositionAndRotation(pos, rot);
        if (cc != null) cc.enabled = true;
    }


    void BuildBody()
    {
        if (bodyPrefab == null) return;
        var b = Instantiate(bodyPrefab, transform);
        b.name = "Govde_Grup";
        bodyRoot = b.transform;
        b.transform.localPosition = Vector3.zero;
        b.transform.localRotation = Quaternion.identity;
        Color c = colors.Length > 0 ? colors[(int)(OwnerClientId % (ulong)colors.Length)] : Color.white;
        foreach (var r in b.GetComponentsInChildren<Renderer>(true)) if (r.name == "Govde") r.material.SetColor("_BaseColor", c);
        if (GetComponent<EggArms>() == null) gameObject.AddComponent<EggArms>();
        if (GetComponent<EggFeet>() == null) gameObject.AddComponent<EggFeet>();
    }
}
