using PGD;
using UnityEngine;

public class HybridTransformSync : PGDSystem<GoLink>
{
    protected override void OnUpdate()
    {
        GetQuery().ForEachEntity((ref GoLink goLink, IEntity entity) =>
        {
            if (goLink.isPrefab)
            {
                return;
            }
            if (entity.TryGetComponent<PGDLocalTransform>(out var trans))
            {
                goLink.transform.position = trans.Position;
                goLink.transform.rotation = trans.Rotation;
            }
        });
    }
}