using System;
using UnityEngine;
using PGD;


public abstract class PGDHyBrid<TAuthoring> where TAuthoring : Component
{
    private TAuthoring _contextAuthoring;

    public abstract void Handle(TAuthoring authoring);

    internal void SetContext(TAuthoring authoring)
    {
        _contextAuthoring = authoring;
    }

    protected IEntity GetHyBridEntity(TAuthoring authoring)
    {
        var prefab = authoring.gameObject;
        return GetHyBridEntity(prefab);
    }

    protected IEntity GetHyBridEntity()
    {
        if (_contextAuthoring == null)
            throw new InvalidOperationException("PGDHyBrid: Context not set.");
        return GetHyBridEntity(_contextAuthoring);
    }

    protected IEntity GetHyBridEntity(GameObject prefab)
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
public sealed class PGDHyBridOrderAttribute : Attribute
{
    public readonly int Order;
    public PGDHyBridOrderAttribute(int order) => Order = order;
}
