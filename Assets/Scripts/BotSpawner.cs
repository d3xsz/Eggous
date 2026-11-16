using UnityEngine;

// Botlari uretir. Toplam oyuncu sayisi (insan + bot) Inspector'dan 4-8 secilir (menu 6.5'te baglanacak).
public class BotSpawner : MonoBehaviour
{
    public GameObject botPrefab;
    [Range(4, 8)] public int totalPlayers = 4;
    public int humanPlayers = 1;
    public float ringRadius = 3.5f;
    public Color[] colors = new Color[]
    {
        new Color(0.95f, 0.95f, 0.9f), new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.95f), new Color(0.3f, 0.8f, 0.4f),
        new Color(0.95f, 0.8f, 0.2f), new Color(0.7f, 0.4f, 0.9f), new Color(0.95f, 0.55f, 0.2f), new Color(0.3f, 0.85f, 0.85f)
    };

    void Start()
    {
        if (botPrefab == null) return;
        var human = FindFirstObjectByType<PlayerController>();
        Vector3 origin = human != null ? human.transform.position : transform.position;
        Vector3 center = origin + Vector3.left * ringRadius; // insan oyuncu halkanin 0 derece noktasinda
        int bots = Mathf.Max(0, totalPlayers - humanPlayers);
        int slots = bots + humanPlayers;
        for (int i = 0; i < bots; i++)
        {
            float ang = Mathf.Deg2Rad * 360f * (i + 1) / slots;
            Vector3 pos = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * ringRadius;
            var b = Instantiate(botPrefab, pos, Quaternion.identity);
            b.name = "Bot " + (i + 1);
            var r = b.GetComponentInChildren<Renderer>();
            if (r != null && colors.Length > 0) r.material.SetColor("_BaseColor", colors[(i + 1) % colors.Length]);
            if (TurnManager.Instance != null) TurnManager.Instance.Register(b);
        }
    }
}
