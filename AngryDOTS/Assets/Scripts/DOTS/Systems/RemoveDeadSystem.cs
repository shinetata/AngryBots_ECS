/* REMOVE DEAD SYSTEM
 * This system finds any entity that has a Health component and checks to see if the entity is
 * "dead" (health <= 0). If so, this system destroys that entity using a CommandBuffer. In this 
 * project, the two entity types it will affect are players and enemies
 */
using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using PGD;

[BurstCompile]
partial class RemoveDeadSystem : PGDSystem
{
    [BurstCompile]
    protected override void OnAddWorld(IECSWorld world)
    {
        // If there are no entities with Health, don't run this system

    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        {
            CommandQueue commandBuffer = PGDGameContext.GetCommandQueue();
            PGDGameContext.BuildHybridQuery<Health, EnemyTag>().ForEachEntity((ref Health health, ref EnemyTag _, 
                IEntity entity) =>
            {
                // Access the value of health (local name for the Health component). Note how this
                // syntax uses "ValueRO" instead of just "Value". This is needed inside a foreach to 
                // specify the type of access needed for the component data
                if (health.Value <= 0f)
                {
                    // If the health is <= 0, queue the entity to be destroy
                    commandBuffer.DestroyEntity(entity);
                }
            });
            // After the foreach, playback the buffer, destroying the entities
            commandBuffer.Apply();
        }
    // Once the "using" block closes, the CommandBuffer will be cleaned up
    }
}
