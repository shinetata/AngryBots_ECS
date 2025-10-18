/* COLLISION SYSTEM
 * This system manages the collision between bullets, enemies, and the player in a
 * very simple manner
 * 
 * WARNING: This code is incredibly inefficient. This was intentional as I wanted to
 * demonstrate how even poorly written code is very performant with Burst. You 
 * should NOT use this manner of collision detection in an actual game. Instead, use
 * DOTS physics or another performant solution
 */

using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using PGD;
using PGD.Jobs;

[BurstCompile] // Enable Burst compilation
partial class CollisionSystem : PGDSystem, IJobifiedSystem
{
    // The three queries this system will be using (Enemies, Bullets, and Player)
    IQuery enemyQuery;
    IQuery bulletQuery;
    IQuery playerQuery;

    private NativeArray<PGDLocalTransform> enemyTransforms;
    private NativeArray<PGDLocalTransform> bulletTransforms;
    private NativeArray<PGDLocalTransform> playerTransforms;

    private NativeArray<Health> enemyHealth;
    private NativeArray<Health> playerHealth;
    public Dependency dependency;
    
    // These variables will contain the unique collision radii for enemies and the player
    float enemyCollisionRadius;
    float playerCollisionRadius;
    private float enemyRadiusSpr;
    float playerRadiusSpr;
    [BurstCompile]
    protected override void OnAddWorld(IECSWorld world)
    {
        // If there are no enemies, this system doesn't need to run
        // Build and save the queries we will be using
        enemyQuery = PGDGameContext.BuildQuery().WithAllComponents(IComponents.Get<Health, EnemyTag, PGDLocalTransform>());
        bulletQuery = PGDGameContext.BuildQuery().WithAllComponents(IComponents.Get<TimeToLive, PGDLocalTransform>());
        playerQuery = PGDGameContext.BuildQuery().WithAllComponents(IComponents.Get<Health, PlayerTag, PGDLocalTransform>());
        // Grab the radii values from the Settings script
        enemyCollisionRadius = Settings.EnemyCollisionRadius;
        playerCollisionRadius = Settings.PlayerCollisionRadius;
        enemyRadiusSpr = enemyCollisionRadius * enemyCollisionRadius;
        playerRadiusSpr = playerCollisionRadius * playerCollisionRadius;
    }

    public void SetJobHandle(ref Dependency deps)
    {
        dependency = deps;
    }
    
    public void SyncDataBack()
    {
        int index = 0;
        foreach (var entity in playerQuery.Entities)
        {
            var health = entity.GetComponent<Health>();
            health.Value = playerHealth[index].Value;
            index++;
        }
        index = 0;
        foreach (var entity in enemyQuery.Entities)
        {
            var health = entity.GetComponent<Health>();
            health.Value = enemyHealth[index].Value;
            index++;
        }
    }

    public void Dispose()
    {
        if (enemyTransforms.IsCreated) enemyTransforms.Dispose();
        if (bulletTransforms.IsCreated) bulletTransforms.Dispose();
        if (playerTransforms.IsCreated) playerTransforms.Dispose();
        if (playerHealth.IsCreated) playerHealth.Dispose();
        if (enemyHealth.IsCreated) enemyHealth.Dispose();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        enemyTransforms = new NativeArray<PGDLocalTransform>(
            enemyQuery.Entities.Select(e => e.GetComponent<PGDLocalTransform>()).ToArray(), Allocator.TempJob);
        playerTransforms = new NativeArray<PGDLocalTransform>(
            playerQuery.Entities.Select(e => e.GetComponent<PGDLocalTransform>()).ToArray(), Allocator.TempJob);
        bulletTransforms = new NativeArray<PGDLocalTransform>(
            bulletQuery.Entities.Select(e => e.GetComponent<PGDLocalTransform>()).ToArray(), Allocator.TempJob);
        enemyHealth = new NativeArray<Health>(
            enemyQuery.Entities.Select(e => e.GetComponent<Health>()).ToArray(), Allocator.TempJob);
        playerHealth = new NativeArray<Health>(
            playerQuery.Entities.Select(e => e.GetComponent<Health>()).ToArray(), Allocator.TempJob);
        // Create a new CollisionJob for Enemies vs Bullets
        var jobEvB = new CollisionJob()
        {
            healthArray = playerHealth,
            transformArray = playerTransforms,
            transToTestAgainst = enemyTransforms,
            // Pass in the radius, which is squared for this algorithm
            radius = playerCollisionRadius * playerCollisionRadius,
            // Pass in a NativeArray of all of the Bullet transforms
        };
        // Schedule this as a multi-threaded job. We pass in the query we want this job to
        // use (in this case, all the enemies) and the state dependency so Unity can
        // help managing timing for us. We then save the return value to state.Dependency
        // to properly manage further dependency tracking (we will use this again below)
        dependency.jobs = jobEvB.ScheduleParallel(enemyQuery.EntityCount, dependency.jobs);
        // Create a new CollisionJob for Player vs Enemies
        var jobPvE = new CollisionJob()
        {
            healthArray = enemyHealth,
            transformArray = enemyTransforms,
            transToTestAgainst = bulletTransforms,
            // transToTestAgainst = enemyTransforms,
            // Pass in the radius, which is squared for this algorithm
            radius = enemyCollisionRadius * enemyCollisionRadius,
            // Pass in a NativeArray of all of the Enemy transforms
        };
        // Schedule this as a multi-threaded job, this time making it run on the player (or
        // players if we had more than one). Remember, state.Dependency is now referring to
        // the job we scheduled right before this one
        dependency.jobs = jobPvE.ScheduleParallel(playerQuery.EntityCount, dependency.jobs);
    }
}

[BurstCompile]
// This job is an IJobEntity even though we don't actually need the entity itself for the work
// we're doing. Instead, we chose this job type because the syntax is simple and convenient
partial struct CollisionJob : IJobParallelFor
{
    internal NativeArray<Health> healthArray;
    [ReadOnly]
    internal NativeArray<PGDLocalTransform> transformArray;
    // Collision radius
    public float radius;
    [ReadOnly]
    public NativeArray<PGDLocalTransform> transToTestAgainst;
    // Some boilerplate position checking that finds determines if two 2D circles overlap (in
    // this case, the radii around our entities)
    bool CheckCollision(float3 posA, float3 posB, float radiusSqr)
    {
        float3 delta = posA - posB;
        float distanceSquare = delta.x * delta.x + delta.z * delta.z;
        return distanceSquare <= radiusSqr;
    }

    public void Execute(int index)
    {
        var health = healthArray[index];
        var transform = transformArray[index];
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
        // let a difference system actually manage the results of an entity "dying"
        if (damage > 0)
            health.Value -= damage;
        healthArray[index] = health;
    }
}