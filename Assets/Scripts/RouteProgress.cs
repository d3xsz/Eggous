using UnityEngine;

// Rota ilerlemesi: ulasilan en son checkpoint sayisi + kulucka. UI buradan okur.
public class RouteProgress : MonoBehaviour
{
    public static RouteProgress Instance;
    public int totalCheckpoints = 2;
    public int reached = 0;
    public bool hatcheryReached = false;
    public event System.Action Changed;

    void Awake() { Instance = this; }

    public float Normalized
    {
        get { return hatcheryReached ? 1f : (float)reached / (totalCheckpoints + 1); }
    }

    public void ReportCheckpoint(int index)
    {
        if (index <= reached) return;
        reached = index;
        if (Changed != null) Changed();
    }

    public void ReportHatchery()
    {
        if (hatcheryReached) return;
        hatcheryReached = true;
        if (Changed != null) Changed();
    }
}
