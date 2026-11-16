using UnityEngine;

// Gol yuzeyi: iki saydam su katmaninin normal haritasini farkli yonlere kaydirir (dalga/parlama hissi).
public class WaterSurface : MonoBehaviour
{
    public Renderer layerA, layerB;
    public Vector2 speedA = new Vector2(0.020f, 0.012f);
    public Vector2 speedB = new Vector2(-0.015f, 0.022f);
    Material mA, mB;
    Vector2 oA, oB;
    float baseYB;

    void Start()
    {
        if (layerA != null) mA = layerA.material;   // kopya: asset bozulmaz
        if (layerB != null) { mB = layerB.material; baseYB = layerB.transform.localPosition.y; }
    }

    void Update()
    {
        oA += speedA * Time.deltaTime; oB += speedB * Time.deltaTime;
        if (mA != null) mA.SetTextureOffset("_BumpMap", oA);
        if (mB != null) mB.SetTextureOffset("_BumpMap", oB);
        if (layerB != null)
        {
            var p = layerB.transform.localPosition;
            p.y = baseYB + Mathf.Sin(Time.time * 0.9f) * 0.015f;
            layerB.transform.localPosition = p;
        }
    }
}
