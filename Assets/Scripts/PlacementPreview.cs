using UnityEngine;
using UnityEngine.InputSystem;

// Yerlestirme onizlemesi: yesil=gecerli, kirmizi=gecersiz. Sol tik yerlestirir, balya SABIT kalir (BaleState).
// Dondurme: fare tekerlegi ya da Q / R (E tusu birakma icin kullaniliyor).
[RequireComponent(typeof(BaleCarrier))]
public class PlacementPreview : MonoBehaviour
{
    public Material validMat;
    public Material invalidMat;
    public float placeDistance = 5f;
    public float rotationStep = 15f;
    [Header("Opsiyonel (varsayilan kapali, balya sabit oldugu icin gerekmiyor)")]
    public bool snapToSupport = true;
    public bool requireStable = true;
    public bool freezeOnPlace = false; // true: yerlesen balya sabit kalir
    public float settleTime = 0.4f;
    public static event System.Action<GameObject> BalePlaced;
    public bool isBot; // bot: fare/klavye okumaz, BotController PlaceAt cagirir // 3.2 TurnManager dinler

    BaleCarrier carrier;
    GameObject ghost;
    Renderer ghostRend;
    float offset;
    bool valid;
    Vector3 pos;
    Quaternion rot;
    int carryFrame = -1; // balyayi aldigi karede yerlestirmesin

    void Awake() { carrier = GetComponent<BaleCarrier>(); }

    void Update() { if (isBot) return; UpdateHuman(); }

    void UpdateHuman()
    {
        if (!carrier.IsCarrying || !TurnManager.CanAct(gameObject))
        {
            if (ghost != null && ghost.activeSelf) ghost.SetActive(false);
            carryFrame = -1;
            return;
        }
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse == null || kb == null) return;

        if (carryFrame < 0) carryFrame = Time.frameCount;
        var cam = carrier.cam;
        Vector3 size = carrier.HeldBale.transform.localScale;
        Vector3 half = size * 0.5f;
        EnsureGhost(size);

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll > 0.1f) offset += rotationStep;
        else if (scroll < -0.1f) offset -= rotationStep;
        if (kb.qKey.wasPressedThisFrame) offset += rotationStep;
        if (kb.rKey.wasPressedThisFrame) offset -= rotationStep;

        float baseYaw = Mathf.Round(transform.eulerAngles.y / rotationStep) * rotationStep;
        rot = Quaternion.Euler(0f, baseYaw + offset, 0f);

        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, placeDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 n = hit.normal;
            float h = Mathf.Abs(Vector3.Dot(rot * Vector3.right, n)) * half.x
                    + Mathf.Abs(Vector3.Dot(rot * Vector3.up, n)) * half.y
                    + Mathf.Abs(Vector3.Dot(rot * Vector3.forward, n)) * half.z;
            pos = hit.point + n * (h + 0.01f);
            bool supported = true;
            if (snapToSupport && n.y < 0.7f && n.y > -0.5f)
            {
                Vector3 st = pos + Vector3.up * 0.05f;
                if (Physics.BoxCast(st, half * 0.98f, Vector3.down, out var dh, rot, 8f, ~0, QueryTriggerInteraction.Ignore))
                    pos = st + Vector3.down * dh.distance + Vector3.up * 0.005f;
                else supported = false;
            }
            valid = supported && n.y > -0.5f && !Physics.CheckBox(pos, half * 0.98f, rot, ~0, QueryTriggerInteraction.Ignore);
        }
        else
        {
            pos = cam.transform.position + cam.transform.forward * placeDistance;
            valid = false;
        }

        if (valid && requireStable && !IsStable(pos, rot, half)) valid = false;
        ghost.transform.SetPositionAndRotation(pos, rot);
        ghostRend.sharedMaterial = valid ? validMat : invalidMat;

        if (valid && Cursor.lockState == CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame && Time.frameCount > carryFrame)
            Place();
    }

    // Opsiyonel denge kontrolu (altindan 9x5 isin)
    bool IsStable(Vector3 p, Quaternion r, Vector3 half)
    {
        float minX = 9f, maxX = -9f, minZ = 9f, maxZ = -9f;
        int count = 0;
        for (int i = -4; i <= 4; i++)
            for (int j = -2; j <= 2; j++)
            {
                float lx = half.x * i / 4f, lz = half.z * j / 2f;
                Vector3 o = p + r * new Vector3(lx, -half.y + 0.05f, lz);
                if (Physics.Raycast(o, Vector3.down, 0.15f, ~0, QueryTriggerInteraction.Ignore))
                {
                    count++;
                    if (lx < minX) minX = lx;
                    if (lx > maxX) maxX = lx;
                    if (lz < minZ) minZ = lz;
                    if (lz > maxZ) maxZ = lz;
                }
            }
        if (count == 0) return false;
        float mx = half.x * 0.5f, mz = half.z * 0.5f;
        return minX <= -mx && maxX >= mx && minZ <= -mz && maxZ >= mz;
    }

    System.Collections.IEnumerator Settle(Rigidbody r)
    {
        yield return new WaitForSeconds(settleTime);
        if (r != null) r.constraints = RigidbodyConstraints.None;
    }

    void EnsureGhost(Vector3 size)
    {
        if (ghost == null)
        {
            ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghost.name = "HayaletBalya";
            Destroy(ghost.GetComponent<Collider>());
            ghostRend = ghost.GetComponent<Renderer>();
            ghostRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        ghost.transform.localScale = size;
        if (!ghost.activeSelf) ghost.SetActive(true);
    }

    public void PlaceAt(Vector3 p, Quaternion r) { pos = p; rot = r; Place(); }

    void Place()
    {
        if (GameManager.NetMode && NetGame.Instance != null)   // ag: balyayi sunucu dogurur ve fizigi orada calisir
        {
            var held = carrier.Release();
            if (held == null) return;
            Destroy(held);
            NetGame.Instance.PlaceBale(pos, rot);
            if (BalePlaced != null) BalePlaced(gameObject);
            if (ghost != null) ghost.SetActive(false);
            return;
        }
        var b = carrier.Release();
        if (b == null) return;
        b.transform.SetPositionAndRotation(pos, rot);
        foreach (var c in b.GetComponentsInChildren<Collider>()) c.enabled = true;
        var rb = b.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.None;
            rb.detectCollisions = true;
        }
        if (BalePlaced != null) BalePlaced(gameObject);
        var st = b.GetComponent<BaleState>();
        if (st == null) st = b.AddComponent<BaleState>();
        st.placedBy = gameObject;
        if (BaleTracker.Instance != null) BaleTracker.Instance.RegisterPlaced(st);
        if (freezeOnPlace) st.Freeze();
        else
        {
            st.placed = true;
            if (rb != null)
            {
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                StartCoroutine(Settle(rb));
            }
        }
        if (ghost != null) ghost.SetActive(false);
    }
}
