using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
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

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = asm.FullName;
            if (name.StartsWith("Unity") || name.StartsWith("System") || name.StartsWith("mscorlib"))
                continue;

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }

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
                    break;
                }
            }
        }

        foreach (var kv in s_Registry)
            kv.Value.Sort((a, b) => a.Order.CompareTo(b.Order));
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
            for (Type type = comp.GetType();
                 type != null && typeof(Component).IsAssignableFrom(type);
                 type = type.BaseType)
            {
                if (!s_Registry.TryGetValue(type, out var handlers) || handlers == null || handlers.Count == 0)
                    continue;

                for (int j = 0; j < handlers.Count; j++)
                {
                    handlers[j].Invoke(comp);
                }
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
            var comps = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                ProcessComponent(comps[i]);
            }
        }
    }
}
