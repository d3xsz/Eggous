using UnityEngine;

// Yumurta botlarin KOLLARI: eller yanlarda sarkar, balya tasirken one uzanip balyanin yanlarini tutar (sevimli 'lastik kol').
// Kol/el parcalari Bot.prefab'ta hazir (KolSol, KolSag, ElSol, ElSag); hepsi govde (Govde) uzayinda hesaplanir,
// boylece vurulup devrilince kollar da govdeyle birlikte doner (PlayerCombat'in GovdePivot'u).
public class EggArms : MonoBehaviour
{
    public Vector3 shoulderLocal = new Vector3(0.46f, 0.85f, 0.02f);   // x isareti sol/sag icin cevrilir
    public Vector3 idleHand = new Vector3(0.64f, 0.38f, 0.06f);
    public Vector3 carryHand = new Vector3(0.64f, 0.95f, 1.05f);       // balyanin yan yuzleri
    public float armRadius = 0.09f;
    public float handSize = 0.17f;
    public float blendSpeed = 6f;
    public float swing = 0.14f;                                        // yururken kol sallanmasi

    Transform body;
    Transform[] arm = new Transform[2];
    Transform[] hand = new Transform[2];
    BaleCarrier carrier;
    CharacterController cc;
    Vector3 lastPos;
    float carry;

    void Start()
    {
        carrier = GetComponent<BaleCarrier>();
        cc = GetComponent<CharacterController>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Govde") body = t;
            else if (t.name == "KolSol") arm[0] = t;
            else if (t.name == "KolSag") arm[1] = t;
            else if (t.name == "ElSol") hand[0] = t;
            else if (t.name == "ElSag") hand[1] = t;
        }
        if (body == null) { enabled = false; return; }
        // kol/el rengi govdeyle ayni (BotSpawner govdeyi boyadiktan sonra)
        var bodyMat = body.GetComponent<Renderer>().sharedMaterial;
        for (int i = 0; i < 2; i++)
        {
            if (arm[i] != null) arm[i].GetComponent<Renderer>().sharedMaterial = bodyMat;
            if (hand[i] != null) hand[i].GetComponent<Renderer>().sharedMaterial = bodyMat;
        }
    }

    void LateUpdate()
    {
        if (body == null) return;
        bool carrying = carrier != null && carrier.IsCarrying;
        carry = Mathf.MoveTowards(carry, carrying ? 1f : 0f, Time.deltaTime * blendSpeed);
        float e = Mathf.SmoothStep(0f, 1f, carry);
        Vector3 dp = transform.position - lastPos; dp.y = 0f; lastPos = transform.position;
        float vel = (cc != null && cc.enabled) ? cc.velocity.magnitude : (Time.deltaTime > 0f ? dp.magnitude / Time.deltaTime : 0f);
        float speed = Mathf.Clamp01(vel / 6f);
        float sway = Mathf.Sin(Time.time * 9f) * swing * speed * (1f - e);

        for (int i = 0; i < 2; i++)
        {
            if (arm[i] == null || hand[i] == null) continue;
            float s = i == 0 ? -1f : 1f;
            Vector3 sh = new Vector3(shoulderLocal.x * s, shoulderLocal.y, shoulderLocal.z);
            Vector3 idle = new Vector3(idleHand.x * s, idleHand.y, idleHand.z + sway * s);
            Vector3 car = new Vector3(carryHand.x * s, carryHand.y, carryHand.z);
            Vector3 h = Vector3.Lerp(idle, car, e);

            Vector3 shW = body.TransformPoint(sh);
            Vector3 hW = body.TransformPoint(h);
            Vector3 d = hW - shW;
            float len = d.magnitude;
            if (len < 0.01f) continue;

            arm[i].position = shW + d * 0.5f;
            arm[i].rotation = Quaternion.LookRotation(d) * Quaternion.Euler(90f, 0f, 0f);
            arm[i].localScale = new Vector3(armRadius * 2f, len * 0.5f, armRadius * 2f);
            hand[i].position = hW;
            hand[i].localScale = Vector3.one * handSize;
        }
    }
}
