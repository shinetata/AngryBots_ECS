/* TURN TOWARDS PLAYER SYSTEM
 * This system finds any entity that has a LocalTransform and EnemyTag component 
 * and turns the entity to point its Z axis towards a target (in this case, the player). 
 * In this project, the one entity type it will affect are enemies
 */
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using PGD;
using PGD.Jobs;
using Unity.Collections;
using System.Linq;

[BurstCompile] // Enable Burst compilation
[PGDUpdateAfter(typeof(RemoveDeadSystem))]
partial class TurnTowardsPlayerSystem : PGDSystem<PGDLocalTransform, EnemyTag>, IJobifiedSystem
{
    public Dependency dependency;
    private NativeArray<PGDLocalTransform> transforms;
    
    [BurstCompile]
    protected override void OnAddWorld(IECSWorld world)
    {
        // Do not run this system if there are no enemy entities
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
            ref EnemyTag _,
            IEntity entity
            ) =>
        {
            transform.Rotation = transforms[index].Rotation;
            index++;
        });
    }

    public void Dispose()
    {
        if (transforms.IsCreated) 
            transforms.Dispose();
    }

    protected override void OnUpdate()
    {
        // Access this data prevents Burst compilation. This prevents jobs from being
        // scheduled if the player is dead
        if (Settings.IsPlayerDead())
            return;
        transforms = new NativeArray<PGDLocalTransform>(
            GetQuery().Entities.Select(e => e.GetComponent<PGDLocalTransform>()).ToArray(), Allocator.TempJob);
        // Create a TurnTowardsTargetJob and pass it the player's position. 
        var TurnTowardsPlayerJob = new TurnTowardTargetJob
        {
            transformArray = transforms,
            targetPosition = Settings.PlayerPosition // This code also prevents Burst
        };
        // Schedule this job as multi-threaded. Since we don't pass in a query, the
        // job itself will contain the query
        dependency.jobs = TurnTowardsPlayerJob.ScheduleParallel(GetQuery().EntityCount, dependency.jobs);
    }
}

[BurstCompile]
// More information on IJobEntity can be found in the MoveForwardSystem.cs
// The query for this job is defined by the Execute() method. Since we only want this to
// process enemy entities, we specify the need for the EnemyTag as well (above). If we 
// wanted this job to be more generic, we would need to create a query in the System code
// above and pass it in when scheduling this job (you can see an example of that in the
// CollisionSytem.cs)
public partial struct TurnTowardTargetJob : IJobParallelFor
{
    internal NativeArray<PGDLocalTransform> transformArray;
    // The position that the entities will turn towards
    public float3 targetPosition;
    public // Execute will run once for each entity that matches the query
    void Execute(int index)
    {
        var transform = transformArray[index];
        // Change the entity's rotation to point at the position
        float3 heading = targetPosition - transform.Position;
        heading.y = 0f;
        transform.Rotation = quaternion.LookRotation(heading, math.up());
        transformArray[index] = transform;
    }
}
