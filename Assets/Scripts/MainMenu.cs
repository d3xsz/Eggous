using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

// Ana menu: gorselin uzerindeki tabelalara gorunmez butonlar. OYNA -> oyun sahnesi, AYARLAR -> SettingsMenu, CIKIS -> kapat.
// Sahne gecisleri ScreenFader ile yumusak (siyaha kararma, yeni sahnede siyahtan acilma).
public class MainMenu : MonoBehaviour
{
    public string gameScene = "Main";
    public RectTransform playBtn, settingsBtn, quitBtn;
    public CanvasGroup fade;                 // eski gecis katmani (artik ScreenFader kullaniliyor, kapali tutulur)
    public float fadeDuration = 0.8f;
    bool loading;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (fade != null) fade.alpha = 0f;
        Hook(playBtn, Play);
        Hook(settingsBtn, Settings);
        Hook(quitBtn, Quit);
    }

    void Hook(RectTransform rt, UnityEngine.Events.UnityAction a)
    {
        if (rt == null) return;
        var b = rt.GetComponent<Button>();
        if (b != null) b.onClick.AddListener(a);
    }

    public void Play()
    {
        if (loading) return;
        loading = true;
        StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        ScreenFader.FadeOut(fadeDuration);
        yield return new WaitForSecondsRealtime(fadeDuration + 0.05f);
        SceneManager.LoadScene(gameScene);
    }

    public void Settings() { if (!loading) SettingsMenu.OpenExternal(); }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
