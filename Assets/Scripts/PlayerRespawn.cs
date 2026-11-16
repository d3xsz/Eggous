using UnityEngine;

// Oyuncunun son checkpoint'e donmesi. Bosluga dusunce (fallY altina inince) respawn olur.
[RequireComponent(typeof(CharacterController))]
public class PlayerRespawn : MonoBehaviour
{
    public float fallY = -8f;
    public int lastCheckpoint = 0;

    Vector3 respawnPos;
    CharacterController cc;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        respawnPos = transform.position;
    }

    void Update()
    {
        if (transform.position.y < fallY) Respawn();
    }

    public void SetCheckpoint(int index, Vector3 pos)
    {
        if (index <= lastCheckpoint) return;
        lastCheckpoint = index;
        respawnPos = pos;
    }

    // Botlar: dusunce dogulacak guvenli nokta (checkpoint sirasindan bagimsiz)
    public void SetRespawnPoint(Vector3 pos) { respawnPos = pos; }

    public void Respawn()
    {
        var np = GetComponent<NetPlayer>();
        if (np != null && np.IsSpawned && np.IsOwner) { np.TeleportTo(respawnPos, transform.rotation); return; }   // ag: digerlerinde kayma olmasin
        cc.enabled = false;
        transform.position = respawnPos;
        cc.enabled = true;
    }
}
