using System.Collections.Generic;
using UnityEngine;

// Roller (md bolum 3): Safe = Haslanmis Yumurta, Tirmanici = Sahanda, Palyaco = Omlet, Sabotajci = Curuk Yumurta
public enum Role { Safe, Tirmanici, Palyaco, Sabotajci }

// Rol dagitimi: tur basinda sahnedeki tum oyunculara (insan + bot) gizli rol verir. Dagitim tablosu Inspector'dan ayarlanir.
public class RoleManager : MonoBehaviour
{
    public static RoleManager Instance;
    public static event System.Action RolesAssigned;

    [System.Serializable]
    public class CountRow
    {
        public int players;
        public int sabotajci;
        public int tirmanici;
        public int palyaco; // Safe sayisi = geri kalan
    }

    [System.Serializable]
    public class Entry { public string name; public Role role; }

    [Header("Dagitim tablosu (md bolum 3). Safe = oyuncu sayisi - digerleri")]
    public CountRow[] table = new CountRow[]
    {
        new CountRow { players = 4, sabotajci = 1, tirmanici = 1, palyaco = 0 },
        new CountRow { players = 5, sabotajci = 1, tirmanici = 1, palyaco = 1 },
        new CountRow { players = 6, sabotajci = 1, tirmanici = 1, palyaco = 1 },
        new CountRow { players = 7, sabotajci = 2, tirmanici = 1, palyaco = 1 },
        new CountRow { players = 8, sabotajci = 2, tirmanici = 1, palyaco = 1 },
    };

    [Header("Test")]
    [Tooltip("Acik: insan oyuncuya asagidaki rol zorla verilir (sabotaj vs. test icin).")]
    public bool forceHumanRole = false;
    public Role humanRole = Role.Sabotajci;
    [Tooltip("Konsola tum rolleri yazar (gelistirme icin, 3.5'ten sonra kapatilabilir).")]
    public bool debugLog = true;

    [Header("Goruntu (salt okunur)")]
    public List<Entry> assignments = new List<Entry>();

    readonly Dictionary<GameObject, Role> roles = new Dictionary<GameObject, Role>();
    bool pending = true;

    public static bool HasRole(GameObject who)
    {
        return Instance != null && who != null && Instance.roles.ContainsKey(who);
    }

    public static Role GetRole(GameObject who)
    {
        Role r;
        if (Instance != null && who != null && Instance.roles.TryGetValue(who, out r)) return r;
        return Role.Safe;
    }

    public static string DisplayName(Role r)
    {
        switch (r)
        {
            case Role.Tirmanici: return "Sahanda (Tirmanici)";
            case Role.Palyaco: return "Omlet (Palyaco)";
            case Role.Sabotajci: return "Curuk Yumurta (Sabotajci)";
            default: return "Haslanmis Yumurta (Safe)";
        }
    }

    void Awake() { Instance = this; }
    void OnEnable() { GameManager.StateChanged += OnState; }
    void OnDisable() { GameManager.StateChanged -= OnState; }

    // Yeni tur baslayinca roller yeniden dagitilir (oylamadan donuste degil)
    void OnState(GameState old, GameState now)
    {
        if (now == GameState.Tur && old != GameState.Oylama) pending = true;
    }

    // Tum Start'lar (botlarin dogmasi dahil) bittikten sonra ilk karede dagit
    void Update()
    {
        if (!pending) return;
        pending = false;
        if (GameManager.NetMode) return;   // ag: roller sunucudan gelir (NetGame)
        Assign();
    }

