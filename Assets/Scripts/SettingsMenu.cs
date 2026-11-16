using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

// ESC ayar menusu (uGUI, calisma aninda kurulur). GameHUD ile ayni gorsel dil.
// Ana ses, fare hassasiyeti, tam ekran. Tek oyunculuda oyun durur; AG OYUNUNDA durmaz (sadece menu acilir).
public class SettingsMenu : MonoBehaviour
{
    public static bool IsOpen;
    static SettingsMenu inst;

    static readonly Color PanelDark = new Color(0.10f, 0.08f, 0.06f, 0.94f);
    static readonly Color Light = new Color(0.97f, 0.94f, 0.87f, 1f);
    static readonly Color Dim = new Color(0.70f, 0.67f, 0.62f, 1f);
    static readonly Color Amber = new Color(0.95f, 0.78f, 0.38f, 1f);
    static readonly Color Alert = new Color(0.95f, 0.30f, 0.26f, 1f);
    static readonly Color DarkInk = new Color(0.12f, 0.08f, 0.03f, 1f);
    static readonly Color TrackC = new Color(0f, 0f, 0f, 0.35f);

    const string KMaster = "set_master", KSens = "set_sens", KFull = "set_full";
    const float SensMin = 0.25f, SensMax = 2.5f;

    float master = 1f, sensMul = 1f; bool full;

    Font font;
    Sprite sFill, sRing, sRound, sCircle;
    GameObject rootGo;
    class Row { public RectTransform hit, track, knob; public Image fill; public Text value; }
    Row rMaster, rSens;
    RectTransform tHit, tKnob; Image tBg; Text tState;
    RectTransform bResume, bQuit; Image bResumeBg, bQuitBg;
    int drag = -1;
    float prevScale = 1f; bool paused; CursorLockMode prevLock; bool prevVis;
    readonly Dictionary<PlayerController, float> baseSens = new Dictionary<PlayerController, float>();
    float nextScan;

    static bool NetActive { get { var nm = Unity.Netcode.NetworkManager.Singleton; return nm != null && nm.IsListening; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (inst != null) return;
        var go = new GameObject("SettingsMenu");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<SettingsMenu>();
    }

    // Ana menuden (MainMenu.Settings) acmak icin
    public static void OpenExternal()
    {
        if (inst == null) Boot();
        if (inst != null && !IsOpen) inst.Open();
    }

    void Awake()
    {
        master = PlayerPrefs.GetFloat(KMaster, 1f);
        sensMul = Mathf.Clamp(PlayerPrefs.GetFloat(KSens, 1f), SensMin, SensMax);
        if (PlayerPrefs.HasKey(KFull)) { full = PlayerPrefs.GetInt(KFull, 0) == 1; Screen.fullScreenMode = full ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed; }
        else full = Screen.fullScreen;
        AudioListener.volume = master;
        SceneManager.sceneLoaded += OnLoaded;
    }

    void OnDestroy() { SceneManager.sceneLoaded -= OnLoaded; if (IsOpen) { IsOpen = false; if (paused) Time.timeScale = 1f; } }

    void OnLoaded(Scene s, LoadSceneMode m)
    {
        if (m != LoadSceneMode.Single) return;
        if (IsOpen) Close(false);
        nextScan = 0f;
    }

    void Save()
    {
        PlayerPrefs.SetFloat(KMaster, master); PlayerPrefs.SetFloat(KSens, sensMul); PlayerPrefs.SetInt(KFull, full ? 1 : 0);
        PlayerPrefs.Save();
    }

    void ApplySens()
    {
        foreach (var kv in baseSens) if (kv.Key != null) kv.Key.mouseSensitivity = kv.Value * sensMul;
    }

    void ScanPlayers()
    {
        var dead = new List<PlayerController>();
        foreach (var kv in baseSens) if (kv.Key == null) dead.Add(kv.Key);
        foreach (var d in dead) baseSens.Remove(d);
        foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (!baseSens.ContainsKey(pc)) { baseSens[pc] = pc.mouseSensitivity; pc.mouseSensitivity = baseSens[pc] * sensMul; }
    }

    void Update()
    {
        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 1f; ScanPlayers(); }

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { if (IsOpen) Close(true); else Open(); return; }
        if (!IsOpen) return;

        if (paused) Time.timeScale = 0f;
        var mouse = Mouse.current; if (mouse == null) return;
        Vector2 mp = mouse.position.ReadValue();
        bool click = mouse.leftButton.wasPressedThisFrame, down = mouse.leftButton.isPressed;

        if (click) { if (Hit(rMaster.hit, mp)) drag = 0; else if (Hit(rSens.hit, mp)) drag = 1; }
        if (!down) { if (drag >= 0) Save(); drag = -1; }
        if (drag >= 0)
        {
            var r = drag == 0 ? rMaster : rSens;
            Vector2 lp;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(r.track, mp, null, out lp);
            float v = Mathf.Clamp01(Mathf.InverseLerp(r.track.rect.xMin, r.track.rect.xMax, lp.x));
            if (drag == 0) { master = v; AudioListener.volume = master; }
            else { sensMul = Mathf.Lerp(SensMin, SensMax, v); ApplySens(); }
            Refresh();
        }
        if (click && Hit(tHit, mp))
        {
            full = !full;
            Screen.fullScreenMode = full ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Refresh(); Save();
        }
        bool hr = Hit(bResume, mp), hq = Hit(bQuit, mp);
        bResumeBg.color = hr ? Color.Lerp(Amber, Color.white, 0.2f) : Amber;
        bQuitBg.color = hq ? new Color(Alert.r, Alert.g, Alert.b, 0.55f) : new Color(1f, 1f, 1f, 0.08f);
        if (click && hr) Close(true);
        if (click && hq) Quit();
    }

