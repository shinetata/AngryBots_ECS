using PGD;
using UnityEngine;

[PGDDisableAutoRegister]
public class HybridTransformSync : PGDSystem<GoLink, PGDLocalTransform>
{
    protected override void OnUpdate()
    {
        GetQuery().WithoutAnyTags(ITags.Get<PrefabTag>()).ForEachEntity((
            ref GoLink goLink, ref PGDLocalTransform transform, IEntity entity) =>
        {
            goLink.transform.position = transform.Position;
            goLink.transform.rotation = transform.Rotation;
        });
    }
}
