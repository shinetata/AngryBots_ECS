/* COLLISION SYSTEM
 * This system manages the collision between bullets, enemies, and the player in a
 * very simple manner
 * 
 * WARNING: This code is incredibly inefficient. This was intentional as I wanted to
 * demonstrate how even poorly written code is very performant with Burst. You 
 * should NOT use this manner of collision detection in an actual game. Instead, use
 * DOTS physics or another performant solution
 */

using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using PGD;
using PGD.Jobs;

[BurstCompile] // Enable Burst compilation
[UpdateSystemAfter(typeof(TurnTowardsPlayerSystem))]
partial class CollisionSystem : PGDJobSystemBase
{
    // The three queries this system will be using (Enemies, Bullets, and Player)
    IQuery enemyQuery;
    IQuery bulletQuery;
    IQuery playerQuery;

    // These variables will contain the unique collision radii for enemies and the player
    float enemyCollisionRadius;
    float playerCollisionRadius;

    [BurstCompile]
    protected override void OnAddWorld(IECSWorld world)
    {
        // If there are no enemies, this system doesn't need to run
        // Build and save the queries we will be using
        enemyQuery = PGDGameContext.BuildHybridQuery()
            .WithAllComponents(IComponents.Get<Health, EnemyTag, PGDLocalTransform>());
        bulletQuery = PGDGameContext.BuildHybridQuery()
            .WithAllComponents(IComponents.Get<TimeToLive, PGDLocalTransform>());
        playerQuery = PGDGameContext.BuildHybridQuery()
            .WithAllComponents(IComponents.Get<Health, PlayerTag, PGDLocalTransform>());

        // Grab the radii values from the Settings script
        enemyCollisionRadius = Settings.EnemyCollisionRadius;
        playerCollisionRadius = Settings.PlayerCollisionRadius;
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        // Extract bullet transforms to NativeArray (needed for collision testing)
        int bulletCount = bulletQuery.EntityCount;
        if (bulletCount == 0) return; // No bullets, no collision possible
        
        var bulletTransforms = new NativeArray<PGDLocalTransform>(bulletCount, Allocator.TempJob);
        int index = 0;
        foreach (var entity in bulletQuery.Entities)
        {
            bulletTransforms[index] = entity.GetComponent<PGDLocalTransform>();
            index++;
        }

        // Create a CollisionJob for Enemies vs Bullets
        var jobEvB = new CollisionJob
        {
            // Pass in the radius, which is squared for this algorithm
            radius = enemyCollisionRadius * enemyCollisionRadius,
            // Pass in a NativeArray of all of the Bullet transforms
            transToTestAgainst = bulletTransforms
        };
        // Schedule this as a multi-threaded job on enemies
        jobEvB.ScheduleParallel(enemyQuery);
        
        // NOTE: bulletTransforms will be automatically disposed by Unity Job System
        // due to [DeallocateOnJobCompletion] attribute on the transToTestAgainst field

        // Extract enemy transforms to NativeArray (needed for player collision testing)
        int enemyCount = enemyQuery.EntityCount;
        var enemyTransforms = new NativeArray<PGDLocalTransform>(enemyCount, Allocator.TempJob);
        index = 0;
        foreach (var entity in enemyQuery.Entities)
        {
            enemyTransforms[index] = entity.GetComponent<PGDLocalTransform>();
            index++;
        }

        // Create a CollisionJob for Player vs Enemies
        var jobPvE = new CollisionJob
        {
            // Pass in the radius, which is squared for this algorithm
            radius = playerCollisionRadius * playerCollisionRadius,
            // Pass in a NativeArray of all of the Enemy transforms
            transToTestAgainst = enemyTransforms
        };
        // Schedule this as a multi-threaded job on players
        // The dependency is managed automatically by the framework
        jobPvE.ScheduleParallel(playerQuery);
        
        // NOTE: enemyTransforms will be automatically disposed by Unity Job System
        // due to [DeallocateOnJobCompletion] attribute on the transToTestAgainst field
    }
}

[BurstCompile]
// This job uses IJobParallel similar to DOTS IJobEntity
// The Execute signature defines which components the job needs
public partial struct CollisionJob : IJobParallel
{
    // Collision radius (squared for performance)
    public float radius;

    // Native Array of transforms we will be testing against
    // Marked with DeallocateOnJobCompletion so it cleans up automatically
    [DeallocateOnJobCompletion]
    [ReadOnly]
    public NativeArray<PGDLocalTransform> transToTestAgainst;

    // Execute is called once for each entity that matches the query
    // ref Health = writable component (auto write-back)
    // in PGDLocalTransform = read-only component
    void Execute(ref Health health, in PGDLocalTransform transform)
    {
        float damage = 0f;

        // Loop through all the transforms we want to test this entity against (remember, this
        // way of checking collisions is intentionally simple and inefficient)
        for (int i = 0; i < transToTestAgainst.Length; i++)
        {
            // If there is a collision, increase the damage of this entity
            if (CheckCollision(transform.Position, transToTestAgainst[i].Position, radius))
                damage += 1;
        }

        // If any damage was taken, reduce the entity's health by that amount. We will
        // let a different system actually manage the results of an entity "dying"
        if (damage > 0)
            health.Value -= damage;
    }

    // Some boilerplate position checking that determines if two 2D circles overlap (in
    // this case, the radii around our entities)
    bool CheckCollision(float3 posA, float3 posB, float radiusSqr)
    {
        float3 delta = posA - posB;
        float distanceSquare = delta.x * delta.x + delta.z * delta.z;
        return distanceSquare <= radiusSqr;
    }
}
