using UnityEngine;

// 5.2 Item kutusu: icine girince rastgele bir item verir (elde item yoksa). respawnSeconds sonra yeniden belirir.
public class ItemPickup : MonoBehaviour
{
    public float respawnSeconds = 30f;
    Renderer rend;
    Collider col;
    bool taken;
    float respawnAt;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        col = GetComponent<Collider>();
    }

    void Update()
    {
        if (!taken) { transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World); return; }
        if (Time.time >= respawnAt) { taken = false; rend.enabled = true; col.enabled = true; }
    }

    void OnTriggerEnter(Collider other)
    {
        if (taken) return;
        var it = other.GetComponentInParent<ItemSystem>();
        if (it == null || it.Held != ItemType.None || VoteSystem.IsEliminated(it.gameObject)) return;
        it.Give((ItemType)Random.Range(1, 5));
        taken = true;
        rend.enabled = false;
        col.enabled = false;
        respawnAt = Time.time + respawnSeconds;
        NetWorld.BoxTaken(this);
    }

    // Ag: baska oyuncu aldi -> burada da gizle
    public void NetTaken()
    {
        if (taken) return;
        taken = true; rend.enabled = false; col.enabled = false; respawnAt = Time.time + respawnSeconds;
    }
}
