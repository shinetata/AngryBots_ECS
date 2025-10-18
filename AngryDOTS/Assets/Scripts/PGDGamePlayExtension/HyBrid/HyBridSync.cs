using PGD;
using UnityEngine;

public class HybridTransformSync : PGDSystem<GoLink>
{
    protected override void OnUpdate()
    {
        GetQuery().ForEachEntity((ref GoLink goLink, IEntity entity) =>
        {
            if (entity.TryGetComponent<PGDPosition>(out var pos))
            {
                goLink.transform.position = new Vector3(pos.x, pos.y, pos.z);
            }
            if (entity.TryGetComponent<PGDRotation>(out var rot))
            {
                goLink.transform.rotation = new Quaternion(rot.x, rot.y, rot.z, rot.w);
            }
        });
    }
}