using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

// Ekran gecisleri: siyaha kararma / siyahtan acilma (yumusak egri). Calisma aninda kurulur, sahneler arasi kalir.
// Her sahne yuklenisinde otomatik siyahtan acilir. Kullanim: ScreenFader.FadeOut(sure) / ScreenFader.FadeIn(sure)
public class ScreenFader : MonoBehaviour
{
    public const float DefaultIn = 1.5f;
    static ScreenFader inst;
    CanvasGroup cg;
    Coroutine co;

    public static float Alpha { get { return inst != null && inst.cg != null ? inst.cg.alpha : 0f; } }
    public static void FadeIn(float t) { if (inst != null) inst.Go(0f, t); }
    public static void FadeOut(float t) { if (inst != null) inst.Go(1f, t); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (inst != null) return;
        var go = new GameObject("ScreenFader");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<ScreenFader>();
        inst.Build();
        // oyun ilk acildiginda siyah ekran yok
        
    }

    void Build()
    {
        var cvGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasGroup));
        cvGo.transform.SetParent(transform, false);
        var cv = cvGo.GetComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 32000;
        cg = cvGo.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false; cg.interactable = false; cg.alpha = 0f;

        var imgGo = new GameObject("Siyah", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = imgGo.GetComponent<RectTransform>();
        rt.SetParent(cvGo.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = imgGo.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        SceneManager.sceneLoaded += OnLoaded;
    }

    void OnDestroy() { SceneManager.sceneLoaded -= OnLoaded; if (inst == this) inst = null; }

    void OnLoaded(Scene s, LoadSceneMode m)
    {
        if (m != LoadSceneMode.Single) return;
        cg.alpha = 1f;          // yeni sahne siyahla baslasin
        Go(0f, DefaultIn, 12);      // ve yumusakca acilsin
    }

    void Go(float target, float t, int hold = 0)
    {
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(Run(target, t, hold));
    }

    IEnumerator Run(float target, float t, int hold)
    {
        for (int i = 0; i < hold; i++) yield return null;   // sahne kurulum takilmalari bitene kadar siyah kalsin
        float from = cg.alpha, e = 0f;
        while (e < t)
        {
            e += Mathf.Min(Time.unscaledDeltaTime, 0.04f);   // takilan karede gecis bitmesin
            cg.alpha = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / t)));
            yield return null;
        }
        cg.alpha = target;
    }
}
