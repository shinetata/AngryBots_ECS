using PGD;
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using PGD.Jobs;

public static class PGDGameContext
{
    public static IEntity NullEntity = new IEntity();
    private static IECSWorld defaultWorld;
    private static PGDJobManager defaultJobManager;

    public static class Time
    {
        public static float DeltaTime => UnityEngine.Time.deltaTime;
        public static float ElapsedTime => UnityEngine.Time.time;
    }
    
    public static IECSWorld GetWorld()
    {
        if (defaultWorld == null)
        {
            defaultWorld = new IECSWorld();
        }
        return defaultWorld;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void InitializeDefaultPGDContext()
    {
        GetWorld();
        GetJobManager();
        defaultWorld.RegisterSystem(new HybridTransformSync());
    }

    public static void UpdateSystem()
    {
        GetWorld().PgdSystemManager.Update(default);
        GetJobManager().Update();
    }

    public static T GetExistingSystem<T>() where T : PgdSystemBase
    {
        var world = GetWorld();
        return world.PgdSystemManager.FindSystem<T>(false);
    }

    public static T GetOrCreateSystem<T>() where T : PgdSystemBase, new()
    {
        var world = GetWorld();
        
        T existingSystem = world.PgdSystemManager.FindSystem<T>(false);
        if (existingSystem != null)
        {
            return existingSystem;
        }
        
        T newSystem = new T();
        RegisterSystem(newSystem);
        return newSystem;
    }

    public static void RegisterSystem<T>(T system) where T : class
    {
        if (system is PgdSystemBase pgdSystem)
        {
            var world = GetWorld();
            world.RegisterSystem(pgdSystem);
        }
        else
        {
            Debug.LogWarning($"System {typeof(T).Name} is not a PGDSystem type. Cannot be registered to PGD World");
        }
    }
    
    #region World Extension
    public static IEntity InstantiateEntity(this IECSWorld world, IEntity templateEntity)
    {
        return PGDObjectPool.Instance.InstantiateEntity(templateEntity);
    }

    public static void InstantiateEntity(this IECSWorld world, IEntity templateEntity, List<IEntity> entities)
    {
        PGDObjectPool.Instance.InstantiateEntity(templateEntity, entities);
    }

    public static void InstantiateEntity(this IECSWorld world, IEntity templateEntity, NativeArray<IEntity> entities)
    {
        PGDObjectPool.Instance.InstantiateEntity(templateEntity, entities);
    }
    #endregion

    #region CommandQueue Extension
    public static CommandQueue GetCommandQueue()
    {
        return GetWorld().GetCommandQueue();
    }

    // 混合模式下，使用CommandQueue删除实体后，将Go回收；
    public static void DestroyEntity(this CommandQueue commandQueue, IEntity entity)
    {
        if (entity.TryGetComponent(out GoLink goLink))
        {
            PGDObjectPool.Instance.ReturnObject(goLink.gameObject, goLink.agentId, commandQueue);
        }
        else
        {
            commandQueue.DeleteEntity(entity.Id);
        }
    }
    #endregion

    #region Query Extension
    // 创建不含模板Entity的Query，避免模板Entity在System中受影响
    public static IQuery BuildHybridQuery()
    {
        return GetWorld().Query().WithoutAnyTags(ITags.Get<PrefabTag>());
    }

    public static IQuery<T1> BuildHybridQuery<T1>()  
        where T1 : struct, IComponent
    {
        return GetWorld().Query<T1>().WithoutAnyTags(ITags.Get<PrefabTag>());
    }

    public static IQuery<T1, T2> BuildHybridQuery<T1, T2>()  
        where T1 : struct, IComponent 
        where T2 : struct, IComponent
    {
        return GetWorld().Query<T1, T2>().WithoutAnyTags(ITags.Get<PrefabTag>());
    }

    public static IQuery<T1, T2, T3> BuildHybridQuery<T1, T2, T3>()  
        where T1 : struct, IComponent 
        where T2 : struct, IComponent 
        where T3 : struct, IComponent
    {
        return GetWorld().Query<T1, T2, T3>().WithoutAnyTags(ITags.Get<PrefabTag>());
    }
    
    
    public static IQuery<T1, T2, T3, T4> BuildHybridQuery<T1, T2, T3, T4>()  
        where T1 : struct, IComponent 
        where T2 : struct, IComponent 
        where T3 : struct, IComponent 
        where T4 : struct, IComponent
    {
        return GetWorld().Query<T1, T2, T3, T4>().WithoutAnyTags(ITags.Get<PrefabTag>());
    }
    
    public static IQuery<T1, T2, T3, T4, T5> BuildHybridQuery<T1, T2, T3, T4, T5>()  
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
        where T4 : struct, IComponent
        where T5 : struct, IComponent
    {
        return GetWorld().Query<T1, T2, T3, T4, T5>().WithoutAnyTags(ITags.Get<PrefabTag>());
    }
    public static IQuery<T1, T2, T3, T4, T5, T6> BuildHybridQuery<T1, T2, T3, T4, T5, T6>()  
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
        where T4 : struct, IComponent
        where T5 : struct, IComponent
        where T6 : struct, IComponent
    {
        return GetWorld().Query<T1, T2, T3, T4, T5, T6>().WithoutAnyTags(ITags.Get<PrefabTag>());
    }
    
    public static IQuery<T1, T2, T3, T4, T5, T6, T7> BuildHybridQuery<T1, T2, T3, T4, T5, T6, T7>()  
        where T1 : struct, IComponent 
        where T2 : struct, IComponent 
        where T3 : struct, IComponent 
        where T4 : struct, IComponent 
        where T5 : struct, IComponent 
        where T6 : struct, IComponent 
        where T7 : struct, IComponent
    {
        return GetWorld().Query<T1, T2, T3, T4, T5, T6, T7>().WithoutAnyTags(ITags.Get<PrefabTag>());
    }
    
    public static NativeArray<T> ToComponentDataArray<T>(this IQuery<T> query, Allocator allocator)
        where T : struct, IComponent
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        int count = query.EntityCount;

        var components = new NativeArray<T>(count, allocator);

        int index = 0;
        foreach (var entity in query.Entities)
        {
            components[index++] = entity.GetComponent<T>();
        }
        
        return components;
    }
    #endregion

