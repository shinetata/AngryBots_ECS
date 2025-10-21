/* MOVE FORWARD SYSTEM
 * This system finds any entity that has a PGDLocalTransform, MoveForward, and MoveSpeed
 * component and moves the entity forward. In this project, the two entity types it will
 * affect are bullets and enemies.
 */

using Unity.Burst;
using Unity.Mathematics;
using PGD;
using PGD.Jobs;

[BurstCompile]
partial class MoveForwardSystem : PGDSystem
{
    [BurstCompile]
    protected override void OnUpdate()
    {
        var job = new MoveForwardJob
        {
            dt = PGDGameContext.Time.DeltaTime
        };

        job.ScheduleParallel();
    }
}

[BurstCompile]
[WithAll(typeof(MoveForward))]
public partial struct MoveForwardJob : IJobParallel
{
    public float dt;

    void Execute(ref PGDLocalTransform transform, in MoveSpeed speed)
    {
        transform.Position = transform.Position + dt * speed.Value * math.forward(transform.Rotation);
    }
}