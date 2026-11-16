using System.Collections.Generic;
using UnityEngine;

// Sira sistemi: oyuncular sirayla birer balya yerlestirir. Sira, yerlestirme (PlacementPreview.BalePlaced) ile ilerler.
// Katilimcilar: Start'ta sahnedeki tum BaleCarrier'lar otomatik eklenir; sonradan gelenler (botlar, 3.3) Register ile eklenir.
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;
    public static event System.Action<GameObject> TurnChanged;

    public List<GameObject> players = new List<GameObject>();
    public int index;
    [Tooltip("false: herkes serbest, ayni anda balya alip koyar (karar). true: sadece siradaki oyuncu.")]
    public bool enforceTurns = false;

    public GameObject Current
    {
        get { return (players.Count == 0) ? null : players[Mathf.Clamp(index, 0, players.Count - 1)]; }
    }

    // Balya alma/yerlestirme izni: tur oynanmiyorsa kimse, siradaki yoksa herkes, yoksa sadece siradaki
    public static bool CanAct(GameObject who)
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return false;
        var t = Instance;
        if (t == null || !t.enforceTurns || t.players.Count == 0) return true;
        return t.Current == who;
    }

    void Awake() { Instance = this; }

    void OnEnable()
    {
        PlacementPreview.BalePlaced += OnBalePlaced;
        GameManager.StateChanged += OnStateChanged;
    }

    void OnDisable()
    {
        PlacementPreview.BalePlaced -= OnBalePlaced;
        GameManager.StateChanged -= OnStateChanged;
    }

    void Start()
    {
        foreach (var c in FindObjectsByType<BaleCarrier>(FindObjectsSortMode.None)) Register(c.gameObject);
        index = 0;
        Notify();
    }

    public void Register(GameObject p)
    {
        if (p != null && !players.Contains(p)) players.Add(p);
    }

    // Elenen oyuncu icin (4.4)
    public void Unregister(GameObject p)
    {
        int i = players.IndexOf(p);
        if (i < 0) return;
        players.RemoveAt(i);
        if (players.Count == 0) { index = 0; Notify(); return; }
        if (i < index) index--;
        if (index >= players.Count) index = 0;
        if (i == index || i < index) Notify();
    }

    public void NextTurn()
    {
        if (players.Count == 0) return;
        index = (index + 1) % players.Count;
        Notify();
    }

    void OnBalePlaced(GameObject who)
    {
        if (who == Current) NextTurn();
    }

    void OnStateChanged(GameState old, GameState now)
    {
        // Yeni tur baslayinca siradan basla (oylamadan donuste sira korunur)
        if (now == GameState.Tur && old != GameState.Oylama) { index = 0; Notify(); }
    }

    void Notify()
    {
        if (TurnChanged != null) TurnChanged(Current);
        if (Current != null) Debug.Log("[TurnManager] Sira: " + Current.name);
    }

    void OnGUI()
    {
        if (Current == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        if (enforceTurns) GUI.Label(new Rect(10, 10, 300, 30), "Sira: " + Current.name);
    }
}
