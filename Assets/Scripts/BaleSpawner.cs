using UnityEngine;

// Balya yigini: gorsel yigin olusturur, Take() ile sonsuz yeni balya verir.
public class BaleSpawner : MonoBehaviour
{
    public GameObject balePrefab;
    [Header("Gorsel yigin")]
    public int pileColumns = 3;
    public int pileRows = 3;
    public int pileLayers = 2;

    void Start()
    {
        if (balePrefab == null) return;
        Vector3 size = balePrefab.transform.localScale;
        for (int l = 0; l < pileLayers; l++)
            for (int r = 0; r < pileRows; r++)
                for (int c = 0; c < pileColumns; c++)
                {
                    Vector3 local = new Vector3((c - (pileColumns - 1) / 2f) * (size.x + 0.05f),
                                                size.y / 2f + l * size.y,
                                                (r - (pileRows - 1) / 2f) * (size.z + 0.05f));
                    var b = Instantiate(balePrefab, transform.position + local, transform.rotation, transform);
                    b.name = "YiginBalyasi";
                    var rb = b.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;
                }
    }

    public GameObject Take()
    {
        return balePrefab == null ? null : Instantiate(balePrefab);
    }
}
