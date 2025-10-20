using UnityEngine;
using UnityEngine.SceneManagement;

public static class PGDHyBridBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => s_Helper = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureHelperExists()
    {
        if (s_Helper != null) return;

        var go = new GameObject("PGDHybridBootstrapper");
        Object.DontDestroyOnLoad(go);
        s_Helper = go.AddComponent<PGDHyBridBootstrapper>();
    }

    private static PGDHyBridBootstrapper s_Helper;
}

sealed class PGDHyBridBootstrapper : MonoBehaviour
{
    void Awake() => PGDHyBridLoader.InitializeAllHybrids();
    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PGDHyBridLoader.RunHandlesOnScene(scene);
    }
}
