# PGD Jobs 使用示例 - 像 DOTS 一样简洁！

## 核心理念：开发者只需要把 IJobEntity 改成 IJobParallel

框架会自动处理所有复杂的事情！

---

## 对比：DOTS vs PGD Jobs

### DOTS 版本

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
partial struct MoveForwardSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var job = new MoveForwardJob
        {
            dt = SystemAPI.Time.DeltaTime
        };
        job.ScheduleParallel();  // ← 简洁的调度
    }
}

[BurstCompile]
[WithAll(typeof(MoveForward))]
public partial struct MoveForwardJob : IJobEntity  // ← IJobEntity
{
    public float dt;
    
    // 直接操作组件
    void Execute(in MoveSpeed speed, ref LocalTransform transform)
    {
        transform.Position += dt * speed.Value * math.forward(transform.Rotation);
    }
}
```

### PGD Jobs 版本（期望的简洁版）

```csharp
using Unity.Burst;
using Unity.Mathematics;
using PGD;
using PGD.Jobs;

[BurstCompile]
partial class MoveForwardSystem : PGDJobSystemBase
{
    [BurstCompile]
    protected override void OnUpdate()
    {
        var job = new MoveForwardJob
        {
            dt = PGDGameContext.Time.DeltaTime
        };
        job.ScheduleParallel();  // ← 一样简洁！
    }
}

[BurstCompile]
[WithAll(typeof(MoveForward))]
public partial struct MoveForwardJob : IJobParallel  // ← 只改这里！
{
    public float dt;
    
    // 直接操作组件（和 DOTS 一样）
    void Execute(in MoveSpeed speed, ref PGDLocalTransform transform)
    {
        transform.Position += dt * speed.Value * math.forward(transform.Rotation);
    }
}
```

**唯一的区别**：
- `IJobEntity` → `IJobParallel`
- `LocalTransform` → `PGDLocalTransform`

**其他完全一样！**

---

## Source Generator 自动生成的代码

开发者看不到，框架自动生成：

### 1. 扩展方法

```csharp
// MoveForwardJob_Generated.g.cs
file static class MoveForwardJobExtensions
{
    private static NativeArray<MoveSpeed> s_speedArray;
    private static NativeArray<PGDLocalTransform> s_transformArray;
    
    public static void ScheduleParallel(this ref MoveForwardJob job)
    {
        // 自动获取 World 和查询
        var world = PGDJobSystemBase.CurrentWorld;
        var query = world.Query<MoveSpeed, PGDLocalTransform>();
        
        // 自动提取数据到 NativeArray
        s_speedArray = new NativeArray<MoveSpeed>(
            query.Entities.Select(e => e.GetComponent<MoveSpeed>()).ToArray(),
            Allocator.TempJob);
        s_transformArray = new NativeArray<PGDLocalTransform>(
            query.Entities.Select(e => e.GetComponent<PGDLocalTransform>()).ToArray(),
            Allocator.TempJob);
        
        // 自动创建包装 Job
        var wrapper = new MoveForwardJob_Wrapper
        {
            innerJob = job,
            speedArray = s_speedArray,
            transformArray = s_transformArray
        };
        
        // 调度
        var handle = wrapper.ScheduleParallel(query.EntityCount, PGDJobSystemBase.CurrentDependency);
        
        // 注册到 Job 管理器，自动完成和清理
        PGDJobManager.RegisterJob(handle, () =>
        {
            // 自动写回数据
            int index = 0;
            foreach (var entity in query.Entities)
            {
                ref var transform = ref entity.GetComponent<PGDLocalTransform>();
                transform = s_transformArray[index];
                index++;
            }
            
            // 自动清理
            if (s_speedArray.IsCreated) s_speedArray.Dispose();
            if (s_transformArray.IsCreated) s_transformArray.Dispose();
        });
    }
}
```

### 2. 包装 Job

```csharp
// 自动生成的包装 Job
[BurstCompile]
file struct MoveForwardJob_Wrapper : IJobParallelFor
{
    public MoveForwardJob innerJob;
    [ReadOnly] public NativeArray<MoveSpeed> speedArray;
    public NativeArray<PGDLocalTransform> transformArray;
    
    public void Execute(int index)
    {
        // 从 NativeArray 提取参数
        var speed = speedArray[index];
        var transform = transformArray[index];
        
        // 调用原始 Execute（模拟 ref 参数）
        innerJob.ExecuteGenerated(in speed, ref transform);
        
        // 写回
        transformArray[index] = transform;
    }
}

