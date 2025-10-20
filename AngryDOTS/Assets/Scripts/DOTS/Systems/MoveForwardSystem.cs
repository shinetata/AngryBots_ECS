/* MOVE FORWARD SYSTEM
 * This system finds any entity that has a LocalTransform, MoveForward, and MoveSpeed
 * component and moves the entity forward. In this project, the two entity types it will 
 * affect are bullets and enemies
 */

using System.Linq;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using PGD;
using Unity.Collections;
using PGD.Jobs;

[BurstCompile] // Enable Burst compilation
partial class MoveForwardSystem : PGDSystem<PGDLocalTransform, MoveSpeed>, IJobifiedSystem
{
    private NativeArray<PGDLocalTransform> transforms;
    private NativeArray<MoveSpeed> speeds;
    public Dependency dependency;

    [BurstCompile]
    protected override void OnAddWorld(IECSWorld world)
    {
        // If there are no entities with MoveSpeed, this system doesn't need to run
    }

    public void SetJobHandle(ref Dependency deps)
    {
        dependency = deps;
    }

    public void SyncDataBack()
    {
        int index = 0;
        GetQuery().ForEachEntity((
            ref PGDLocalTransform transform,
            ref MoveSpeed speed,
            IEntity entity
            ) =>
        {
            transform.Position = transforms[index].Position;
            index++;
        });
    }

    public void Dispose()
    {
        if (transforms.IsCreated) transforms.Dispose();
        if (speeds.IsCreated) speeds.Dispose();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        transforms = new NativeArray<PGDLocalTransform>(
                GetQuery().Entities.Select(e => e.GetComponent<PGDLocalTransform>()).ToArray(), Allocator.TempJob);
        speeds = new NativeArray<MoveSpeed>(
            GetQuery().Entities.Select(e => e.GetComponent<MoveSpeed>()).ToArray(), Allocator.TempJob);
        
        // Create a MoveForwardJob and tell it the amount of time that has passed
        // since the last time this system updated
        var MoveForwardJob = new MoveForwardJob
        {
            speedArray = speeds,
            transformArray = transforms,
            dt = PGDGameContext.Time.DeltaTime
        };
        // Schedule this job as multi-threaded. Since we don't pass in a query, the
        // job itself will contain the query
        dependency.jobs = MoveForwardJob.ScheduleParallel(GetQuery().EntityCount, dependency.jobs);
    }
}

[BurstCompile]
// One of the reasons IJobEntity is convenient is that it can infer the entity query for you
// based on the arguments of its Execute method. In this case, the job will work on entities
// that have a MoveSpeed and a LocalTransform component. Additionally, since this system should
// only work on entities that specifically need to move forward (as opposed to other types of
// movement), we specify that MoveForward is also required on the line above. We don't put that
// in the Execute method, however, since we don't need to access MoveFoward, we just need to make
// sure it is there. Think of MoveForward like a tag, since it doesn't actually contain any data
public partial struct MoveForwardJob : IJobParallelFor
{
    [ReadOnly]
    internal NativeArray<MoveSpeed> speedArray;
    internal NativeArray<PGDLocalTransform> transformArray;
    public float dt; // The amount of time that has passed since the last update
    public // Execute will run once for each entity that matches the query
    void Execute(int index)
    {
        var speed = speedArray[index];
        var transform = transformArray[index];
        // Change the entities position in the forward direction based on its speed and time
        transform.Position = transform.Position + dt * speed.Value * math.forward(transform.Rotation);
        transformArray[index] = transform;
    }
}
