using PGD;
using Unity.Mathematics;
using UnityEngine;

public struct PGDLocalTransform : IComponent
{
    public float3 Position;
    public float Scale; 
    public Quaternion Rotation;
}

public struct PrefabTag : ITag {}