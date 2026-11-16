using UnityEngine;

// Kulucka hedefi: ilk giren oyuncu kuluckaya ulasmis sayilir (kazanan belirleme 4.6'da).
public class Hatchery : MonoBehaviour
{
    public static event System.Action<GameObject> Reached;
    public bool reached;

    void OnTriggerEnter(Collider other)
    {
        if (reached) return;
        var p = other.GetComponentInParent<PlayerRespawn>();
        if (p == null) return;
        if (!p.enabled) return;   // ag: uzaktaki oyuncunun kopyasi sayilmaz
        reached = true;
        if (RouteProgress.Instance != null) RouteProgress.Instance.ReportHatchery();
        if (Reached != null) Reached(p.gameObject);
        Debug.Log("Kuluckaya ulasildi: " + p.name);
    }
}