    public void Assign()
    {
        var list = new List<GameObject>();
        foreach (var c in FindObjectsByType<BaleCarrier>(FindObjectsSortMode.None)) list.Add(c.gameObject);
        int n = list.Count;
        roles.Clear();
        assignments.Clear();
        if (n == 0) return;

        CountRow row = RowFor(n);
        var pool = new List<Role>();
        for (int i = 0; i < row.sabotajci; i++) pool.Add(Role.Sabotajci);
        for (int i = 0; i < row.tirmanici; i++) pool.Add(Role.Tirmanici);
        for (int i = 0; i < row.palyaco; i++) pool.Add(Role.Palyaco);
        while (pool.Count < Mathf.Max(n, row.players)) pool.Add(Role.Safe);
        if (pool.Count > n) pool.RemoveRange(n, pool.Count - n); // az oyuncu: onem sirasina gore (sabotajci, tirmanici, ...)

        for (int i = pool.Count - 1; i > 0; i--) // Fisher-Yates
        {
            int j = Random.Range(0, i + 1);
            Role t = pool[i]; pool[i] = pool[j]; pool[j] = t;
        }
        for (int i = 0; i < n; i++) roles[list[i]] = pool[i];

        if (forceHumanRole)
        {
            var human = PlayerController.LocalOrFind();
            if (human != null && roles.ContainsKey(human.gameObject) && roles[human.gameObject] != humanRole)
            {
                GameObject other = null;
                foreach (var kv in roles)
                    if (kv.Key != human.gameObject && kv.Value == humanRole) { other = kv.Key; break; }
                if (other != null) roles[other] = roles[human.gameObject];
                roles[human.gameObject] = humanRole;
            }
        }

        foreach (var kv in roles)
        {
            assignments.Add(new Entry { name = kv.Key.name, role = kv.Value });
            if (debugLog) Debug.Log("[RoleManager] " + kv.Key.name + ": " + DisplayName(kv.Value));
        }
        if (RolesAssigned != null) RolesAssigned();
    }

    // Ag: sunucu rolleri hesaplar (ayni dagitim tablosu). Sozluk: oyuncu objesi -> rol
    public Dictionary<GameObject, Role> Compute(List<GameObject> list)
    {
        var res = new Dictionary<GameObject, Role>();
        int n = list.Count;
        if (n == 0) return res;
        CountRow row = RowFor(n);
        var pool = new List<Role>();
        for (int i = 0; i < row.sabotajci; i++) pool.Add(Role.Sabotajci);
        for (int i = 0; i < row.tirmanici; i++) pool.Add(Role.Tirmanici);
        for (int i = 0; i < row.palyaco; i++) pool.Add(Role.Palyaco);
        while (pool.Count < Mathf.Max(n, row.players)) pool.Add(Role.Safe);
        if (pool.Count > n)
        {
            // az oyuncu: 1 Sabotajci garanti, kalan roller tum havuzdan HER TURDA rastgele cekilir (hep ayni roller cikmasin)
            pool.Remove(Role.Sabotajci);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                Role t = pool[i]; pool[i] = pool[j]; pool[j] = t;
            }
            pool.RemoveRange(Mathf.Max(0, n - 1), pool.Count - Mathf.Max(0, n - 1));
            pool.Add(Role.Sabotajci);
        }
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Role t = pool[i]; pool[i] = pool[j]; pool[j] = t;
        }
        for (int i = 0; i < n; i++) res[list[i]] = pool[i];
        return res;
    }

    // Ag: istemci kendi rolunu (tur basi) veya tum rolleri (tur sonu) yazar
    public void NetSetRoles(Dictionary<GameObject, Role> map, bool announce = true)
    {
        roles.Clear();
        assignments.Clear();
        foreach (var kv in map)
        {
            roles[kv.Key] = kv.Value;
            assignments.Add(new Entry { name = kv.Key.name, role = kv.Value });
        }
        if (announce && RolesAssigned != null) RolesAssigned();
    }

    CountRow RowFor(int n)
    {
        CountRow best = null;
        int bestDiff = int.MaxValue;
        foreach (var r in table)
        {
            int d = Mathf.Abs(r.players - n);
            if (d < bestDiff) { bestDiff = d; best = r; }
        }
        return best ?? new CountRow { players = n, sabotajci = 1, tirmanici = 1, palyaco = 0 };
    }
}
