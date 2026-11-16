using UnityEngine;
using UnityEngine.UI;

// Oyun ici HUD v2 (uGUI, calisma aninda kurulur; harici gorsel gerekmez, ikonlar kodla cizilir).
public class GameHUD : MonoBehaviour
{
    public static bool Active;
    [Tooltip("Istege bagli: kendi .ttf fontunu buraya surukle. Bossa Impact / Bahnschrift denenir.")]
    public Font customFont;
    public float findSeconds = 1f;

    static readonly Color PanelDark = new Color(0.10f, 0.08f, 0.06f, 0.62f);
    static readonly Color Light = new Color(0.97f, 0.94f, 0.87f, 1f);
    static readonly Color Dim = new Color(0.70f, 0.67f, 0.62f, 1f);
    static readonly Color Amber = new Color(0.95f, 0.78f, 0.38f, 1f);
    static readonly Color Hay = new Color(0.96f, 0.76f, 0.26f, 1f);
    static readonly Color Water = new Color(0.36f, 0.68f, 0.92f, 1f);
    static readonly Color Alert = new Color(0.95f, 0.30f, 0.26f, 1f);
    static readonly Color StoneC = new Color(0.62f, 0.66f, 0.74f, 1f);
    static readonly Color Grass = new Color(0.47f, 0.80f, 0.35f, 1f);
    static readonly Color EmptyC = new Color(0.55f, 0.55f, 0.55f, 1f);
    static readonly Color DarkInk = new Color(0.12f, 0.08f, 0.03f, 1f);

    const int IS = 128;
    static readonly System.Globalization.CultureInfo TR = new System.Globalization.CultureInfo("tr-TR");

    class P { public RectTransform rt; public Image shadow, fill, ring, bar; }
    class Slot { public P p; public Image icon, badge; public Text cap, name, badgeText; public int last = -99; public float pop; }

    Font font;
    Sprite rounded, circle, chamferFill, chamferRing, hexFill, hexRing, icBale, icStone, icBucket, icClock, icEgg, icArrow;
    Sprite[] icItem = new Sprite[4];
    CanvasGroup root;

    P timerP; Image clockImg; Text timerCap, timerText;
    P roleP; GameObject roleGo; Image roleHex, roleHexRing; Text roleCap, roleText, roleSub;
    RectTransform heightPanel, heightFill, marker, peakLine; Image heightFillImg, markerBubble, markerArrow; Text heightValue, heightGoal, heightPeak;
    Slot handSlot, itemSlot, itemSlot2;
    Text cardTitle; Image cardHexFill;

    BaleCarrier carrier; ItemSystem items; ObjectThrower thrower; WaterBucket bucket; Hatchery hatchery;
    float nextFind, nextHatch, spawnY = float.MinValue, peakH, shownH;

    // ---- olay bazli bilgi (eski OnGUI yazilari yerine) ----
    public static string Airdrop, Msg;
    public static float AirdropUntil, MsgUntil;
    public static Rect Air(string text, Rect r) { Airdrop = text; AirdropUntil = Time.unscaledTime + 0.4f; return Active ? Rect.zero : r; }
    public static Rect Note(string text, Rect r) { Msg = text; MsgUntil = Time.unscaledTime + 0.4f; return Active ? Rect.zero : r; }

    P hitChip, pushChip, airP, msgP; Text hitText, pushText, airText, msgText; Image hitBar, pushBar;
    PlayerCombat combat; SecretPush push;

    void BuildExtras(Transform c)
    {
        hitChip = Chip(c, "VurmaChip", 28f, out hitText, out hitBar, Alert);
        pushChip = Chip(c, "ItmeChip", 96f, out pushText, out pushBar, Water);
        airP = NewPanel(c, "ItemDusuyor", new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(580, 76), Amber);
        airText = Label(airP.fill.transform, "T", 34, TextAnchor.MiddleCenter, Light, Vector2.zero, Vector2.one, new Vector2(18, 6), new Vector2(-18, -6));
        Fit(airText, 16);
        msgP = NewPanel(c, "Mesaj", new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(620, 76), Amber);
        msgText = Label(msgP.fill.transform, "T", 36, TextAnchor.MiddleCenter, Light, Vector2.zero, Vector2.one, new Vector2(18, 6), new Vector2(-18, -6));
        Fit(msgText, 16);
        hitChip.rt.gameObject.SetActive(false); pushChip.rt.gameObject.SetActive(false);
        airP.rt.gameObject.SetActive(false); msgP.rt.gameObject.SetActive(false);
    }