// 自动生成的适配方法
partial struct MoveForwardJob
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExecuteGenerated(in MoveSpeed speed, ref PGDLocalTransform transform)
    {
        // 调用用户的 Execute 方法
        Execute(in speed, ref transform);
    }
}
```

---

## 完整示例

### 示例 1：MoveForwardSystem

```csharp
using Unity.Burst;
using Unity.Mathematics;
using PGD;
using PGD.Jobs;

// System - 超级简洁
[BurstCompile]
partial class MoveForwardSystem : PGDJobSystemBase
{
    protected override void OnUpdate()
    {
        var job = new MoveForwardJob
        {
            dt = PGDGameContext.Time.DeltaTime
        };
        job.ScheduleParallel();
    }
}

// Job - 像 DOTS 一样
[BurstCompile]
[WithAll(typeof(MoveForward))]
public partial struct MoveForwardJob : IJobParallel
{
    public float dt;
    
    void Execute(in MoveSpeed speed, ref PGDLocalTransform transform)
    {
        transform.Position += dt * speed.Value * math.forward(transform.Rotation);
    }
}
```

### 示例 2：TurnTowardsPlayerSystem

```csharp
using Unity.Burst;
using Unity.Mathematics;
using PGD;
using PGD.Jobs;

[BurstCompile]
partial class TurnTowardsPlayerSystem : PGDJobSystemBase
{
    protected override void OnUpdate()
    {
        if (Settings.IsPlayerDead()) return;
        
        var job = new TurnTowardTargetJob
        {
            targetPosition = Settings.PlayerPosition
        };
        job.ScheduleParallel();
    }
}

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

### 示例 3：CollisionSystem（带自定义查询）

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using PGD;
using PGD.Jobs;

[BurstCompile]
partial class CollisionSystem : PGDJobSystemBase
{
    protected override void OnUpdate()
    {
        // 自定义查询（可选）
        var bulletQuery = World.Query<TimeToLive, PGDLocalTransform>();
        var bulletTransforms = bulletQuery.ToNativeArray<PGDLocalTransform>(Allocator.TempJob);
        
        var job = new CollisionJob
        {
            radius = Settings.EnemyCollisionRadius * Settings.EnemyCollisionRadius,
            transToTestAgainst = bulletTransforms
        };
        
        // 自动处理 enemyQuery
        job.ScheduleParallel();
        
        // bulletTransforms 会自动在 job 完成后清理
    }
}

[BurstCompile]
public partial struct CollisionJob : IJobParallel
{
    public float radius;
    [DeallocateOnJobCompletion]
    [ReadOnly] public NativeArray<PGDLocalTransform> transToTestAgainst;
    
    void Execute(ref Health health, in PGDLocalTransform transform)
    {
        float damage = 0f;
        for (int i = 0; i < transToTestAgainst.Length; i++)
        {
            if (CheckCollision(transform.Position, transToTestAgainst[i].Position, radius))
                damage += 1;
        }
        if (damage > 0)
            health.Value -= damage;
    }
    