    #region PGDJob Extension
    public static PGDJobManager GetJobManager()
    {
        if (defaultJobManager == null)
        {
            defaultJobManager = new PGDJobManager();
        }
        return defaultJobManager;
    }
    #endregion
    
    #region Singleton
    public static bool HasSingleton<T>() where T : struct, IComponent
    {
        var world = GetWorld();
        var query = world.Query<T>();
    
        return query.EntityCount == 1;
    }
    
    public static bool HasSingleton<T>(this IQuery query) where T : struct, IComponent
    {
        return query.EntityCount == 1;
    }

    public static T GetSingleton<T>() where T : struct, IComponent
    {
        var world = GetWorld();
        var query = world.Query<T>();
    
        int count = query.EntityCount;
        if (count == 0)
        {
            throw new InvalidOperationException($"Singleton {typeof(T).Name} does not exist");
        }
    
        if (count > 1)
        {
            throw new InvalidOperationException($"Component {typeof(T).Name} has ({count}) instances, is not singleton");
        }
    
        foreach (var entity in query.Entities)
        {
            return entity.GetComponent<T>();
        }
    
        return default(T);
    }

    public static T GetSingleton<T>(this IQuery query) where T : struct, IComponent
    {
        int count = query.EntityCount;
        if (count == 0)
        {
            throw new InvalidOperationException($"Singleton {typeof(T).Name} does not exist");
        }
        
        if (count > 1)
        {
            throw new InvalidOperationException($"Component {typeof(T).Name} has ({count}) instances, is not singleton");
        }
        
        foreach (var entity in query.Entities)
        {
            return entity.GetComponent<T>();
        }
        
        return default(T);
    }
    
    public static void SetSingleton<T>(T component) where T : struct, IComponent
    {
        var world = GetWorld();
        var query = world.Query<T>();
    
        int count = query.EntityCount;
        if (count == 0)
        {
            var entity = world.CreateEntity();
            entity.AddComponent(component);
        }
        else if (count == 1)
        {
            foreach (var entity in query.Entities)
            {
                entity.Set(component);
                break;
            }
        }
        else
        {
            throw new InvalidOperationException($"Component {typeof(T).Name} has ({count}) instances, is not singleton");
        }
    }
    
    public static void SetSingleton<T>(this IQuery query, T component) where T : struct, IComponent
    {
        var world = GetWorld();
        int count = query.EntityCount;
        if (count == 0)
        {
            var entity = world.CreateEntity();
            entity.AddComponent(component);
        }
        else if (count == 1)
        {
            foreach (var entity in query.Entities)
            {
                entity.Set(component);
                break;
            }
        }
        else
        {
            throw new InvalidOperationException($"Component {typeof(T).Name} has ({count}) instances, is not singleton");
        }
    }
    #endregion
}

