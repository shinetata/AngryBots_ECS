# PGD Job 并行运行时

PGD 现已支持与 DOTS `IJobEntity` 几乎一致的书写体验，但在运行期会生成并调度 Unity `IJobParallelFor`，自动完成组件获取、结果回写与资源回收。

## 系统编写

系统仍然继承现有的 `PGDSystem<>`，无需实现 `IJobifiedSystem` 或使用额外的基类。

```csharp
using PGD;
using PGD.Jobs;

partial class MoveForwardSystem : PGDSystem<PGDLocalTransform, MoveSpeed>
{
    protected override void OnUpdate()
    {
        var job = new MoveForwardJob
        {
            dt = PGDGameContext.Time.DeltaTime
        };

        job.ScheduleParallel();            // 可选: job.ScheduleParallel(world)
    }
}
```

## Job 声明

声明 `partial struct` 并实现 `IJobParallel`，`Execute` 方法的参数即组件需求。PGD 会自动分析签名，生成查询、调度包装和结果写回逻辑。

```csharp
using Unity.Burst;
using Unity.Mathematics;
using PGD.Jobs;

[BurstCompile]
[WithAll(typeof(EnemyTag))]
public partial struct TurnTowardTargetJob : IJobParallel
{
    public float3 targetPosition;

    void Execute(ref PGDLocalTransform transform)
    {
        float3 heading = targetPosition - transform.Position;
        heading.y = 0f;
        transform.Rotation = quaternion.LookRotation(heading, math.up());
    }
}
```

### 参数访问模式

| 写法                | 行为说明            |
| ------------------- | ------------------- |
| `T` / `in T`        | 仅读               |
| `ref T`             | 读写，调度后回写   |
| `out T`             | 仅写入，自动初始化 |

`IEntity` 参数目前未自动生成，需要时可手动使用 `context.EntityIds` 等实现。

附加的 `[WithAll]` / `[WithAny]` / `[WithNone]` 特性既支持组件也支持标签 (`ITag`)。

## 调度与回写

- 编译期 Source Generator 会为每个 `IJobParallel` 生成描述器、上下文以及 Unity `IJobParallelFor` 包装。
- 运行时 `PGDParallelJobScheduler` 负责：
  1. 使用生成的描述器收集实体及组件数据；
  2. 将 Job 包装为 `IJobParallelFor` 调用 `ScheduleParallel`；
  3. Job 完成后自动写回修改的组件并释放临时 `NativeArray`。
- 调度会自动创建一个 `PGDParallelJobFlushSystem`，由 `PGDJobManager` 驱动，在每帧结尾合并依赖并刷写结果。

## 当前限制

- 仅支持组件参数；`IEntity` 参数暂未自动生成，需要后续扩展。
- Job 的组件拷贝在调度前会转存到 `NativeArray`，与 DOTS 一样需要注意数据体量，但无需手写拷贝逻辑。
- 若需要自定义世界，可调用 `job.ScheduleParallel(world)` 手动指定。

