using System;
using UnityEngine;
using PGD;


public abstract class PGDHybrid<TAuthoring> where TAuthoring : Component
{
    private TAuthoring _contextAuthoring;

    public abstract void Handle(TAuthoring authoring);

    internal void SetContext(TAuthoring authoring)
    {
        _contextAuthoring = authoring;
    }

    protected IEntity GetHybridEntity(TAuthoring authoring)
    {
        var prefab = authoring.gameObject;
        return GetHybridEntity(prefab);
    }

    protected IEntity GetHybridEntity()
    {
        if (_contextAuthoring == null)
            throw new InvalidOperationException("PGDHybrid: Context not set.");
        return GetHybridEntity(_contextAuthoring);
    }

    protected IEntity GetHybridEntity(GameObject prefab)
    {
        if (!prefab) throw new ArgumentNullException(nameof(prefab));
        if (!PGDObjectPool.Instance.IsInitialized(prefab))
        {
            PGDObjectPool.Instance.InitializePool(prefab);
        }
        PGDObjectPool.Instance.TryGetTemplateEntity(prefab, out var entity);
        return entity;
    }

    protected bool AddComponent<T>(IEntity entity, T component)
        where T : struct, IComponent
    {
        return entity.AddComponent(in component);
    }

    protected bool AddComponent<T>(IEntity entity)
        where T : struct, IComponent
    {
        return entity.AddComponent<T>();
    }

    protected void SetComponent<T>(IEntity entity, T component)
        where T : struct, IComponent
    {
        entity.Set(in component);
    }
}

/// <summary>控制同一 Authoring 上多个 Hybrid 的顺序（数值越小越早）。</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PGDHybridOrderAttribute : Attribute
{
    public readonly int Order;
    public PGDHybridOrderAttribute(int order) => Order = order;
}
