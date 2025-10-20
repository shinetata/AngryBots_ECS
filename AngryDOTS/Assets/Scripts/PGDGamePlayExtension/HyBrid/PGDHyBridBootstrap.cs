using System.Collections.Generic;
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
    private readonly HashSet<int> _processedScenes = new HashSet<int>();

    void Awake()
    {
        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            ProcessScene(scene, force: true);
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        _processedScenes.Clear();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ProcessScene(scene);
    }

    void OnSceneUnloaded(Scene scene)
    {
        if (!scene.IsValid()) return;
        _processedScenes.Remove(scene.handle);
    }

    private void ProcessScene(Scene scene, bool force = false)
    {
        if (!scene.IsValid()) return;

        int handle = scene.handle;
        if (force)
        {
            _processedScenes.Add(handle);
        }
        else if (!_processedScenes.Add(handle))
        {
            return;
        }

        PGDHyBridLoader.RunHandlesOnScene(scene);
    }
}