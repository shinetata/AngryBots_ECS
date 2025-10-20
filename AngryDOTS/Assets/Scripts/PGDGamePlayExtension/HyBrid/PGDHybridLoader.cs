using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 负责构建和管理 PGDHyBrid 注册表，并提供统一的 Hybrid 初始化入口。
/// 可在任意自定义时机手动调用，以配合不同的场景加载流程。
/// </summary>
[Preserve]
public static class PGDHyBridLoader
{
    private static bool s_Initialized;
    private static Dictionary<Type, List<IInvoker>> s_Registry;
    private static readonly Dictionary<Type, IInvoker[]> s_HandlerCache = new Dictionary<Type, IInvoker[]>(128);
    private static readonly HashSet<Assembly> s_ProcessedAssemblies = new HashSet<Assembly>();

    #region ---- 注册表与调用器（去泛型统一调用） ----
    private interface IInvoker
    {
        Type AuthoringType { get; }
        int Order { get; }
        void Invoke(Component authoring);
    }

    [Preserve]
    private sealed class Invoker<TAuthoring> : IInvoker where TAuthoring : Component
    {
        private readonly PGDHyBrid<TAuthoring> _impl;
        public Type AuthoringType => typeof(TAuthoring);
        public int Order { get; }

        public Invoker(PGDHyBrid<TAuthoring> impl, int order)
        {
            _impl = impl;
            Order = order;
        }

        public void Invoke(Component authoring)
        {
            _impl.SetContext((TAuthoring)authoring);
            _impl.Handle((TAuthoring)authoring);
        }
    }

    private static void BuildRegistryIfNeeded()
    {
        if (s_Initialized) return;
        s_Initialized = true;
        s_Registry = new Dictionary<Type, List<IInvoker>>(64);

        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            RegisterAssembly(asm);

        foreach (var kv in s_Registry)
            kv.Value.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
    {
        RegisterAssembly(args.LoadedAssembly);
    }

    private static void RegisterAssembly(Assembly asm)
    {
        if (asm == null || asm.IsDynamic) return;
        if (!s_ProcessedAssemblies.Add(asm)) return;

        var name = asm.FullName;
        if (name.StartsWith("Unity") || name.StartsWith("System") || name.StartsWith("mscorlib"))
            return;

        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types; }

        if (types == null) return;

        foreach (var t in types)
        {
            if (t == null || !t.IsClass || t.IsAbstract) continue;

            for (var bt = t.BaseType; bt != null; bt = bt.BaseType)
            {
                if (!bt.IsGenericType || bt.GetGenericTypeDefinition() != typeof(PGDHyBrid<>))
                    continue;

                var authoringType = bt.GetGenericArguments()[0];
                if (!typeof(Component).IsAssignableFrom(authoringType)) break;

                var impl = Activator.CreateInstance(t);
                if (impl == null) break;

                var orderAttr = t.GetCustomAttribute<PGDHyBridOrderAttribute>();
                int order = orderAttr?.Order ?? 0;

                var invokerType = typeof(Invoker<>).MakeGenericType(authoringType);
                var invoker = (IInvoker)Activator.CreateInstance(invokerType, impl, order);

                if (!s_Registry.TryGetValue(authoringType, out var list))
                    s_Registry[authoringType] = list = new List<IInvoker>(2);
                list.Add(invoker);
                list.Sort((a, b) => a.Order.CompareTo(b.Order));
                s_HandlerCache.Clear();
                break;
            }
        }
    }

    private static IInvoker[] GetHandlersForType(Type type)
    {
        if (type == null) return Array.Empty<IInvoker>();

        if (s_HandlerCache.TryGetValue(type, out var cached))
            return cached;

        List<IInvoker> collector = null;
        for (var current = type; current != null && typeof(Component).IsAssignableFrom(current); current = current.BaseType)
        {
            if (!s_Registry.TryGetValue(current, out var handlers) || handlers == null || handlers.Count == 0)
                continue;

            collector ??= new List<IInvoker>(handlers.Count);
            collector.AddRange(handlers);
        }

        var result = collector == null ? Array.Empty<IInvoker>() : collector.ToArray();
        s_HandlerCache[type] = result;
        return result;
    }
    #endregion

    /// <summary>
    /// 触发当前所有已加载场景中的 Hybrid Handle。
    /// </summary>
    public static void InitializeAllHybrids()
    {
        BuildRegistryIfNeeded();
        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            RunHandlesOnScene(scene);
        }
    }

    /// <summary>
    /// 对指定场景执行注册的所有 Hybrid Handle。
    /// </summary>
    public static void RunHandlesOnScene(Scene scene)
    {
        BuildRegistryIfNeeded();
        if (!scene.IsValid() || s_Registry == null || s_Registry.Count == 0) return;

        var roots = scene.GetRootGameObjects();
        var processedComponents = new HashSet<Component>();
#if UNITY_EDITOR
        var processedGameObjects = new HashSet<GameObject>();
#endif

        void InvokeHandlers(Component comp)
        {
            var handlers = GetHandlersForType(comp.GetType());
            for (int j = 0; j < handlers.Length; j++)
            {
                handlers[j].Invoke(comp);
            }
        }

        void ProcessComponent(Component comp)
        {
            if (comp == null) return;
            if (!processedComponents.Add(comp)) return;

            InvokeHandlers(comp);

#if UNITY_EDITOR
            CollectSerializedReferences(comp);
#endif
        }

#if UNITY_EDITOR
        void CollectSerializedReferences(Component comp)
        {
            try
            {
                var so = new SerializedObject(comp);
                var iterator = so.GetIterator();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    var referenced = iterator.objectReferenceValue;
                    if (referenced == null)
                        continue;

                    if (referenced is Component refComp)
                    {
                        ProcessComponent(refComp);
                        VisitGameObject(refComp.gameObject);
                    }
                    else if (referenced is GameObject go)
                    {
                        VisitGameObject(go);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"PGDHyBridLoader: Failed to inspect serialized references on {comp.GetType().Name}: {ex.Message}");
            }
        }

        void VisitGameObject(GameObject go)
        {
            if (go == null) return;
            if (!processedGameObjects.Add(go)) return;

            var comps = go.GetComponents<Component>();
            for (int idx = 0; idx < comps.Length; idx++)
            {
                ProcessComponent(comps[idx]);
            }
        }
#endif

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var root = roots[rootIndex];
            var comps = ListPool<Component>.Get();
            try
            {
                root.GetComponentsInChildren(true, comps);
                for (int i = 0; i < comps.Count; i++)
                {
                    ProcessComponent(comps[i]);
                }
            }
            finally
            {
                ListPool<Component>.Release(comps);
            }
        }
    }
}