    P Chip(Transform c, string name, float x, out Text txt, out Image fill, Color accent)
    {
        var p = NewPanel(c, name, new Vector2(0, 0), new Vector2(28, x), new Vector2(210, 56), accent);
        txt = Label(p.fill.transform, "T", 28, TextAnchor.MiddleCenter, Light, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -4));
        Fit(txt, 14);
        var tr = NewRT("Yol", p.fill.transform); tr.gameObject.SetActive(false);
        tr.anchorMin = Vector2.zero; tr.anchorMax = new Vector2(1, 0); tr.offsetMin = new Vector2(18, 8); tr.offsetMax = new Vector2(-18, 14);
        var bgI = tr.gameObject.AddComponent<Image>();
        bgI.sprite = rounded; bgI.type = Image.Type.Sliced; bgI.color = new Color(0f, 0f, 0f, 0.35f); bgI.raycastTarget = false;
        fill = Slice(tr, "Dolgu", rounded, accent);
        return p;
    }

    void ShowChip(P chip, Text txt, Image fill, float left, float total, string label)
    {
        bool on = left > 0.01f;
        chip.rt.gameObject.SetActive(on);
        if (!on) return;
        txt.text = label + "  " + left.ToString("0.0") + " sn";
        fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(left / Mathf.Max(0.1f, total)), 1f);
    }

    void UpdateExtras()
    {
        ShowChip(hitChip, hitText, hitBar, combat != null ? combat.CooldownLeft : 0f, combat != null ? combat.hitCooldown : 5f, "VURMA");
        ShowChip(pushChip, pushText, pushBar, push != null ? push.CdLeft : 0f, push != null ? push.cooldown : 6f, "ITME");

        string m = null;
        if (Time.unscaledTime < MsgUntil && !string.IsNullOrEmpty(Msg)) m = Msg;
        else if (combat != null && combat.IsStunned) m = "SERSEMLEDIN!";
        msgP.rt.gameObject.SetActive(m != null);
        if (m != null) msgText.text = m;

        bool a = Time.unscaledTime < AirdropUntil && !string.IsNullOrEmpty(Airdrop);
        airP.rt.gameObject.SetActive(a);
        if (a)
        {
            airText.text = Airdrop;
            float p = 1f + 0.03f * Mathf.Sin(Time.unscaledTime * 6f);
            airP.rt.localScale = new Vector3(p, p, 1f);
        }
    }

    // ---- sonuc ekrani (ResultScreen'in eski OnGUI'sinin yerine) ----
    CanvasGroup resGroup; P resP, youP; Text resTitle, resReason, resRoleName, resVerdict, resFoot; Image youBadge;
    RectTransform[] resRows = new RectTransform[10]; Text[] resRowName = new Text[10], resRowRole = new Text[10]; Image[] resRowDot = new Image[10];
    bool resShown;

    void BuildResult()
    {
        var go = new GameObject("ResultCanvas");
        go.transform.SetParent(transform, false);
        var cv = go.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 20;
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(2560, 1440); sc.matchWidthOrHeight = 0.5f;
        resGroup = go.AddComponent<CanvasGroup>(); resGroup.alpha = 0f; resGroup.blocksRaycasts = false; resGroup.interactable = false;

        var d = NewRT("Karart", go.transform); Stretch(d);
        var di = d.gameObject.AddComponent<Image>(); di.color = new Color(0f, 0f, 0f, 0.55f); di.raycastTarget = false;

        resP = NewPanel(go.transform, "Sonuc", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 760), Amber);
        resP.fill.color = new Color(0.10f, 0.08f, 0.06f, 0.90f);
        var f = resP.fill.transform;
        var cap = Label(f, "Cap", 24, TextAnchor.MiddleCenter, Amber, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -62), new Vector2(0, -26));
        cap.text = "OYUN BITTI";
        resTitle = Label(f, "Kazanan", 84, TextAnchor.MiddleCenter, Light, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -160), new Vector2(-24, -62), true);
        Fit(resTitle, 36);
        resReason = Label(f, "Neden", 26, TextAnchor.MiddleCenter, Dim, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -206), new Vector2(-30, -164));
        Fit(resReason, 14);

        youP = NewPanel(f, "Sen", new Vector2(0.5f, 1), new Vector2(0, -232), new Vector2(800, 112), Amber);
        var y = youP.fill.transform;
        var brt = NewRT("Rozet", y);
        brt.anchorMin = brt.anchorMax = new Vector2(0, 0.5f); brt.pivot = new Vector2(0, 0.5f); brt.anchoredPosition = new Vector2(20, 0); brt.sizeDelta = new Vector2(76, 76);
        youBadge = Img(brt, "Dolgu", hexFill, Amber);
        Img(brt, "Cerceve", hexRing, Color.white);
        var ert = NewRT("Yumurta", brt);
        ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0.5f); ert.sizeDelta = new Vector2(50, 50);
        var ei = ert.gameObject.AddComponent<Image>(); ei.sprite = icEgg; ei.raycastTarget = false;
        var rcap = Label(y, "RolCap", 20, TextAnchor.UpperLeft, Amber, new Vector2(0, 1), new Vector2(1, 1), new Vector2(116, -40), new Vector2(-300, -10));
        rcap.text = "SENIN ROLUN";
        resRoleName = Label(y, "RolAd", 44, TextAnchor.MiddleLeft, Light, Vector2.zero, Vector2.one, new Vector2(116, 6), new Vector2(-300, -36));
        Fit(resRoleName, 20);
        resVerdict = Label(y, "Sonuc", 52, TextAnchor.MiddleRight, Light, Vector2.zero, Vector2.one, new Vector2(500, 6), new Vector2(-24, -6), true);
        Fit(resVerdict, 24);

        var lcap = Label(f, "RollerCap", 22, TextAnchor.MiddleCenter, Dim, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -392), new Vector2(0, -362));
        lcap.text = "ROLLER ACILDI";
        for (int i = 0; i < resRows.Length; i++)
        {
            var rr = NewRT("Satir" + i, f);
            rr.anchorMin = new Vector2(0, 1); rr.anchorMax = new Vector2(1, 1);
            rr.offsetMin = new Vector2(50, -404 - 54 * i - 48); rr.offsetMax = new Vector2(-50, -404 - 54 * i);
            var bgI = rr.gameObject.AddComponent<Image>();
            bgI.sprite = rounded; bgI.type = Image.Type.Sliced; bgI.color = new Color(1f, 1f, 1f, i % 2 == 0 ? 0.07f : 0.03f); bgI.raycastTarget = false;
            var dot = NewRT("Nokta", rr);
            dot.anchorMin = dot.anchorMax = new Vector2(0, 0.5f); dot.pivot = new Vector2(0, 0.5f); dot.anchoredPosition = new Vector2(18, 0); dot.sizeDelta = new Vector2(22, 22);
            resRowDot[i] = dot.gameObject.AddComponent<Image>(); resRowDot[i].sprite = circle; resRowDot[i].raycastTarget = false;
            resRowName[i] = Label(rr, "Ad", 28, TextAnchor.MiddleLeft, Light, Vector2.zero, Vector2.one, new Vector2(54, 0), new Vector2(-380, 0));
            Fit(resRowName[i], 14);
            resRowRole[i] = Label(rr, "Rol", 26, TextAnchor.MiddleRight, Light, Vector2.zero, Vector2.one, new Vector2(420, 0), new Vector2(-18, 0));
            Fit(resRowRole[i], 12);
            resRows[i] = rr;
            rr.gameObject.SetActive(false);
        }
        resFoot = Label(f, "Alt", 26, TextAnchor.MiddleCenter, Amber, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 16), new Vector2(0, 52));
        resFoot.text = "R: TEKRAR OYNA";
    }

    void UpdateResult(GameManager gm)
    {
        bool on = gm != null && gm.State == GameState.Sonuc;
        resGroup.alpha = Mathf.MoveTowards(resGroup.alpha, on ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        if (!on) { resShown = false; return; }
        if (!resShown) { resShown = true; FillResult(gm); }
        var c2 = Amber; c2.a = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f); resFoot.color = c2;
    }

    void FillResult(GameManager gm)
    {
        resTitle.text = GameManager.WinnerName(gm.winner).ToUpperInvariant();
        resReason.text = ResultScreen.Reason(gm);
        var human = carrier != null ? carrier.gameObject : PlayerController.LocalObject();
        bool has = human != null && RoleManager.HasRole(human);
        youP.rt.gameObject.SetActive(has);
        if (has)
        {
            Role r = RoleManager.GetRole(human);
            bool won = ResultScreen.Won(r, gm.winner);
            string full = RoleManager.DisplayName(r);
            int a = full.IndexOf('('), b = full.IndexOf(')');
            resRoleName.text = ((a >= 0 && b > a) ? full.Substring(a + 1, b - a - 1) : full).ToUpperInvariant();
            youBadge.color = RoleColor(r);
            Color vc = won ? new Color(0.50f, 0.88f, 0.42f, 1f) : new Color(0.96f, 0.38f, 0.32f, 1f);
            resVerdict.text = won ? "KAZANDIN!" : "KAYBETTIN";
            resVerdict.color = vc;
            youP.ring.color = Color.Lerp(vc, Color.white, 0.2f);
        }
        int n = 0;
        var rm = RoleManager.Instance;
        if (rm != null)
            foreach (var e in rm.assignments)
            {
                if (n >= resRows.Length) break;
                Color rc = RoleColor(e.role);
                resRows[n].gameObject.SetActive(true);
                resRowDot[n].color = rc;
                resRowName[n].text = e.name + (e.name == "Oyuncu" ? "  (sen)" : "");
                resRowRole[n].text = RoleManager.DisplayName(e.role); resRowRole[n].color = rc;
                n++;
            }
        for (int i = n; i < resRows.Length; i++) resRows[i].gameObject.SetActive(false);
        resP.rt.sizeDelta = new Vector2(900, 404 + n * 54 + 84);
    }

    // ---- oylama paneli (uGUI, fare ile secim) + etkilesim ipucu ----
    static string PromptText; static float PromptUntil;
    public static Rect Prompt(string text, Rect r) { PromptText = text; PromptUntil = Time.unscaledTime + 0.3f; return Active ? Rect.zero : r; }

    VoteSystem vs; float nextVs;
    CanvasGroup voteGroup; P voteP, promptP; Text voteQ, voteResult, voteHint, promptText;
    P meetP; Text meetCap, meetTitle, meetHint;
    RectTransform[] vRows = new RectTransform[9]; Text[] vName = new Text[9]; Image[] vBg = new Image[9]; GameObject[] vCand = new GameObject[9];
    RectTransform vSkip; Image vSkipBg; int vCount; bool voteShown, voteResolvedShown;

    void BuildVote(Transform hudRoot)
    {
        promptP = NewPanel(hudRoot, "Ipucu", new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(380, 60), Amber);
        promptText = Label(promptP.fill.transform, "T", 30, TextAnchor.MiddleCenter, Light, Vector2.zero, Vector2.one, new Vector2(14, 2), new Vector2(-14, -2));
        Fit(promptText, 14);
        promptP.rt.gameObject.SetActive(false);

        // ---- TOPLANTI BASLIGI (ust orta): rol/sure panelleriyle ayni dil ----
        meetP = NewPanel(hudRoot, "Toplanti", new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1320, 265), Amber);
        var mf = meetP.fill.transform;
        meetCap = Label(mf, "Cap", 32, TextAnchor.UpperCenter, Amber, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -54), new Vector2(-12, -10));
        meetTitle = Label(mf, "Baslik", 114, TextAnchor.MiddleCenter, Light, Vector2.zero, Vector2.one, new Vector2(16, 80), new Vector2(-16, -56), true);
        Fit(meetTitle, 38);
        meetHint = Label(mf, "Ipucu", 34, TextAnchor.LowerCenter, Dim, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 16), new Vector2(-12, 64));
        Fit(meetHint, 19);
        meetP.rt.gameObject.SetActive(false);

        var go = new GameObject("VoteCanvas");
        go.transform.SetParent(transform, false);
        var cv = go.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 15;
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(2560, 1440); sc.matchWidthOrHeight = 0.5f;
        voteGroup = go.AddComponent<CanvasGroup>(); voteGroup.alpha = 0f; voteGroup.blocksRaycasts = false; voteGroup.interactable = false;

        voteP = NewPanel(go.transform, "Oylama", new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(620, 600), Amber);
        voteP.fill.color = new Color(0.10f, 0.08f, 0.06f, 0.90f);
        var f = voteP.fill.transform;
        var cap = Label(f, "Cap", 22, TextAnchor.MiddleCenter, Amber, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -52), new Vector2(0, -20));
        cap.text = "OYLAMA";
        voteQ = Label(f, "Soru", 58, TextAnchor.MiddleCenter, Light, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -122), new Vector2(-24, -54), true);
        voteQ.text = "OYLAMA"; Fit(voteQ, 28);
        voteResult = Label(f, "Sonuc", 36, TextAnchor.MiddleCenter, Light, new Vector2(0, 0), new Vector2(1, 1), new Vector2(40, 50), new Vector2(-40, -70));
        Fit(voteResult, 16); voteResult.gameObject.SetActive(false);
        voteHint = Label(f, "Ipucu", 18, TextAnchor.MiddleCenter, Dim, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 12), new Vector2(0, 38));
        for (int i = 0; i < vRows.Length; i++)
        {
            Image bg; Text nm;
            vRows[i] = VoteRow(f, true, out bg, out nm);
            vBg[i] = bg; vName[i] = nm;
            vRows[i].offsetMin = new Vector2(40, -130 - 70 * i - 60); vRows[i].offsetMax = new Vector2(-40, -130 - 70 * i);
            vRows[i].gameObject.SetActive(false);
        }
        Text st;
        vSkip = VoteRow(f, false, out vSkipBg, out st);
        st.text = "VAZGEC (OY KULLANMA)"; st.color = Dim;
        vSkip.gameObject.SetActive(false);
    }

    RectTransform VoteRow(Transform parent, bool withEgg, out Image bg, out Text name)
    {
        var rr = NewRT("Satir", parent);
        rr.anchorMin = new Vector2(0, 1); rr.anchorMax = new Vector2(1, 1);
        bg = rr.gameObject.AddComponent<Image>();
        bg.sprite = rounded; bg.type = Image.Type.Sliced; bg.color = new Color(1f, 1f, 1f, 0.08f); bg.raycastTarget = false;
        if (withEgg)
        {
            var e = NewRT("Yumurta", rr);
            e.anchorMin = e.anchorMax = new Vector2(0, 0.5f); e.pivot = new Vector2(0, 0.5f); e.anchoredPosition = new Vector2(16, 0); e.sizeDelta = new Vector2(44, 44);
            var ei = e.gameObject.AddComponent<Image>(); ei.sprite = icEgg; ei.raycastTarget = false;
        }
        name = Label(rr, "Ad", 32, withEgg ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, Light, Vector2.zero, Vector2.one, new Vector2(withEgg ? 74 : 16, 0), new Vector2(-16, 0));
        Fit(name, 16);
        return rr;
    }

    void UpdateMeeting(bool meet)
    {
        if (meetP == null) return;
        bool on = meet && vs != null && vs.IsVoting;
        if (meetP.rt.gameObject.activeSelf != on) meetP.rt.gameObject.SetActive(on);
        if (!on) return;
        if (!vs.IsResolved)
        {
            var who = vs.CallerObject; var me = vs.HumanObject;
            meetCap.text = who == null ? "TOPLANTI" : (who == me ? "TOPLANTI  •  SEN CAGIRDIN" : "TOPLANTI  •  " + who.name.ToUpperInvariant() + " CAGIRDI");
            meetTitle.text = "OYLAMA";
            meetHint.text = "ETRAFINA BAK  •  E: OY VER  •  Q: OY KULLANMA";
        }
        else
        {
            meetCap.text = "SONUC";
            meetTitle.text = vs.ResultText.ToUpperInvariant();
            meetHint.text = "SANA VERILEN OY: " + vs.VotesFor(vs.HumanObject);
        }
    }

    void UpdateVote()
    {
        bool meet = vs != null && vs.InMeeting;
        if (timerP != null && timerP.rt != null && !timerP.rt.gameObject.activeSelf) timerP.rt.gameObject.SetActive(true);
        UpdateMeeting(meet);
        UpdateVoteCore();
    }

    void UpdateVoteCore()
    {
        // etkilesim ipucu (E: ...)
        bool pr = Time.unscaledTime < PromptUntil && !string.IsNullOrEmpty(PromptText);
        promptP.rt.gameObject.SetActive(pr);
        if (pr) promptText.text = PromptText.ToUpperInvariant();

        if (vs == null && Time.unscaledTime >= nextVs) { nextVs = Time.unscaledTime + 1f; vs = FindFirstObjectByType<VoteSystem>(); }
        bool on = vs != null && vs.IsVoting && !vs.UseMeetingUI;
        voteGroup.alpha = Mathf.MoveTowards(voteGroup.alpha, on ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        if (!on) { voteShown = false; return; }

        bool resolved = vs.IsResolved;
        if (!voteShown || resolved != voteResolvedShown) { voteShown = true; voteResolvedShown = resolved; FillVote(resolved); }
        if (resolved) return;

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;
        Vector2 mp = mouse.position.ReadValue();
        bool click = mouse.leftButton.wasPressedThisFrame;
        Color hov = new Color(Amber.r, Amber.g, Amber.b, 0.35f), idle = new Color(1f, 1f, 1f, 0.08f);
        for (int i = 0; i < vCount; i++)
        {
            bool h = RectTransformUtility.RectangleContainsScreenPoint(vRows[i], mp, null);
            vBg[i].color = h ? hov : idle;
            if (h && click) { vs.CastVote(vCand[i]); return; }
        }
        bool hs = RectTransformUtility.RectangleContainsScreenPoint(vSkip, mp, null);
        vSkipBg.color = hs ? hov : idle;
        if (hs && click) vs.CastVote(null);
    }

    void FillVote(bool resolved)
    {
        vCount = 0;
        if (!resolved)
        {
            var human = vs.HumanObject;
            foreach (var c in vs.CandidateList)
            {
                if (c == null || c == human || vCount >= vRows.Length) continue;
                vCand[vCount] = c; vName[vCount].text = c.name.ToUpperInvariant(); vCount++;
            }
        }
        for (int i = 0; i < vRows.Length; i++) vRows[i].gameObject.SetActive(i < vCount);
        vSkip.gameObject.SetActive(!resolved);
        vSkip.offsetMin = new Vector2(40, -130 - 70 * vCount - 60); vSkip.offsetMax = new Vector2(-40, -130 - 70 * vCount);
        voteQ.gameObject.SetActive(!resolved);
        voteResult.gameObject.SetActive(resolved);
        if (resolved) voteResult.text = vs.ResultText;
        voteHint.text = resolved ? "" : "FARE ILE SEC";
        voteP.rt.sizeDelta = new Vector2(620, resolved ? 380 : 130 + (vCount + 1) * 70 + 70);
    }

    void Awake() { Build(); Active = true; }
    void Start() { StyleRoleCard(); }
    void OnDestroy() { Active = false; }
    void OnGUI() { if (font != null) GUI.skin.font = font; } // OnGUI yazilari da ayni font (IMGUI)

    // ================= KURULUM =================
    void Build()
    {
        font = customFont;
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift SemiBold Condensed", "Bahnschrift SemiBold", "Segoe UI Semibold", "Trebuchet MS" }, 48);
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        rounded = SdSprite(64, (x, y) => RoundSd(x, y, 64f, 22f), 0f, new Vector4(22, 22, 22, 22));
        circle = SdSprite(64, (x, y) => RoundSd(x, y, 64f, 32f), 0f, Vector4.zero);
        chamferFill = SdSprite(64, (x, y) => RoundSd(x, y, 64f, 14f), 0f, new Vector4(24, 24, 24, 24));
        chamferRing = SdSprite(64, (x, y) => RoundSd(x, y, 64f, 14f), 2f, new Vector4(24, 24, 24, 24));
        hexFill = SdSprite(128, (x, y) => RoundSd(x, y, 128f, 64f), 0f, Vector4.zero);
        hexRing = SdSprite(128, (x, y) => RoundSd(x, y, 128f, 64f), 5f, Vector4.zero);

        icBale = Paint(IS, IcBale, Color.black);
        icStone = Paint(IS, IcStone, Color.black);
        icBucket = Paint(IS, IcBucket, Color.black);
        icEgg = Paint(IS, IcEgg, Color.black);
        icClock = Paint(IS, IcClock, Color.white);
        icArrow = Paint(64, (u, v) => Over(Color.clear, Color.white, Cov(Tri(u, v, 0.95f, 0.10f, 0.95f, 0.90f, 0.10f, 0.50f), 64f)), Color.white);
        for (int i = 0; i < 4; i++) icItem[i] = ItemBadge(i, ItemColor((ItemType)(i + 1)));

        var cgo = new GameObject("HUDCanvas");
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(3200, 1800);
        sc.matchWidthOrHeight = 0.5f;
        root = cgo.AddComponent<CanvasGroup>();
        root.blocksRaycasts = false; root.interactable = false; root.alpha = 0f;
        var c = cgo.transform;

        // ---- SURE (sag ust) ----
        timerP = NewPanel(c, "Sure", new Vector2(1, 1), new Vector2(-28, -24), new Vector2(330, 112), Amber);
        var tc = timerP.fill.transform;
        var crt = NewRT("Saat", tc);
        crt.anchorMin = crt.anchorMax = new Vector2(0, 0.5f); crt.pivot = new Vector2(0, 0.5f);
        crt.anchoredPosition = new Vector2(20, 2); crt.sizeDelta = new Vector2(64, 64);
        clockImg = crt.gameObject.AddComponent<Image>(); clockImg.sprite = icClock; clockImg.color = Amber; clockImg.raycastTarget = false;
        timerCap = Label(tc, "Cap", 22, TextAnchor.UpperCenter, Amber, new Vector2(0, 1), new Vector2(1, 1), new Vector2(100, -36), new Vector2(-12, -8));
        timerCap.text = "KALAN SURE";
        timerText = Label(tc, "Sayi", 66, TextAnchor.MiddleCenter, Light, Vector2.zero, Vector2.one, new Vector2(100, 6), new Vector2(-12, -36), true);
        Fit(timerText, 28);

        // ---- ROL (sol ust) ----
        roleP = NewPanel(c, "Rol", new Vector2(0, 1), new Vector2(28, -24), new Vector2(430, 116), Amber);
        roleGo = roleP.rt.gameObject;
        var rc = roleP.fill.transform;
        var hrt = NewRT("Rozet", rc);
        hrt.anchorMin = hrt.anchorMax = new Vector2(0, 0.5f); hrt.pivot = new Vector2(0, 0.5f);
        hrt.anchoredPosition = new Vector2(16, 2); hrt.sizeDelta = new Vector2(100, 100);
        roleHex = Img(hrt, "Dolgu", hexFill, Amber);
        roleHexRing = Img(hrt, "Cerceve", hexRing, Color.white);
        var ert = NewRT("Yumurta", hrt);
        ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0.5f); ert.sizeDelta = new Vector2(64, 64);
        var eimg = ert.gameObject.AddComponent<Image>(); eimg.sprite = icEgg; eimg.raycastTarget = false;
        roleCap = Label(rc, "Cap", 22, TextAnchor.UpperLeft, Amber, new Vector2(0, 1), new Vector2(1, 1), new Vector2(128, -36), new Vector2(-12, -8));
        roleCap.text = "ROLUN";
        roleText = Label(rc, "Ad", 54, TextAnchor.MiddleLeft, Light, Vector2.zero, Vector2.one, new Vector2(128, 8), new Vector2(-12, -34), true);
        Fit(roleText, 30);
        roleSub = Label(rc, "Alt", 24, TextAnchor.LowerLeft, Dim, new Vector2(0, 0), new Vector2(1, 0), new Vector2(128, 10), new Vector2(-12, 36));
        Fit(roleSub, 14);

        // ---- YUKSEKLIK (sol orta) ----
        var hp = NewPanel(c, "Yukseklik", new Vector2(0, 0.5f), new Vector2(28, -10), new Vector2(164, 760), Grass);
        heightPanel = hp.rt; var hc = hp.fill.transform;
        var hcap = Label(hc, "Cap", 24, TextAnchor.UpperLeft, Grass, new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -42), new Vector2(-10, -12));
        hcap.text = "YUKSEKLIK"; Fit(hcap, 14);
        heightGoal = Label(hc, "Hedef", 22, TextAnchor.UpperLeft, Dim, new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -68), new Vector2(-10, -44));
        Fit(heightGoal, 12);
        heightPeak = Label(hc, "Zirve", 22, TextAnchor.LowerLeft, Dim, new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 18), new Vector2(-10, 44));
        Fit(heightPeak, 12);

        var track = NewRT("Yol", hc);
        track.anchorMin = new Vector2(0, 0); track.anchorMax = new Vector2(0, 1);
        track.offsetMin = new Vector2(32, 78); track.offsetMax = new Vector2(58, -92);
        var tImg = track.gameObject.AddComponent<Image>();
        tImg.sprite = rounded; tImg.type = Image.Type.Sliced; tImg.color = new Color(0f, 0f, 0f, 0.35f); tImg.raycastTarget = false;
        for (int i = 0; i <= 10; i++)
        {
            var tk = NewRT("Tik" + i, track);
            tk.anchorMin = tk.anchorMax = new Vector2(0, i / 10f); tk.pivot = new Vector2(1, 0.5f);
            tk.anchoredPosition = new Vector2(-5, 0);
            bool big = i % 5 == 0;
            tk.sizeDelta = new Vector2(big ? 20 : 11, big ? 3 : 2);
            var ti = tk.gameObject.AddComponent<Image>(); ti.color = new Color(1f, 1f, 1f, big ? 0.55f : 0.28f); ti.raycastTarget = false;
        }
        heightFill = NewRT("Dolgu", track);
        heightFill.anchorMin = Vector2.zero; heightFill.anchorMax = new Vector2(1, 0.02f);
        heightFill.offsetMin = new Vector2(3, 3); heightFill.offsetMax = new Vector2(-3, -3);
        heightFillImg = heightFill.gameObject.AddComponent<Image>();
        heightFillImg.sprite = rounded; heightFillImg.type = Image.Type.Sliced; heightFillImg.color = Grass; heightFillImg.raycastTarget = false;

        peakLine = NewRT("ZirveCizgi", track);
        peakLine.anchorMin = new Vector2(0, 0.02f); peakLine.anchorMax = new Vector2(1, 0.02f); peakLine.sizeDelta = new Vector2(16, 3);
        var pl = peakLine.gameObject.AddComponent<Image>(); pl.color = new Color(1f, 1f, 1f, 0.85f); pl.raycastTarget = false;

        marker = NewRT("Isaret", track);
        marker.anchorMin = marker.anchorMax = new Vector2(1, 0.02f); marker.pivot = new Vector2(0, 0.5f);
        marker.anchoredPosition = new Vector2(6, 0); marker.sizeDelta = new Vector2(100, 46);
        var art = NewRT("Ok", marker);
        art.anchorMin = art.anchorMax = new Vector2(0, 0.5f); art.pivot = new Vector2(0, 0.5f); art.sizeDelta = new Vector2(12, 20);
        markerArrow = art.gameObject.AddComponent<Image>(); markerArrow.sprite = icArrow; markerArrow.color = Grass; markerArrow.raycastTarget = false;
        markerBubble = Slice(marker, "Balon", chamferFill, Grass);
        var br = markerBubble.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0, 0.5f); br.pivot = new Vector2(0, 0.5f);
        br.anchoredPosition = new Vector2(10, 0); br.sizeDelta = new Vector2(84, 44);
        heightValue = Label(br, "Deger", 30, TextAnchor.MiddleCenter, DarkInk, Vector2.zero, Vector2.one, new Vector2(0, 2), Vector2.zero);
        Fit(heightValue, 18);

        // ---- SLOTLAR (alt orta) ----
        handSlot = MakeSlot(c, "EL", 0f);
        
        
        BuildExtras(c);
        BuildResult();
        BuildVote(c);
    }

    Slot MakeSlot(Transform parent, string cap, float x)
    {
        var s = new Slot();
        s.p = NewPanel(parent, "Slot_" + cap, new Vector2(0.5f, 0), new Vector2(x, 34), new Vector2(176, 176), Amber);
        var c = s.p.fill.transform;
        s.cap = Label(c, "Cap", 22, TextAnchor.UpperLeft, Amber, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -36), new Vector2(-54, -8));
        s.cap.text = cap; Fit(s.cap, 12);
        var irt = NewRT("Ikon", c);
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.anchoredPosition = new Vector2(0, 12); irt.sizeDelta = new Vector2(102, 102);
        s.icon = irt.gameObject.AddComponent<Image>(); s.icon.preserveAspect = true; s.icon.raycastTarget = false;
        s.name = Label(c, "Ad", 30, TextAnchor.MiddleCenter, Light, new Vector2(0, 0), new Vector2(1, 0), new Vector2(6, 12), new Vector2(-6, 46));
        Fit(s.name, 14);
        var brt = NewRT("Tus", c);
        brt.anchorMin = brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(1, 1);
        brt.anchoredPosition = new Vector2(-12, -10); brt.sizeDelta = new Vector2(34, 34);
        s.badge = brt.gameObject.AddComponent<Image>(); s.badge.sprite = circle; s.badge.color = Amber; s.badge.raycastTarget = false;
        s.badgeText = Label(brt, "T", 24, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return s;
    }

    // ---- rol karti: sade, kisa yazili tasarim ----
    RoleCardUI rcu; Text cardCap, cardName, cardTask, cardHint; Image cardRing;

    void StyleRoleCard()
    {
        rcu = FindFirstObjectByType<RoleCardUI>(FindObjectsInactive.Include);
        if (rcu == null) return;
        rcu.GetComponent<RectTransform>().sizeDelta = new Vector2(730, 310);
        var bg = rcu.GetComponent<Image>();
        if (bg != null) { bg.sprite = chamferFill; bg.type = Image.Type.Sliced; bg.color = new Color(0.10f, 0.08f, 0.06f, 0.82f); }
        foreach (var tx in new[] { rcu.title, rcu.task, rcu.goal, rcu.hint }) if (tx != null) tx.enabled = false;
        cardTitle = rcu.title;

        var ringRt = NewRT("StilCerceve", rcu.transform); Stretch(ringRt); ringRt.SetAsFirstSibling();
        cardRing = ringRt.gameObject.AddComponent<Image>();
        cardRing.sprite = chamferRing; cardRing.type = Image.Type.Sliced; cardRing.color = Amber; cardRing.raycastTarget = false;

        var hrt = NewRT("StilRozet", rcu.transform);
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1); hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.anchoredPosition = Vector2.zero; hrt.sizeDelta = new Vector2(118, 118);
        Img(hrt, "Halka", hexFill, new Color(0.10f, 0.08f, 0.06f, 1f)).rectTransform.localScale = new Vector3(1.12f, 1.12f, 1f);
        cardHexFill = Img(hrt, "Dolgu", hexFill, Amber);
        Img(hrt, "Cerceve", hexRing, Color.white);
        var ert = NewRT("Yumurta", hrt);
        ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0.5f); ert.sizeDelta = new Vector2(76, 76);
        var eimg = ert.gameObject.AddComponent<Image>(); eimg.sprite = icEgg; eimg.raycastTarget = false;

        var rt2 = rcu.transform;
        cardCap = Label(rt2, "Ust", 25, TextAnchor.MiddleCenter, Amber, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -106), new Vector2(0, -72));
        cardCap.text = "SENIN ROLUN";
        cardName = Label(rt2, "Rol", 90, TextAnchor.MiddleCenter, Light, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -210), new Vector2(-16, -110), false);
        { var cs = cardName.GetComponent<Shadow>(); if (cs != null) Destroy(cs); }
        Fit(cardName, 42);
        cardTask = Label(rt2, "Gorev", 31, TextAnchor.MiddleCenter, Light, new Vector2(0, 1), new Vector2(1, 1), new Vector2(34, -266), new Vector2(-34, -216));
        Fit(cardTask, 17);
        cardHint = Label(rt2, "Ipucu", 20, TextAnchor.LowerCenter, Dim, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 8), new Vector2(0, 36));
        cardHint.text = "TAB: ROLUNU TEKRAR GOR";
    }

    void LateUpdate()
    {
        if (rcu == null || cardName == null || rcu.title == null) return;
        string nm = BigName(rcu.title.text);
        cardName.text = nm.ToUpperInvariant();
        Color col = rcu.title.color; col.a = 1f;
        cardName.color = col;
        cardRing.color = Color.Lerp(col, Color.white, 0.2f);
        cardTask.text = ShortTask(nm, rcu.task != null ? rcu.task.text : "");
    }

    static string BigName(string full)
    {
        if (string.IsNullOrEmpty(full)) return "";
        int a = full.IndexOf('('), b = full.IndexOf(')');
        return (a >= 0 && b > a) ? full.Substring(a + 1, b - a - 1) : full;
    }

    static string ShortTask(string nm, string raw)
    {
        string n = nm.ToLower(TR), r = (raw ?? "").ToLower(TR);
        if (n.StartsWith("sabotaj")) return "YUMURTALARIN KULUÇKAYA ÇIKMASINI ENGELLE!";
        if (r.Contains("sabotajciyi bul")) return "KULUCKAYA ULAS, SABOTAJCIYI BUL!";
        string s = raw ?? "";
        int k = s.IndexOf(':'); if (k >= 0 && k < 24) s = s.Substring(k + 1);
        s = s.Trim(); int d = s.IndexOf('.'); if (d > 0) s = s.Substring(0, d);
        return s.Trim().ToUpperInvariant();
    }

    // ================= GUNCELLEME =================
    void Update()
    {
        if (cardHexFill != null && cardTitle != null) cardHexFill.color = cardTitle.color;

        var gm = GameManager.Instance;
        bool show = gm != null && (gm.IsPlaying || gm.State.ToString() == "Oylama");
        UpdateResult(gm); UpdateVote();
        root.alpha = Mathf.MoveTowards(root.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        if (root.alpha <= 0.01f) return;

        if (carrier == null && Time.unscaledTime >= nextFind) { nextFind = Time.unscaledTime + findSeconds; FindPlayer(); }
        if (hatchery == null && Time.unscaledTime >= nextHatch) { nextHatch = Time.unscaledTime + 2f; hatchery = FindFirstObjectByType<Hatchery>(); }

        bool meetNow = vs != null && vs.InMeeting;   // toplanti/oylama sirasinda envanter slotlari gizli
        if (handSlot != null) handSlot.p.rt.gameObject.SetActive(!meetNow);
        if (itemSlot != null) itemSlot.p.rt.gameObject.SetActive(!meetNow);
        if (itemSlot2 != null) itemSlot2.p.rt.gameObject.SetActive(!meetNow);
        UpdateTimer(gm); UpdateRole(); UpdateHeight(); UpdateSlots(); UpdateExtras();
        Pop(handSlot);
    }

    void FindPlayer()
    {
        if (GameManager.NetMode)
        {
            // ag: sahnedeki ilk BaleCarrier uzaktaki oyuncu olabilir -> HUD sadece yerel (sahip) oyuncuyu izler
            var lp = PlayerController.Local;
            if (lp == null) return;
            var lb = lp.GetComponent<BaleCarrier>();
            if (lb == null || lb.isBot) return;
            carrier = lb; items = lb.GetComponent<ItemSystem>(); thrower = lb.GetComponent<ObjectThrower>(); bucket = lb.GetComponent<WaterBucket>(); combat = lb.GetComponent<PlayerCombat>(); push = lb.GetComponent<SecretPush>();
            return;
        }
        foreach (var b in FindObjectsByType<BaleCarrier>(FindObjectsSortMode.None))
        {
            if (b.isBot) continue;
            carrier = b; items = b.GetComponent<ItemSystem>(); thrower = b.GetComponent<ObjectThrower>(); bucket = b.GetComponent<WaterBucket>(); combat = b.GetComponent<PlayerCombat>(); push = b.GetComponent<SecretPush>();
            break;
        }
    }

    void UpdateTimer(GameManager gm)
    {
        if (gm == null) return;
        bool inVote = vs != null && vs.InMeeting && vs.IsVoting && !vs.IsResolved;
        float t = inVote ? vs.MeetingLeft : Mathf.Max(0f, gm.timeLeft);
        int m = (int)(t / 60f), s = (int)(t % 60f);
        timerText.text = inVote ? Mathf.CeilToInt(t).ToString() : m.ToString("00") + ":" + s.ToString("00");
        timerCap.text = inVote ? "OYLAMA SURESI" : "KALAN SURE";
        bool low = inVote ? t <= 10f : (t <= 60f && gm.IsPlaying);
        Color acc = low ? Alert : Amber;
        timerText.color = low ? Alert : Light;
        timerCap.color = acc; clockImg.color = acc;
        timerP.ring.color = Color.Lerp(acc, Color.white, 0.25f); timerP.bar.color = acc;
        float p = low ? 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 7f) : 1f;
        timerP.rt.localScale = new Vector3(p, p, 1f);
    }

    void UpdateRole()
    {
        bool has = carrier != null && RoleManager.HasRole(carrier.gameObject);
        roleGo.SetActive(has);
        if (!has) return;
        var r = RoleManager.GetRole(carrier.gameObject);
        string full = RoleManager.DisplayName(r), big = full, sub = "";
        int a = full.IndexOf('('), b = full.IndexOf(')');
        if (a >= 0 && b > a) { big = full.Substring(a + 1, b - a - 1); sub = full.Substring(0, a).Trim(); }
        roleText.text = big.ToUpperInvariant();
        roleSub.text = ""; roleCap.text = sub.Length > 0 ? sub.ToUpperInvariant() : "ROLUN";
        Color rcol = RoleColor(r);
        roleHex.color = rcol; roleCap.color = rcol;
        roleP.ring.color = Color.Lerp(rcol, Color.white, 0.2f); roleP.bar.color = rcol;
    }

    void UpdateHeight()
    {
        bool meet = vs != null && vs.InMeeting;
        if (heightPanel != null && heightPanel.gameObject.activeSelf == meet) heightPanel.gameObject.SetActive(!meet);
        if (meet) return;   // toplantida yukseklik gizli ve guncellenmez
        UpdateHeightCore();
    }

    void UpdateHeightCore()
    {
        if (carrier == null) return;
        float y = carrier.transform.position.y;
        if (spawnY == float.MinValue) spawnY = y;           // dogdugun yer = 0 metre
        float h = Mathf.Max(0f, y - spawnY);
        if (h > peakH) peakH = h;
        float target = 30f;
        if (hatchery != null) { float t = hatchery.transform.position.y - spawnY; if (t > 5f) target = t; }
        float scale = Mathf.Max(target, peakH, 1f);
        float f = Mathf.Clamp01(h / scale), pf = Mathf.Clamp01(peakH / scale);
        shownH = Mathf.Lerp(shownH, f, Time.unscaledDeltaTime * 8f);
        float fy = Mathf.Max(0.02f, shownH);

        heightFill.anchorMax = new Vector2(1f, fy);
        marker.anchorMin = marker.anchorMax = new Vector2(1f, fy);
        peakLine.anchorMin = new Vector2(0f, Mathf.Max(0.02f, pf)); peakLine.anchorMax = new Vector2(1f, Mathf.Max(0.02f, pf));
        Color col = HeightColor(shownH);
        heightFillImg.color = col; markerBubble.color = col; markerArrow.color = col;
        heightValue.text = h.ToString("0.0") + " m";
        heightGoal.text = "HEDEF  " + Mathf.RoundToInt(target) + " m";
        heightPeak.text = "ZIRVE  " + peakH.ToString("0.0") + " m";
    }

    void UpdateSlots()
    {
        if (carrier == null) { SetSlot(handSlot, -1, null, "BOŞ", EmptyC, "", "EL"); return; }

        if (carrier.IsCarrying) SetSlot(handSlot, 0, icBale, "BALYA", Hay, "X", "EL");
        else if (thrower != null && thrower.Held != null)
        {
            string n = thrower.Held.name.Replace("(Clone)", "").Trim();
            string low = n.ToLower();
            bool stone = low.Contains("tas") || low.Contains("taş") || low.Contains("rock") || low.Contains("stone");
            var tint = HeldTint(thrower.Held.gameObject, n);
            SetSlot(handSlot, 1, icStone, HeldLabel(n, stone), tint, "", "EL");
            handSlot.icon.color = tint;   // ikon nesnenin kendi renginde
        }
        else if (bucket != null && bucket.HasBucket) SetSlot(handSlot, 2, icBucket, "KOVA " + Mathf.RoundToInt(bucket.water) + "/" + bucket.capacity, Water, "", "EL");
        else if (items != null && items.Held != ItemType.None)
        {
            var t = items.Held;
            SetSlot(handSlot, 30 + (int)t, icItem[(int)t - 1], ItemSystem.Name(t).ToUpper(TR), ItemColor(t),
                items.Equipped ? "" : "G", items.Equipped ? "KULLAN: SOL TIK" : "EŞYA");
        }
        else SetSlot(handSlot, -1, null, "BOŞ", EmptyC, "", "EL");
    }

        // Elde tasinan nesnenin adi: "Nesne_Kabak" -> "KABAK" (onek yok)
    static string HeldLabel(string n, bool stone)
    {
        if (stone) return "TAŞ";
        string s = n;
        int u = s.IndexOf('_');
        if (u >= 0 && s.Substring(0, u).ToLowerInvariant() == "nesne") s = s.Substring(u + 1);
        s = s.Replace('_', ' ').Trim();
        if (s.ToLowerInvariant() == "misir") return "MISIR";
        return s.ToUpper(TR);
    }

    // Nesnenin dokusunun ortalama rengi (materyal rengiyle carpilir), ikon icin okunur olacak kadar parlatilir
    readonly System.Collections.Generic.Dictionary<string, Color> heldTints = new System.Collections.Generic.Dictionary<string, Color>();
    Color HeldTint(GameObject go, string key)
    {
        Color c;
        if (heldTints.TryGetValue(key, out c)) return c;
        c = Color.white;
        var r = go != null ? go.GetComponentInChildren<Renderer>() : null;
        var m = r != null ? r.sharedMaterial : null;
        if (m != null)
        {
            Color tint = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
            Texture tex = null;
            if (m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
            if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
            c = (tex != null ? AvgColor(tex) : Color.white) * tint;
            float hh, ss, vv;
            Color.RGBToHSV(c, out hh, out ss, out vv);
            c = Color.HSVToRGB(hh, Mathf.Min(1f, ss * 1.15f), Mathf.Max(vv, 0.80f));
        }
        c.a = 1f;
        heldTints[key] = c;
        return c;
    }

    static Color AvgColor(Texture tex)
    {
        var rt = RenderTexture.GetTemporary(16, 16, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;
        var t2 = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        t2.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = t2.GetPixels();
        UnityEngine.Object.Destroy(t2);
        float rr = 0f, gg = 0f, bb = 0f;
        for (int i = 0; i < px.Length; i++) { rr += px[i].r; gg += px[i].g; bb += px[i].b; }
        return new Color(rr / px.Length, gg / px.Length, bb / px.Length, 1f);
    }

    void SetSlot(Slot s, int key, Sprite icon, string label, Color accent, string badge, string cap)
    {
        if (s.last != key) { s.last = key; s.pop = 1f; }
        bool has = icon != null;
        s.icon.enabled = has; s.icon.color = Color.white; if (has) s.icon.sprite = icon;
        s.name.text = label; s.name.color = has ? Light : Dim;
        s.cap.text = cap; s.cap.color = has ? accent : Dim;
        var rc = Color.Lerp(accent, Color.white, 0.2f); rc.a = has ? 1f : 0.35f; s.p.ring.color = rc;
        var bc = accent; bc.a = has ? 1f : 0.25f; s.p.bar.color = bc;
        bool sb = badge.Length > 0;
        s.badge.gameObject.SetActive(sb); if (sb) s.badgeText.text = badge;
    }

    void Pop(Slot s)
    {
        s.pop = Mathf.MoveTowards(s.pop, 0f, Time.unscaledDeltaTime * 4f);
        float k = 1f + 0.14f * Mathf.Sin(s.pop * Mathf.PI);
        s.p.rt.localScale = new Vector3(k, k, 1f);
    }

    static Color HeightColor(float f)
    {
        var g = new Color(0.47f, 0.80f, 0.35f, 1f); var y = new Color(0.97f, 0.80f, 0.27f, 1f); var o = new Color(0.97f, 0.50f, 0.22f, 1f);
        return f < 0.6f ? Color.Lerp(g, y, f / 0.6f) : Color.Lerp(y, o, (f - 0.6f) / 0.4f);
    }

    static Color ItemColor(ItemType t)
    {
        switch (t)
        {
            case ItemType.CivcivRoketi: return new Color(0.96f, 0.55f, 0.22f, 1f);
            case ItemType.UykuTuyu: return new Color(0.66f, 0.55f, 0.92f, 1f);
            case ItemType.YagLekesi: return new Color(0.22f, 0.62f, 0.66f, 1f);
            case ItemType.TozBulutu: return new Color(0.84f, 0.66f, 0.40f, 1f);
        }
        return StoneC;
    }

    static Color RoleColor(Role r)
    {
        switch (r)
        {
            case Role.Sabotajci: return new Color(0.93f, 0.28f, 0.25f, 1f);
            case Role.Tirmanici: return new Color(0.45f, 0.80f, 0.34f, 1f);
            case Role.Palyaco: return new Color(0.95f, 0.52f, 0.74f, 1f);
        }
        return new Color(0.40f, 0.70f, 0.95f, 1f);
    }

    // ================= UI YARDIMCILARI =================
    RectTransform NewRT(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }

    Image Slice(Transform parent, string name, Sprite sp, Color col)
    {
        var rt = NewRT(name, parent); Stretch(rt);
        var im = rt.gameObject.AddComponent<Image>();
        im.sprite = sp; im.type = Image.Type.Sliced; im.color = col; im.raycastTarget = false;
        return im;
    }

    Image Img(Transform parent, string name, Sprite sp, Color col)
    {
        var rt = NewRT(name, parent); Stretch(rt);
        var im = rt.gameObject.AddComponent<Image>();
        im.sprite = sp; im.color = col; im.raycastTarget = false;
        return im;
    }

    P NewPanel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color accent)
    {
        var p = new P();
        p.rt = NewRT(name, parent);
        p.rt.anchorMin = p.rt.anchorMax = anchor; p.rt.pivot = anchor; p.rt.anchoredPosition = pos; p.rt.sizeDelta = size;
        p.shadow = Slice(p.rt, "Golge", chamferFill, new Color(0f, 0f, 0f, 0.25f));
        p.shadow.rectTransform.offsetMin = new Vector2(4, -8); p.shadow.rectTransform.offsetMax = new Vector2(4, -8);
        p.fill = Slice(p.rt, "Dolgu", chamferFill, PanelDark);
        p.ring = Slice(p.rt, "Cerceve", chamferRing, Color.Lerp(accent, Color.white, 0.25f));
        var brt = NewRT("Cizgi", p.rt);
        brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(0, 1);
        brt.offsetMin = new Vector2(24, -9); brt.offsetMax = new Vector2(72, -4);
        p.bar = brt.gameObject.AddComponent<Image>(); p.bar.color = accent; p.bar.raycastTarget = false; brt.gameObject.SetActive(false);
        return p;
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

    static void Fit(Text t, int min)
    {
        t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = t.fontSize;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
    }

    // ================= PROSEDUREL SPRITE'LAR =================
    static Sprite SdSprite(int size, System.Func<float, float, float> sd, float ring, Vector4 border)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = sd(x + 0.5f, y + 0.5f);
                float a = Mathf.Clamp01(0.5f - d);
                if (ring > 0f) a = 0.6f * Mathf.Clamp01(a - Mathf.Clamp01(0.5f - (d + ring)));
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    static Sprite Paint(int size, System.Func<float, float, Color> f, Color emptyRgb)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var c = f((x + 0.5f) / size, (y + 0.5f) / size);
                if (c.a < 0.004f) c = new Color(emptyRgb.r, emptyRgb.g, emptyRgb.b, 0f);
                px[y * size + x] = c;
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static float RoundSd(float px, float py, float S, float r)
    {
        float qx = Mathf.Abs(px - S * 0.5f) - (S * 0.5f - r), qy = Mathf.Abs(py - S * 0.5f) - (S * 0.5f - r);
        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    // sol ust ve sag alt kosesi kesik (acili) panel
    static float ChamferSd(float x, float y, float S, float cut)
    {
        float bx = Mathf.Max(Mathf.Max(-x, x - S), Mathf.Max(-y, y - S));
        float d1 = (cut - (x + (S - y))) * 0.7071f;
        float d2 = (cut - ((S - x) + y)) * 0.7071f;
        return Mathf.Max(bx, Mathf.Max(d1, d2));
    }

    static float HexSd(float px, float py, float S)
    {
        float x = Mathf.Abs(px - S * 0.5f), y = Mathf.Abs(py - S * 0.5f);
        float a = 0.8660254f * (S * 0.5f - 1.5f);
        return Mathf.Max(x, 0.5f * x + 0.8660254f * y) - a;
    }

    static float Cov(float sd, float size) { return Mathf.Clamp01(0.5f - sd * size); }

    static Color Over(Color d, Color s, float a)
    {
        a = Mathf.Clamp01(a) * s.a;
        float oa = a + d.a * (1f - a);
        if (oa <= 0.0001f) return new Color(d.r, d.g, d.b, 0f);
        var c = (new Vector3(s.r, s.g, s.b) * a + new Vector3(d.r, d.g, d.b) * d.a * (1f - a)) / oa;
        return new Color(c.x, c.y, c.z, oa);
    }

    static float Box(float u, float v, float cx, float cy, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(u - cx) - hx + r, qy = Mathf.Abs(v - cy) - hy + r;
        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }
    static float Circ(float u, float v, float cx, float cy, float r) { float du = u - cx, dv = v - cy; return Mathf.Sqrt(du * du + dv * dv) - r; }
    static float Ell(float u, float v, float cx, float cy, float rx, float ry)
    {
        float du = (u - cx) / rx, dv = (v - cy) / ry;
        return (Mathf.Sqrt(du * du + dv * dv) - 1f) * Mathf.Min(rx, ry);
    }
    static float EdgeD(float px, float py, float ax, float ay, float bx, float by)
    {
        float ex = bx - ax, ey = by - ay, l = Mathf.Sqrt(ex * ex + ey * ey);
        return -(ex * (py - ay) - ey * (px - ax)) / l;
    }
    static float Tri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float s = ((bx - ax) * (cy - ay) - (by - ay) * (cx - ax)) > 0f ? 1f : -1f;
        float d = s * EdgeD(px, py, ax, ay, bx, by);
        d = Mathf.Max(d, s * EdgeD(px, py, bx, by, cx, cy));
        return Mathf.Max(d, s * EdgeD(px, py, cx, cy, ax, ay));
    }

    // ---- ikonlar ----
    static Color IcBale(float u, float v)
    {
        Color c = Color.clear;
        float body = Box(u, v, 0.5f, 0.5f, 0.43f, 0.30f, 0.07f);
        c = Over(c, new Color(0, 0, 0, 0.28f), Cov(Box(u, v, 0.5f, 0.45f, 0.43f, 0.30f, 0.07f), IS));
        c = Over(c, new Color(0.42f, 0.27f, 0.07f, 1f), Cov(body, IS));
        Color gold = Color.Lerp(new Color(0.80f, 0.56f, 0.12f), new Color(1f, 0.86f, 0.42f), Mathf.InverseLerp(0.2f, 0.8f, v));
        if (Mathf.Sin(v * 70f + u * 6f) > 0.80f) gold = new Color(gold.r * 0.88f, gold.g * 0.88f, gold.b * 0.88f, 1f);
        c = Over(c, gold, Cov(body + 0.025f, IS));
        float strap = Mathf.Min(Mathf.Abs(u - 0.3f), Mathf.Abs(u - 0.7f)) - 0.028f;
        c = Over(c, new Color(0.38f, 0.24f, 0.08f, 1f), Cov(Mathf.Max(strap, body + 0.025f), IS));
        return c;
    }

    static Color IcStone(float u, float v)
    {
        Color c = Color.clear;
        float sd = Mathf.Min(Ell(u, v, 0.5f, 0.42f, 0.40f, 0.28f), Mathf.Min(Ell(u, v, 0.37f, 0.50f, 0.26f, 0.25f), Ell(u, v, 0.64f, 0.47f, 0.25f, 0.24f)));
        c = Over(c, new Color(0, 0, 0, 0.25f), Cov(Ell(u, v, 0.5f, 0.30f, 0.40f, 0.22f), IS));
        c = Over(c, new Color(0.22f, 0.23f, 0.28f, 1f), Cov(sd, IS));
        Color g = Color.Lerp(new Color(0.42f, 0.43f, 0.48f), new Color(0.78f, 0.79f, 0.83f), Mathf.InverseLerp(0.15f, 0.75f, v));
        c = Over(c, g, Cov(sd + 0.025f, IS));
        c = Over(c, new Color(1, 1, 1, 0.35f), Cov(Ell(u, v, 0.38f, 0.60f, 0.11f, 0.05f), IS));
        return c;
    }

    static Color IcBucket(float u, float v)
    {
        Color c = Color.clear;
        float half = Mathf.Lerp(0.24f, 0.34f, Mathf.Clamp01((v - 0.14f) / 0.50f));
        float body = Mathf.Max(Mathf.Abs(u - 0.5f) - half, Mathf.Max(0.14f - v, v - 0.64f));
        float handle = Mathf.Max(Mathf.Abs(Circ(u, v, 0.5f, 0.62f, 0.32f)) - 0.022f, 0.64f - v);
        c = Over(c, new Color(0.35f, 0.36f, 0.40f, 1f), Cov(handle, IS));
        c = Over(c, new Color(0.10f, 0.22f, 0.38f, 1f), Cov(body, IS));
        Color blue = Color.Lerp(new Color(0.22f, 0.50f, 0.80f), new Color(0.45f, 0.75f, 0.95f), Mathf.InverseLerp(0.14f, 0.64f, v));
        c = Over(c, blue, Cov(body + 0.025f, IS));
        c = Over(c, new Color(0.15f, 0.35f, 0.60f, 1f), Cov(Mathf.Max(Mathf.Abs(v - 0.34f) - 0.035f, body + 0.025f), IS));
        c = Over(c, new Color(0.16f, 0.30f, 0.50f, 1f), Cov(Ell(u, v, 0.5f, 0.64f, half + 0.02f, 0.05f), IS));
        c = Over(c, new Color(0.55f, 0.85f, 1f, 1f), Cov(Ell(u, v, 0.5f, 0.645f, half - 0.03f, 0.032f), IS));
        return c;
    }

    static Color IcClock(float u, float v)
    {
        Color c = Color.clear;
        c = Over(c, Color.white, Cov(Mathf.Abs(Circ(u, v, 0.5f, 0.5f, 0.40f)) - 0.05f, IS));
        c = Over(c, Color.white, Cov(Box(u, v, 0.5f, 0.62f, 0.035f, 0.13f, 0.03f), IS));
        c = Over(c, Color.white, Cov(Box(u, v, 0.60f, 0.5f, 0.10f, 0.035f, 0.03f), IS));
        c = Over(c, Color.white, Cov(Circ(u, v, 0.5f, 0.5f, 0.055f), IS));
        c = Over(c, Color.white, Cov(Box(u, v, 0.5f, 0.965f, 0.06f, 0.03f, 0.02f), IS));
        return c;
    }

    static Color IcEgg(float u, float v)
    {
        Color c = Color.clear;
        float k = 1f - 0.20f * (v - 0.46f) / 0.38f;
        float du = (u - 0.5f) / (0.31f * k), dv = (v - 0.46f) / 0.40f;
        float sd = (Mathf.Sqrt(du * du + dv * dv) - 1f) * 0.31f;
        c = Over(c, new Color(0.18f, 0.10f, 0.05f, 1f), Cov(sd, IS));
        c = Over(c, new Color(1f, 0.96f, 0.84f, 1f), Cov(sd + 0.03f, IS));
        var eye = new Color(0.12f, 0.08f, 0.05f, 1f);
        c = Over(c, eye, Cov(Circ(u, v, 0.40f, 0.54f, 0.055f), IS));
        c = Over(c, eye, Cov(Circ(u, v, 0.60f, 0.54f, 0.055f), IS));
        c = Over(c, Color.white, Cov(Circ(u, v, 0.415f, 0.56f, 0.018f), IS));
        c = Over(c, Color.white, Cov(Circ(u, v, 0.615f, 0.56f, 0.018f), IS));
        c = Over(c, new Color(0.96f, 0.62f, 0.20f, 1f), Cov(Tri(u, v, 0.44f, 0.44f, 0.56f, 0.44f, 0.5f, 0.37f), IS));
        return c;
    }

    static Sprite ItemBadge(int kind, Color col)
    {
        return Paint(IS, (u, v) =>
        {
            Color c = Color.clear;
            c = Over(c, new Color(0, 0, 0, 0.28f), Cov(Circ(u, v, 0.5f, 0.46f, 0.44f), IS));
            c = Over(c, new Color(col.r * 0.5f, col.g * 0.5f, col.b * 0.5f, 1f), Cov(Circ(u, v, 0.5f, 0.5f, 0.46f), IS));
            c = Over(c, col, Cov(Circ(u, v, 0.5f, 0.5f, 0.46f) + 0.035f, IS));
            float g = kind == 0 ? GRocket(u, v) : kind == 1 ? GFeather(u, v) : kind == 2 ? GDrop(u, v) : GCloud(u, v);
            return Over(c, Color.white, Cov(g, IS));
        }, Color.black);
    }

    static float GRocket(float u, float v)
    {
        float sd = Box(u, v, 0.5f, 0.48f, 0.10f, 0.20f, 0.09f);
        sd = Mathf.Min(sd, Tri(u, v, 0.40f, 0.64f, 0.60f, 0.64f, 0.5f, 0.82f));
        sd = Mathf.Min(sd, Tri(u, v, 0.41f, 0.40f, 0.28f, 0.26f, 0.41f, 0.28f));
        sd = Mathf.Min(sd, Tri(u, v, 0.59f, 0.40f, 0.72f, 0.26f, 0.59f, 0.28f));
        sd = Mathf.Min(sd, Tri(u, v, 0.45f, 0.28f, 0.55f, 0.28f, 0.5f, 0.15f));
        return Mathf.Max(sd, -Circ(u, v, 0.5f, 0.52f, 0.045f));
    }
    static float GFeather(float u, float v)
    {
        float x = ((u - 0.5f) + (v - 0.5f)) * 0.7071f + 0.5f, y = (-(u - 0.5f) + (v - 0.5f)) * 0.7071f + 0.5f;
        float sd = Ell(x, y, 0.5f, 0.5f, 0.33f, 0.13f);
        return Mathf.Max(sd, -Box(x, y, 0.5f, 0.5f, 0.36f, 0.012f, 0f));
    }
    static float GDrop(float u, float v) { return Mathf.Min(Circ(u, v, 0.5f, 0.38f, 0.19f), Tri(u, v, 0.33f, 0.46f, 0.67f, 0.46f, 0.5f, 0.80f)); }
    static float GCloud(float u, float v)
    {
        float sd = Circ(u, v, 0.36f, 0.46f, 0.14f);
        sd = Mathf.Min(sd, Circ(u, v, 0.54f, 0.54f, 0.18f));
        sd = Mathf.Min(sd, Circ(u, v, 0.67f, 0.44f, 0.13f));
        return Mathf.Min(sd, Box(u, v, 0.5f, 0.40f, 0.27f, 0.09f, 0.08f));
    }
}
