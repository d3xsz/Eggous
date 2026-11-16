using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 5.4 Sade sonuc ekrani: kazanan + neden + senin sonucun + tum roller acilir. Puan YOK (PROGRESS karari).
// R: ayni sahneyi bastan yukler. Gecici OnGUI (Faz 6'da uGUI'ye cevrilecek).
public class ResultScreen : MonoBehaviour
{
    GUIStyle title, sub, row, foot, you;

    void Awake() { GameManager.ExternalResultUI = true; }
    void OnDestroy() { GameManager.ExternalResultUI = false; }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Sonuc) return;
        if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            var nmx = Unity.Netcode.NetworkManager.Singleton;
            if (nmx != null) { nmx.Shutdown(); Destroy(nmx.gameObject); }
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public static bool Won(Role r, Winner w)
    {
        switch (w)
        {
            case Winner.Tirmanici: return r == Role.Tirmanici;
            case Winner.Safeler: return r == Role.Safe || r == Role.Tirmanici;
            case Winner.Sabotajci: return r == Role.Sabotajci;
            case Winner.Palyaco: return r == Role.Palyaco;
        }
        return false;
    }

    static Color RoleColor(Role r)
    {
        switch (r)
        {
            case Role.Sabotajci: return new Color(1f, 0.4f, 0.4f);
            case Role.Tirmanici: return new Color(1f, 0.75f, 0.25f);
            case Role.Palyaco: return new Color(1f, 0.55f, 1f);
            default: return Color.white;
        }
    }

    public static string Reason(GameManager gm)
    {
        string who = gm.hatcheryReacher != null ? gm.hatcheryReacher.name : "?";
        switch (gm.winner)
        {
            case Winner.Tirmanici: return "Kuluckaya ilk ulasan: " + who + " (Tirmanici)";
            case Winner.Safeler:
                return "Kuluckaya ilk ulasan: " + who + " (" + RoleManager.DisplayName(RoleManager.GetRole(gm.hatcheryReacher)) + ")";
            case Winner.Sabotajci: return "Sure doldu, kimse kuluckaya ulasamadi";
            case Winner.Palyaco: return "Palyaco oylamayla asildi";
        }
        return "";
    }

    void Init()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 44, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        title.normal.textColor = Color.white;
        sub = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
        sub.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        row = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        foot = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        foot.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        you = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
    }

    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Sonuc) return;
        if (GameHUD.Active) return; Init();

        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = Screen.width;
        GUI.Label(new Rect(0, 50, w, 70), "Kazanan: " + GameManager.WinnerName(gm.winner), title);
        GUI.Label(new Rect(0, 125, w, 40), Reason(gm), sub);

        var human = PlayerController.LocalObject();
        if (human != null && RoleManager.HasRole(human))
        {
            Role r = RoleManager.GetRole(human);
            bool won = Won(r, gm.winner);
            you.normal.textColor = won ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.45f, 0.45f);
            GUI.Label(new Rect(0, 185, w, 50), "Senin rolun: " + RoleManager.DisplayName(r) + "  —  " + (won ? "KAZANDIN!" : "KAYBETTIN"), you);
        }

        var rm = RoleManager.Instance;
        if (rm != null)
        {
            float y = 265f;
            GUI.Label(new Rect(0, y, w, 36), "Roller acildi", sub);
            y += 42f;
            foreach (var e in rm.assignments)
            {
                row.normal.textColor = RoleColor(e.role);
                string who = e.name + (PlayerController.Local != null && e.name == PlayerController.Local.name ? "  (sen)" : "");
                GUI.Label(new Rect(w / 2f - 320, y, 640, 30), who + "  —  " + RoleManager.DisplayName(e.role), row);
                y += 32f;
            }
        }

        GUI.Label(new Rect(0, Screen.height - 70, w, 40), "R: lobiye don", foot);
    }
}
