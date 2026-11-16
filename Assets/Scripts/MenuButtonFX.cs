using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Menu tabelasi: fareyle ustune gelince tabeladaki yazi/ikon hafifce kararir, basinca daha koyu olur.
public class MenuButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public RawImage ink;                 // tabela piksellerinin kopyasi (shader sadece yazi piksellerini koyulastirir)
    public float hoverAmount = 0.7f, pressAmount = 0.95f;
    bool hover, down;
    Material mat;
    float cur;

    void Awake()
    {
        if (ink != null) { mat = new Material(ink.material); ink.material = mat; mat.SetFloat("_Amount", 0f); }
    }

    public void OnPointerEnter(PointerEventData e) { hover = true; }
    public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
    public void OnPointerDown(PointerEventData e) { down = true; }
    public void OnPointerUp(PointerEventData e) { down = false; }

    void Update()
    {
        if (mat == null) return;
        float target = down ? pressAmount : hover ? hoverAmount : 0f;
        cur = Mathf.Lerp(cur, target, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
        mat.SetFloat("_Amount", cur);
    }
}