    bool CheckCollision(float3 posA, float3 posB, float radiusSqr)
    {
        float3 delta = posA - posB;
        float distanceSquare = delta.x * delta.x + delta.z * delta.z;
        return distanceSquare <= radiusSqr;
    }
}
```

---

## System 基类选择

### 选项 1：使用 PGDJobSystemBase（推荐）

```csharp
partial class MySystem : PGDJobSystemBase
{
    protected override void OnUpdate()
    {
        var job = new MyJob { /* ... */ };
        job.ScheduleParallel();  // 自动处理一切
    }
}
```

- ✅ 自动实现 `IJobifiedSystem`
- ✅ 自动管理依赖链
- ✅ 自动处理数据写回和清理
- ✅ 提供 `World` 属性

### 选项 2：使用 PGDSystem（如果需要更多控制）

```csharp
partial class MySystem : PGDSystem<Component1, Component2>
{
    protected override void OnUpdate()
    {
        var job = new MyJob { /* ... */ };
        job.ScheduleParallel(GetQuery());  // 传入自定义查询
    }
}
```

---

## Execute 方法参数规则

Source Generator 会根据参数类型和修饰符自动推导：

### 1. 组件参数

```csharp
void Execute(ref Transform t, in Speed s)
//           ^^^            ^^
//           可写           只读
```

- `ref` → 可修改的组件（自动写回）
- `in` → 只读组件（不写回）
- 无修饰符 → 按值传递（不推荐）

### 2. 实体参数（可选）

```csharp
void Execute(ref Transform t, IEntity entity)
//                            ^^^^^^^
//                            访问实体本身
```

- 可以销毁实体、添加/删除组件等

### 3. 索引参数（可选）

```csharp
void Execute(ref Transform t, int entityIndex)
//                            ^^^
//                            当前实体索引
```

---

## 查询过滤

使用属性控制查询：

```csharp
[WithAll(typeof(Tag1), typeof(Tag2))]      // 必须有这些组件
[WithAny(typeof(Tag3), typeof(Tag4))]      // 至少有一个
[WithNone(typeof(Tag5))]                    // 不能有这些组件
public partial struct MyJob : IJobParallel
{
    void Execute(ref Transform t) { }
}
```

---

## 与 DOTS 的完整对比

| 特性 | DOTS | PGD Jobs | 说明 |
|------|------|----------|------|
| Job 接口 | `IJobEntity` | `IJobParallel` | 唯一需要改的 |
| 调度方式 | `job.ScheduleParallel()` | `job.ScheduleParallel()` | 完全一样 |
| Execute 签名 | `(ref/in Component)` | `(ref/in Component)` | 完全一样 |
| 查询过滤 | `[WithAll]` 等 | `[WithAll]` 等 | 完全一样 |
| 依赖管理 | 自动 | 自动 | 框架处理 |
| 数据提取 | 自动 | 自动 | 框架处理 |
| 数据写回 | 自动 | 自动 | 框架处理 |
| 资源清理 | 自动 | 自动 | 框架处理 |
| System 基类 | `ISystem` | `PGDJobSystemBase` | 名字不同 |

---

## 开发者只需要做什么？

### 步骤 1：定义 Job

```csharp
[WithAll(typeof(SomeTag))]  // 可选：过滤
public partial struct MyJob : IJobParallel  // ← 改这里
//                            ^^^^^^^^^^^^
{
    public float someData;  // Job 的数据成员
    
    // Execute 参数决定了查询哪些组件
    void Execute(ref Component1 c1, in Component2 c2)
    {
        // 直接修改组件
        c1.value += someData * c2.multiplier;
    }
}
```

### 步骤 2：在 System 中调度

```csharp
partial class MySystem : PGDJobSystemBase
{
    protected override void OnUpdate()
    {
        var job = new MyJob { someData = 123 };
        job.ScheduleParallel();  // 就这么简单！
    }
}
```

### 步骤 3：（没有步骤 3 了！）

框架自动处理：
- ✅ 查询实体
- ✅ 提取数据到 NativeArray
- ✅ 调度 IJobParallelFor
- ✅ 完成时写回数据
- ✅ 清理 NativeArray
- ✅ 管理依赖链

---

## 总结

**开发者体验**：
```
IJobEntity  →  IJobParallel
就这么简单！
```

**框架负责**：
- 所有的 NativeArray 管理
- 所有的数据提取和写回
- 所有的依赖管理
- 所有的资源清理

**Source Generator 生成**：
- 扩展方法
- 包装 Job
- 查询代码
- 数据提取代码
- 数据写回代码
- 清理代码

开发者完全不需要看到或关心这些实现细节！
