/* TIMED DESTROY SYSTEM
 * This system finds any entity that has a TimeToLive component. It reduces the Value of
 * the TimeToLive component, and then destroys any entities that are out of time. Since 
 * destroying entities causes structural changes to memory, they are tricky to do in a
 * multi-threaded job. As such, this code runs on the main thread and uses an 
 * EntityCommandBuffer to queue up all the Destroy commands. In this project, the one 
 * entity type it will affect are bullets
 */
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using PGD;

[BurstCompile] // Enable Burst compilation
public partial class TimedDestroySystem : PGDSystem
{
    [BurstCompile]
    protected override void OnUpdate()
    {
        {
            CommandQueue commandBuffer = PGDGameContext.GetCommandQueue();
            PGDGameContext.GetWorld().Query<TimeToLive>().ForEachEntity((ref TimeToLive timer, IEntity entity) =>
            {
                // Access the value of timer (local name for the TimeToLive component). Note how this
                // syntax uses "ValueRW" instead of just "Value". This is needed inside a foreach to 
                // make changes to component data
                timer.Value -= Time.fixedDeltaTime;
                // If the TimeToLive value (read-only here) is less than 0...
                if (timer.Value < 0f)
                {
                    // Queue the destruction of this entity into the command buffer
                    commandBuffer.DestroyEntity(entity);
                }
            });
            // After the foreach, playback the buffer, destroying the entities
            commandBuffer.Apply();
        }
    // Once the "using" block closes, the CommandBuffer will be cleaned up
    }
}