using System.Collections.Generic;
using UnityEngine;

// Balya takibi (4.1): kim kac balya yerlestirdi, kimin balyasi dustu. Tek tek balyalari izler, yolun butununu degil;
// oyun akisini degistirmez. Sadece YERLESTIRILMIS balyalar sayilir (elden dusen/birakilan sayilmaz).
public class BaleTracker : MonoBehaviour
{
    public static BaleTracker Instance;
    public static event System.Action<BaleState> BaleFell; // balya yok edilmeden hemen once (placedBy okunabilir)

    [Tooltip("Yerlestirilmis balya bu yukseklikten asagi inerse 'dustu' sayilir.")]
    public float fallY = -8f;
    [Tooltip("Dusen balyayi ekranda herkese duyur (ipucu).")]
    public bool announce = true;
    public float messageSeconds = 5f;

    [System.Serializable]
    public class Record { public string player; public int placed; public int fallen; }
    [Header("Goruntu (salt okunur)")]
    public List<Record> records = new List<Record>();

    readonly Dictionary<GameObject, Record> map = new Dictionary<GameObject, Record>();
    readonly List<string> msgs = new List<string>();
    readonly List<float> msgUntil = new List<float>();

    void Awake() { Instance = this; }

    Record Get(GameObject p)
    {
        Record r;
        if (!map.TryGetValue(p, out r))
        {
            r = new Record { player = p.name };
            map[p] = r;
            records.Add(r);
        }
        return r;
    }

    public int Placed(GameObject p) { Record r; return (p != null && map.TryGetValue(p, out r)) ? r.placed : 0; }
    public int Fallen(GameObject p) { Record r; return (p != null && map.TryGetValue(p, out r)) ? r.fallen : 0; }
    public int Standing(GameObject p) { return Placed(p) - Fallen(p); }

    public void RegisterPlaced(BaleState b)
    {
        if (b == null || b.placedBy == null) return;
        Get(b.placedBy).placed++;
    }

    public void ReportFell(BaleState b)
    {
        if (b == null) return;
        if (b.placedBy != null)
        {
            Get(b.placedBy).fallen++;
            if (GameManager.NetMode && NetGame.Instance != null) NetGame.Instance.AnnounceFell(b.placedBy.name + " adli oyuncunun balyasi");
            Debug.Log("[BaleTracker] Dusen balya: " + b.placedBy.name + " tarafindan konmustu");
            if (announce && !GameManager.NetMode)
            {
                var human = PlayerController.LocalOrFind();
                string who = (human != null && b.placedBy == human.gameObject) ? "Senin balyan" : b.placedBy.name + " adli oyuncunun balyasi";
                msgs.Add(who + " dustu!");
                msgUntil.Add(Time.time + messageSeconds);
            }
        }
        if (BaleFell != null) BaleFell(b);
        DestroyBale(b.gameObject);
    }

    public void AddMessage(string m) { msgs.Add(m); msgUntil.Add(Time.time + messageSeconds); }

    void DestroyBale(GameObject g)
    {
        var no = g.GetComponent<Unity.Netcode.NetworkObject>();
        if (no != null && no.IsSpawned) { if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer) no.Despawn(true); }
        else Destroy(g);
    }


    // Gecici duyuru metni (UI guzellestirmesi Faz 6'da)
    void OnGUI()
    {
        for (int i = msgs.Count - 1; i >= 0; i--)
            if (Time.time > msgUntil[i]) { msgs.RemoveAt(i); msgUntil.RemoveAt(i); }
        for (int i = 0; i < msgs.Count; i++)
            GUI.Label(new Rect(10, 40 + i * 24, 500, 24), msgs[i]);
    }
}
