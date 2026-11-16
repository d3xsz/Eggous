using UnityEngine;
using UnityEngine.UI;

// Rota ilerleme bari (uGUI): dolgu genisligi RouteProgress.Normalized'a gore degisir.
public class RouteProgressUI : MonoBehaviour
{
    public RectTransform fill;
    public Text label;

    void Start()
    {
        if (RouteProgress.Instance != null) RouteProgress.Instance.Changed += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (RouteProgress.Instance != null) RouteProgress.Instance.Changed -= Refresh;
    }

    void Refresh()
    {
        var r = RouteProgress.Instance;
        if (r == null) return;
        fill.anchorMax = new Vector2(Mathf.Clamp01(r.Normalized), 1f);
        label.text = r.hatcheryReached ? "Kuluckaya ulasildi!" : "Rota: " + r.reached + " / " + r.totalCheckpoints + " checkpoint";
    }
}