    static bool Hit(RectTransform rt, Vector2 mp) { return rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mp, null); }

    void Open()
    {
        if (rootGo == null) Build();
        paused = !NetActive;
        prevScale = Time.timeScale > 0.5f ? Time.timeScale : 1f;
        prevLock = Cursor.lockState; prevVis = Cursor.visible;
        IsOpen = true; drag = -1;
        if (paused) Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Refresh();
        rootGo.SetActive(true);
    }

    void Close(bool restoreCursor)
    {
        Save();
        IsOpen = false; drag = -1;
        if (paused) Time.timeScale = prevScale;
        paused = false;
        if (rootGo != null) rootGo.SetActive(false);
        if (restoreCursor) { Cursor.lockState = prevLock; Cursor.visible = prevVis; }
    }

    void Quit()
    {
        Save();
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Refresh()
    {
        SetSlider(rMaster, master, Mathf.RoundToInt(master * 100f) + "%");
        SetSlider(rSens, Mathf.InverseLerp(SensMin, SensMax, sensMul), Mathf.RoundToInt(sensMul * 100f) + "%");
        tBg.color = full ? Amber : TrackC;
        tKnob.anchoredPosition = new Vector2(full ? 150f - 6f - 26f : 6f + 26f, 0f);
        tState.text = full ? "AÇIK" : "KAPALI";
        tState.color = full ? Amber : Dim;
    }

    static void SetSlider(Row r, float v, string text)
    {
        r.fill.rectTransform.anchorMax = new Vector2(Mathf.Max(v, 0.001f), 1f);
        r.knob.anchorMin = r.knob.anchorMax = new Vector2(v, 0.5f);
        r.knob.anchoredPosition = Vector2.zero;
        r.value.text = text;
    }

    // ================= KURULUM =================
    void Build()
    {
        var hud = FindFirstObjectByType<GameHUD>();
        if (hud != null) font = hud.customFont;
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift SemiBold Condensed", "Bahnschrift SemiBold", "Segoe UI Semibold", "Trebuchet MS" }, 48);
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        sFill = Sd(64, 14f, 0f, 24f); sRing = Sd(64, 14f, 2f, 24f);
        sRound = Sd(64, 32f, 0f, 30f); sCircle = Sd(64, 32f, 0f, 0f);

        var cgo = new GameObject("SettingsCanvas");
        cgo.transform.SetParent(transform, false);
        var cv = cgo.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 40;
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(3200, 1800); sc.matchWidthOrHeight = 0.5f;

        var rt0 = NewRT("Kok", cgo.transform); Stretch(rt0); rootGo = rt0.gameObject;
        var dim = rt0.gameObject.AddComponent<Image>(); dim.color = new Color(0f, 0f, 0f, 0.6f); dim.raycastTarget = false;

        var pr = NewRT("Panel", rt0);
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f); pr.anchoredPosition = Vector2.zero; pr.sizeDelta = new Vector2(1100f, 940f);
        var sh = Slice(pr, "Golge", sFill, new Color(0f, 0f, 0f, 0.25f));
        sh.rectTransform.offsetMin = new Vector2(4, -8); sh.rectTransform.offsetMax = new Vector2(4, -8);
        var fillImg = Slice(pr, "Dolgu", sFill, PanelDark);
        Slice(pr, "Cerceve", sRing, Color.Lerp(Amber, Color.white, 0.25f));
        var f = fillImg.transform;

        Label(f, "Cap", 24, TextAnchor.MiddleCenter, Amber, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(0, -34)).text = "DURAKLATILDI";
        Label(f, "Baslik", 84, TextAnchor.MiddleCenter, Light, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -176), new Vector2(-24, -70), true).text = "AYARLAR";
        var ln = NewRT("Cizgi", f);
        ln.anchorMin = new Vector2(0, 1); ln.anchorMax = new Vector2(1, 1); ln.offsetMin = new Vector2(60, -198); ln.offsetMax = new Vector2(-60, -196);
        var lni = ln.gameObject.AddComponent<Image>(); lni.color = new Color(1f, 1f, 1f, 0.12f); lni.raycastTarget = false;

        rMaster = MakeSlider(f, 230f, "ANA SES");
        rSens = MakeSlider(f, 400f, "FARE HASSASİYETİ");

        var tr = NewRT("Satir_TamEkran", f);
        tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.offsetMin = new Vector2(60, -(570f + 90f)); tr.offsetMax = new Vector2(-60, -570f);
        tHit = tr;
        Label(tr, "Ad", 34, TextAnchor.MiddleLeft, Light, Vector2.zero, new Vector2(0.65f, 1), Vector2.zero, Vector2.zero).text = "TAM EKRAN";
        tState = Label(tr, "Durum", 30, TextAnchor.MiddleRight, Dim, new Vector2(0.45f, 0), Vector2.one, Vector2.zero, new Vector2(-180, 0), true);
        var sw = NewRT("Anahtar", tr);
        sw.anchorMin = sw.anchorMax = sw.pivot = new Vector2(1, 0.5f); sw.anchoredPosition = Vector2.zero; sw.sizeDelta = new Vector2(150, 64);
        tBg = sw.gameObject.AddComponent<Image>(); tBg.sprite = sRound; tBg.type = Image.Type.Sliced; tBg.color = TrackC; tBg.raycastTarget = false;
        tKnob = NewRT("Topuz", sw);
        tKnob.anchorMin = tKnob.anchorMax = new Vector2(0, 0.5f); tKnob.pivot = new Vector2(0.5f, 0.5f); tKnob.sizeDelta = new Vector2(52, 52);
        var tki = tKnob.gameObject.AddComponent<Image>(); tki.sprite = sCircle; tki.color = Light; tki.raycastTarget = false;

        bResume = MakeButton(f, -250f, 700f, "DEVAM ET", DarkInk, Amber, out bResumeBg);
        bQuit = MakeButton(f, 250f, 700f, "OYUNDAN ÇIK", Light, new Color(1f, 1f, 1f, 0.08f), out bQuitBg);

        Label(f, "Ipucu", 22, TextAnchor.MiddleCenter, Dim, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 22), new Vector2(0, 58)).text = "ESC: DEVAM ET";
        rootGo.SetActive(false);
    }

    Row MakeSlider(Transform f, float y, string label)
    {
        var r = new Row();
        var rt = NewRT("Satir_" + label, f);
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.offsetMin = new Vector2(60, -(y + 140f)); rt.offsetMax = new Vector2(-60, -y);
        r.hit = rt;
        Label(rt, "Ad", 34, TextAnchor.MiddleLeft, Light, new Vector2(0, 1), new Vector2(0.65f, 1), new Vector2(0, -60), Vector2.zero).text = label;
        r.value = Label(rt, "Deger", 38, TextAnchor.MiddleRight, Amber, new Vector2(0.65f, 1), new Vector2(1, 1), new Vector2(0, -60), Vector2.zero, true);
        var tr = NewRT("Yol", rt);
        tr.anchorMin = new Vector2(0, 0); tr.anchorMax = new Vector2(1, 0); tr.offsetMin = new Vector2(0, 30); tr.offsetMax = new Vector2(0, 50);
        var bg = tr.gameObject.AddComponent<Image>(); bg.sprite = sRound; bg.type = Image.Type.Sliced; bg.color = TrackC; bg.raycastTarget = false;
        r.track = tr;
        r.fill = Slice(tr, "Dolgu", sRound, Amber);
        var k = NewRT("Topuz", tr);
        k.anchorMin = k.anchorMax = new Vector2(0, 0.5f); k.pivot = new Vector2(0.5f, 0.5f); k.sizeDelta = new Vector2(46, 46);
        var ki = k.gameObject.AddComponent<Image>(); ki.sprite = sCircle; ki.color = Light; ki.raycastTarget = false;
        r.knob = k;
        return r;
    }

    RectTransform MakeButton(Transform f, float x, float y, string text, Color textColor, Color bgColor, out Image bg)
    {
        var rt = NewRT("Buton_" + text, f);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(440, 100);
        bg = Slice(rt, "Dolgu", sFill, bgColor);
        Slice(rt, "Cerceve", sRing, Color.Lerp(bgColor.a > 0.5f ? bgColor : Dim, Color.white, 0.25f));
        Label(rt, "Yazi", 38, TextAnchor.MiddleCenter, textColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        rt.GetComponentInChildren<Text>().text = text;
        return rt;
    }

    static RectTransform NewRT(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }

    static Image Slice(Transform parent, string name, Sprite sp, Color col)
    {
        var rt = NewRT(name, parent); Stretch(rt);
        var im = rt.gameObject.AddComponent<Image>();
        im.sprite = sp; im.type = Image.Type.Sliced; im.color = col; im.raycastTarget = false;
        return im;
    }

    Text Label(Transform parent, string name, int size, TextAnchor anchor, Color color, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, bool stroke = false)
    {
        var rt = NewRT(name, parent);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font; t.fontSize = size; t.alignment = anchor; t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false; t.supportRichText = false;
        if (stroke) { var ol = rt.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0f, 0f, 0f, 0.6f); ol.effectDistance = new Vector2(2, -2); }
        else { var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.5f); sh.effectDistance = new Vector2(1.5f, -1.5f); }
        return t;
    }

    static float RoundSd(float x, float y, float size, float r)
    {
        float c = size * 0.5f;
        float qx = Mathf.Abs(x - c) - (c - r), qy = Mathf.Abs(y - c) - (c - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static Sprite Sd(int size, float r, float ring, float border)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundSd(x + 0.5f, y + 0.5f, size, r);
                float a = Mathf.Clamp01(0.5f - d);
                if (ring > 0f) a = 0.6f * Mathf.Clamp01(a - Mathf.Clamp01(0.5f - (d + ring)));
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }
}
