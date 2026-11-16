using Unity.Netcode;
using UnityEngine;

// Agdaki balya: fizigi SADECE sunucuda calisir; istemcilerde kinematik (konum NetworkTransform'dan gelir).
public class NetBale : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        var rb = GetComponent<Rigidbody>();
        if (!IsServer && rb != null) rb.isKinematic = true;
    }
}
