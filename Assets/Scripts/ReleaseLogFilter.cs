using UnityEngine;

// Surum (Development Build olmayan) build'lerde Debug.Log/Warning spam'ini kapatir; hatalar yine Player.log'a yazilir.
public static class ReleaseLogFilter
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (!Application.isEditor && !Debug.isDebugBuild)
            Debug.unityLogger.filterLogType = LogType.Error;
    }
}
