using UnityEngine;

public enum GameState { Menu, Tur, Oylama, Sonuc }
public enum Winner { None, Tirmanici, Safeler, Sabotajci, Palyaco }

// Oyun durumu + sure + kazanan (4.6). Kazanma tablosu: PROGRESS 'YENI KARARLAR'.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public static bool ExternalResultUI; // ResultScreen varsa basit 'Kazanan' yazisi cizilmez
    public static event System.Action<GameState, GameState> StateChanged;
    public static event System.Action<Winner> RoundEnded;

    public GameState startState = GameState.Tur;
    public GameState State { get; private set; } = GameState.Menu;

    [Header("Sure (sadece Tur durumunda akar, oylamada durur)")]
    public float roundSeconds = 900f;
    public float timeLeft;

    [Header("Sonuc")]
    public GameObject hatcheryReacher;
    public Winner winner = Winner.None;

    public bool IsPlaying { get { return State == GameState.Tur; } }

    // Ag: karar sadece sunucuda; host 'baslat'a basana kadar tur calismaz
    public static bool NetMode, NetIsServer, NetStarted;
    void Awake() { Instance = this; }
    void OnEnable() { Hatchery.Reached += OnHatcheryReached; }
    void OnDisable() { Hatchery.Reached -= OnHatcheryReached; }
    void Start() { timeLeft = roundSeconds; SetState(NetMode ? GameState.Menu : startState); }

    void Update()
    {
        if (State != GameState.Tur) return;
        if (NetMode && !NetStarted) return;
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) { timeLeft = 0f; if (!NetMode || NetIsServer) Finish(Winner.Sabotajci); }
    }

    public void SetState(GameState next)
    {
        if (next == State && Time.frameCount > 1) return;
        var old = State;
        State = next;
        Debug.Log("[GameManager] " + old + " -> " + next);
        if (StateChanged != null) StateChanged(old, next);
    }

    public void StartRound() { hatcheryReacher = null; winner = Winner.None; timeLeft = roundSeconds; SetState(GameState.Tur); }
    public void StartVote() { if (State == GameState.Tur) SetState(GameState.Oylama); }
    public void EndVote() { if (State == GameState.Oylama) SetState(GameState.Tur); }
    public void EndRound() { SetState(GameState.Sonuc); }

    // Tur biter, kazanan belirlenir (4.4 oylama: palyaco asilirsa Finish(Winner.Palyaco))
    public void Finish(Winner w) { Finish(w, false); }
    public void Finish(Winner w, bool fromNet)
    {
        if (NetMode && !NetIsServer && !fromNet) return;   // ag: sadece sunucu karar verir
        if (State == GameState.Sonuc) return;
        winner = w;
        Debug.Log("[GameManager] Kazanan: " + w);
        EndRound();
        if (RoundEnded != null) RoundEnded(w);
        if (NetMode && NetIsServer && !fromNet && NetGame.Instance != null) NetGame.Instance.OnServerFinished(w, hatcheryReacher);
    }

    void OnHatcheryReached(GameObject player)
    {
        if (NetMode) return;   // ag: kuluckaya varisi NetGame karar verir
        if (State != GameState.Tur) return;
        hatcheryReacher = player;
        Finish(RoleManager.GetRole(player) == Role.Tirmanici ? Winner.Tirmanici : Winner.Safeler);
    }

    public static string WinnerName(Winner w)
    {
        switch (w)
        {
            case Winner.Tirmanici: return "Sahanda (Tirmanici)";
            case Winner.Safeler: return "Haslanmis Yumurtalar (Safeler)";
            case Winner.Sabotajci: return "Curuk Yumurta (Sabotajci)";
            case Winner.Palyaco: return "Omlet (Palyaco)";
            default: return "-";
        }
    }

    // Gecici basit UI (Faz 6'da guzellestirilecek)
    void OnGUI()
    {
        if (State == GameState.Tur || State == GameState.Oylama)
        {
            int m = Mathf.FloorToInt(timeLeft / 60f);
            int s = Mathf.FloorToInt(timeLeft % 60f);
            var st = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.UpperRight };
            if (!GameHUD.Active) GUI.Label(new Rect(Screen.width - 210, 10, 200, 40), "Sure " + m.ToString("00") + ":" + s.ToString("00"), st);
        }
        else if (State == GameState.Sonuc && !ExternalResultUI)
        {
            var big = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height / 2f - 60, Screen.width, 120), "Kazanan: " + WinnerName(winner), big);
        }
    }
}
