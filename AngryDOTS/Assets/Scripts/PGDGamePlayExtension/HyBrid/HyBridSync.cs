using PGD;
using UnityEngine;

public class HybridTransformSync : PGDSystem<GoLink, PGDLocalTransform>
{
    protected override void OnUpdate()
    {
        GetQuery().ForEachEntity((ref GoLink goLink, ref PGDLocalTransform transform, IEntity entity) =>
        {
            if (goLink.isPrefab)
            {
                return;
            }
            goLink.transform.position = transform.Position;
            goLink.transform.rotation = transform.Rotation;
        });
    }
}