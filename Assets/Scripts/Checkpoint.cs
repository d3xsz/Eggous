using UnityEngine;

// Ada checkpoint'i: oyuncu girince respawn noktasi guncellenir.
public class Checkpoint : MonoBehaviour
{
    public int index = 1;

    void OnTriggerEnter(Collider other)
    {
        var p = other.GetComponentInParent<PlayerRespawn>();
        if (p != null && p.enabled)
        {
            p.SetCheckpoint(index, transform.position + Vector3.up * 0.3f);
            if (RouteProgress.Instance != null) RouteProgress.Instance.ReportCheckpoint(index);
        }
    }
}
