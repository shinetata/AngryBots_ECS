using PGD;
using Unity.Mathematics;

public struct PGDLocalTransform : IComponent
{
    public float3 Position;
    public float Scale; 
    public quaternion Rotation;
}