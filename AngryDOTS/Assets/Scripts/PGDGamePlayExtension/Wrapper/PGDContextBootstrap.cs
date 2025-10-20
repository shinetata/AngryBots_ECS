using System;
using System.Collections.Generic;
using System.Reflection;
using PGD;
using PGD.Jobs;
using UnityEngine;

public static class PGDContextBootstrap
{
    private static bool s_SystemsRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize()
    {
        if (s_SystemsRegistered) return;

        try
        {
            PGDSystemExecutionScheduler.RegisterAll();
            s_SystemsRegistered = true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"PGDContextBootstrap: Failed to bootstrap systems. {ex}");
        }
    }

    private static class PGDSystemExecutionScheduler
    {
        private static readonly Type SystemBaseType = typeof(PgdSystemBase);
        private static readonly StringComparer TypeNameComparer = StringComparer.Ordinal;
        private static readonly IComparer<Type> TypeComparer = Comparer<Type>.Create(
            (x, y) => TypeNameComparer.Compare(x?.FullName, y?.FullName));

        public static void RegisterAll()
        {
            var orderedTypes = BuildOrderedSystemTypes();
            if (orderedTypes.Count == 0)
                return;

            var world = PGDGameContext.GetWorld();
            var jobManager = PGDGameContext.GetJobManager();

            foreach (var type in orderedTypes)
            {
                try
                {
                    if (Activator.CreateInstance(type) is not PgdSystemBase system)
                        continue;

                    world.RegisterSystem(system);

                    if (system is IJobifiedSystem jobifiedSystem)
                    {
                        jobManager.Register(jobifiedSystem);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"PGDContextBootstrap: Failed to instantiate {type.FullName}. {ex.Message}");
                }
            }
        }

        private static List<Type> BuildOrderedSystemTypes()
        {
            var nodes = new List<SystemNode>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm == null || asm.IsDynamic) continue;
                var name = asm.FullName;
                if (name.StartsWith("Unity", StringComparison.Ordinal) ||
                    name.StartsWith("System", StringComparison.Ordinal) ||
                    name.StartsWith("mscorlib", StringComparison.Ordinal))
                {
                    continue;
                }

                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }

                if (types == null) continue;

                foreach (var type in types)
                {
                    if (type == null ||
                        type.IsAbstract ||
                        type.IsInterface ||
                        type.IsGenericTypeDefinition ||
                        !SystemBaseType.IsAssignableFrom(type) ||
                        type.GetConstructor(Type.EmptyTypes) == null ||
                        Attribute.IsDefined(type, typeof(DisableAutoRegisterAttribute)))
                    {
                        continue;
                    }

                    nodes.Add(new SystemNode(type));
                }
            }

            if (nodes.Count == 0)
                return new List<Type>();

            nodes.Sort((a, b) => TypeNameComparer.Compare(a.Type.FullName, b.Type.FullName));

            var lookup = new Dictionary<Type, SystemNode>(nodes.Count);
            foreach (var node in nodes)
            {
                lookup[node.Type] = node;
                node.CollectAttributes();
            }

            var graph = new Dictionary<Type, HashSet<Type>>(nodes.Count);
            var indegree = new Dictionary<Type, int>(nodes.Count);
            foreach (var node in nodes)
            {
                graph[node.Type] = new HashSet<Type>();
                indegree[node.Type] = 0;
            }

            foreach (var node in nodes)
            {
                ProcessEdges(node.Type, node.UpdateBeforeTargets, lookup, graph, indegree, true);
                ProcessEdges(node.Type, node.UpdateAfterTargets, lookup, graph, indegree, false);
            }

            return TopologicalSort(nodes, graph, indegree);
        }

        private static void ProcessEdges(
            Type sourceType,
            IReadOnlyList<Type> targets,
            Dictionary<Type, SystemNode> lookup,
            Dictionary<Type, HashSet<Type>> graph,
            Dictionary<Type, int> indegree,
            bool forward)
        {
            if (targets == null || targets.Count == 0)
                return;

            foreach (var target in targets)
            {
                if (target == null)
                    continue;

                if (!lookup.ContainsKey(target))
                {
                    Debug.LogWarning($"PGDContextBootstrap: {sourceType.Name} references {target.Name}, but it is not auto-registered. Constraint ignored.");
                    continue;
                }

                if (target == sourceType)
                {
                    Debug.LogWarning($"PGDContextBootstrap: {sourceType.Name} cannot define execution order relative to itself. Constraint ignored.");
                    continue;
                }

                var from = forward ? sourceType : target;
                var to = forward ? target : sourceType;

                var edges = graph[from];
                if (edges.Add(to))
                {
                    indegree[to] = indegree[to] + 1;
                }
            }
        }

        private static List<Type> TopologicalSort(
            List<SystemNode> nodes,
            Dictionary<Type, HashSet<Type>> graph,
            Dictionary<Type, int> indegree)
        {
            var zeroSet = new SortedSet<Type>(TypeComparer);
            foreach (var pair in indegree)
            {
                if (pair.Value == 0)
                {
                    zeroSet.Add(pair.Key);
                }
            }

            var ordered = new List<Type>(nodes.Count);
            var visited = new HashSet<Type>();

            while (zeroSet.Count > 0)
            {
                var type = zeroSet.Min;
                zeroSet.Remove(type);
                ordered.Add(type);
                visited.Add(type);

                if (!graph.TryGetValue(type, out var neighbours))
                    continue;

                foreach (var neighbour in neighbours)
                {
                    var degree = --indegree[neighbour];
                    if (degree == 0)
                    {
                        zeroSet.Add(neighbour);
                    }
                }
            }

            if (ordered.Count != nodes.Count)
            {
                Debug.LogWarning("PGDContextBootstrap: Detected cycle in PGD system execution order. Falling back to alphabetical order for remaining systems.");
                foreach (var node in nodes)
                {
                    if (visited.Add(node.Type))
                    {
                        ordered.Add(node.Type);
                    }
                }
            }

            return ordered;
        }

        private sealed class SystemNode
        {
            public SystemNode(Type type)
            {
                Type = type;
            }

            public Type Type { get; }
            public List<Type> UpdateBeforeTargets { get; } = new List<Type>();
            public List<Type> UpdateAfterTargets { get; } = new List<Type>();

            public void CollectAttributes()
            {
                foreach (var attr in Type.GetCustomAttributes(typeof(UpdateSystemBeforeAttribute), true))
                {
                    if (attr is UpdateSystemBeforeAttribute beforeAttr)
                    {
                        AddTargets(UpdateBeforeTargets, beforeAttr.TargetTypes);
                    }
                }

                foreach (var attr in Type.GetCustomAttributes(typeof(UpdateSystemAfterAttribute), true))
                {
                    if (attr is UpdateSystemAfterAttribute afterAttr)
                    {
                        AddTargets(UpdateAfterTargets, afterAttr.TargetTypes);
                    }
                }
            }

            private static void AddTargets(List<Type> list, IReadOnlyList<Type> targets)
            {
                if (targets == null) return;
                for (int i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    if (target != null)
                    {
                        list.Add(target);
                    }
                }
            }
        }
    }
}
