using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

// Host / Katil ekrani + bekleme odasi (OnGUI). Test amacli: IP + port ile baglanir.
// Host ENTER'a basana kadar oyun DURUR (GameManager Menu durumunda kalir).
public class NetBootstrap : MonoBehaviour
{
    public string ip = "127.0.0.1";
    public string portText = "7777";
    public GameObject netGamePrefab;            // NetGame.prefab (host dogurur)
    string msg = "";
    string myIps = "";
    GUIStyle box, title, lbl, btn, field, small;
    GameObject lobbyCam;

    void Awake()
    {
        Application.runInBackground = true;   // coklu pencere testinde arka plandaki pencere de calissin
        GameManager.NetMode = true;
        GameManager.NetStarted = false;
        GameManager.NetIsServer = false;
        NetGame.Started = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        try
        {
            foreach (var a in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (a.AddressFamily == AddressFamily.InterNetwork) myIps += (myIps.Length > 0 ? "   " : "") + a;
        }
        catch { }
    }

    void Start()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null) nm.OnClientDisconnectCallback += OnDisc;
        UpdateLobbyCam();
    }

    void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null) nm.OnClientDisconnectCallback -= OnDisc;
    }

    void Update()
    {
        UpdateLobbyCam();
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsServer && !NetGame.Started && NetGame.Instance != null)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.enterKey.wasPressedThisFrame) NetGame.Instance.HostStart();
        }
    }

    // Oyuncu yokken (lobi) sahneyi gostermek icin gecici kamera; yerel oyuncu dogunca kaldirilir
    void UpdateLobbyCam()
    {
        bool need = PlayerController.Local == null;
        if (need && lobbyCam == null)
        {
            lobbyCam = new GameObject("LobiKamera");
            lobbyCam.AddComponent<Camera>();
            lobbyCam.AddComponent<AudioListener>();
            lobbyCam.transform.position = new Vector3(3f, 7f, -14f);
            lobbyCam.transform.LookAt(new Vector3(3f, 1.5f, -1f));
        }
        else if (!need && lobbyCam != null) { Destroy(lobbyCam); lobbyCam = null; }
    }

    void OnDisc(ulong id)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        if (!nm.IsServer && id == nm.LocalClientId)
        {
            string r = nm.DisconnectReason;
            msg = "Baglanti yok/koptu" + (string.IsNullOrEmpty(r) ? "" : ": " + r);
            nm.Shutdown();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    ushort Port() { ushort p; return ushort.TryParse(portText, out p) ? p : (ushort)7777; }

    // Oyun basladiysa veya oda doluysa yeni baglantiyi reddet
    void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
    {
        var nm = NetworkManager.Singleton;
        bool ok = !NetGame.Started && nm.ConnectedClientsIds.Count < 8;
        res.Approved = ok;
        res.CreatePlayerObject = ok;
        if (!ok) res.Reason = NetGame.Started ? "Oyun basladi" : "Oda dolu";
    }

    void DoHost()
    {
        var nm = NetworkManager.Singleton;
        var utp = nm.GetComponent<UnityTransport>();
        utp.SetConnectionData("127.0.0.1", Port(), "0.0.0.0");
        nm.ConnectionApprovalCallback = Approve;
        if (nm.StartHost())
        {
            msg = "";
            if (netGamePrefab != null)
            {
                var g = Instantiate(netGamePrefab);
                g.GetComponent<NetworkObject>().Spawn();
            }
            else msg = "NetGame prefab eksik!";
        }
        else msg = "Host baslatilamadi (port dolu olabilir)";
    }

    void DoJoin()
    {
        var nm = NetworkManager.Singleton;
        var utp = nm.GetComponent<UnityTransport>();
        utp.SetConnectionData(ip.Trim(), Port());
        utp.MaxConnectAttempts = 6;
        msg = "Baglaniliyor...";
        if (!nm.StartClient()) msg = "Istemci baslatilamadi";
    }

    void EnsureStyles(float s)
    {
        if (box == null)
        {
            box = new GUIStyle(GUI.skin.box);
            title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            lbl = new GUIStyle(GUI.skin.label);
            btn = new GUIStyle(GUI.skin.button);
            field = new GUIStyle(GUI.skin.textField);
            small = new GUIStyle(GUI.skin.label);
            title.normal.textColor = Color.white; lbl.normal.textColor = Color.white; small.normal.textColor = Color.white;
        }
        title.fontSize = Mathf.RoundToInt(44 * s); lbl.fontSize = Mathf.RoundToInt(24 * s);
        btn.fontSize = Mathf.RoundToInt(30 * s); field.fontSize = Mathf.RoundToInt(28 * s); small.fontSize = Mathf.RoundToInt(20 * s);
    }

    void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        GUI.depth = -100;
        float s = Screen.height / 1080f;
        EnsureStyles(s);

        if (nm.IsListening && (nm.IsServer || nm.IsConnectedClient))
        {
            int n = FindObjectsByType<NetPlayer>(FindObjectsSortMode.None).Length;
            string t = (nm.IsHost ? "HOST" : "ISTEMCI") + "  |  oyuncu: " + n + (nm.IsHost && myIps.Length > 0 ? "  |  IP: " + myIps : "");
            GUI.Label(new Rect(14 * s, Screen.height - 40 * s, 1200 * s, 30 * s), t, small);

            if (!NetGame.Started)
            {
                float w = 820 * s, h = 190 * s;
                var r = new Rect((Screen.width - w) / 2f, 50 * s, w, h);
                GUI.Box(r, GUIContent.none, box);
                GUILayout.BeginArea(new Rect(r.x + 24 * s, r.y + 16 * s, w - 48 * s, h - 32 * s));
                GUILayout.Label("BEKLEME ODASI", title);
                GUILayout.Label("Odadaki oyuncu: " + n, lbl);
                GUILayout.Label(nm.IsServer ? "ENTER: oyunu baslat (herkes baglaninca)" : "Host oyunu baslatinca tur baslar...", lbl);
                GUILayout.EndArea();
            }
            return;
        }

        float pw = 680 * s, ph = 620 * s;
        var pr = new Rect((Screen.width - pw) / 2f, (Screen.height - ph) / 2f, pw, ph);
        GUI.Box(pr, GUIContent.none, box);
        GUILayout.BeginArea(new Rect(pr.x + 34 * s, pr.y + 26 * s, pw - 68 * s, ph - 52 * s));
        GUILayout.Label("COK OYUNCULU", title);
        GUILayout.Space(14 * s);

        if (nm.IsClient && !nm.IsConnectedClient)
        {
            GUILayout.Label("Baglaniliyor: " + ip + ":" + portText, lbl);
            GUILayout.Space(10 * s);
            if (GUILayout.Button("IPTAL", btn, GUILayout.Height(60 * s))) { nm.Shutdown(); msg = ""; }
        }
        else
        {
            GUILayout.Label("Sunucu IP (katilmak icin)", lbl);
            ip = GUILayout.TextField(ip, field, GUILayout.Height(54 * s));
            GUILayout.Space(8 * s);
            GUILayout.Label("Port", lbl);
            portText = GUILayout.TextField(portText, field, GUILayout.Height(54 * s));
            GUILayout.Space(20 * s);
            if (GUILayout.Button("HOST  (sunucu ac)", btn, GUILayout.Height(66 * s))) DoHost();
            GUILayout.Space(10 * s);
            if (GUILayout.Button("KATIL", btn, GUILayout.Height(66 * s))) DoJoin();
        }
        GUILayout.Space(12 * s);
        if (!string.IsNullOrEmpty(msg)) GUILayout.Label(msg, lbl);
        if (myIps.Length > 0) GUILayout.Label("Bu bilgisayarin IP'leri: " + myIps, small);
        GUILayout.EndArea();
    }
}
