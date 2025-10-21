## 📋 Source Generator 完整任务清单

基于这个 PGD Job 框架的设计，Source Generator 需要完成以下工作：

### 🎯 核心职责概览

Source Generator 的作用是：**分析用户编写的 `IJobParallel` Job，自动生成所有胶水代码，让用户只需要关注业务逻辑。**

---

### 1️⃣ **Job 检测与验证**

- [ ] 扫描程序集中所有实现 `IJobParallel` 接口的 `partial struct`
- [ ] 验证 struct 必须是 `partial`（否则无法生成代码到同一类型）
- [ ] 验证必须有 `Execute` 方法（可以是任意访问级别）
- [ ] 验证 `Execute` 方法参数类型合法（必须是组件类型）
- [ ] 生成友好的编译错误提示（如果不符合要求）

**示例检测目标：**
```csharp
// ✅ 应该被检测到
public partial struct MoveForwardJob : IJobParallel
{
    void Execute(ref PGDLocalTransform transform, in MoveSpeed speed) { }
}

// ❌ 应该报错：不是 partial
public struct BadJob : IJobParallel { }
```

---

### 2️⃣ **分析 Execute 方法签名**

- [ ] 提取所有参数的类型和访问模式：
  - `in T` 或 `T` → **ReadOnly**（只读）
  - `ref T` → **ReadWrite**（需要回写）
  - `out T` → **WriteOnly**（只写，自动初始化）
- [ ] 记录参数顺序（生成代码时需要按顺序传递）
- [ ] 验证参数类型是有效的组件类型

**需要提取的信息：**
```csharp
void Execute(ref PGDLocalTransform transform, in MoveSpeed speed)
//           ^^^                         ^^^
//          模式:ReadWrite              模式:ReadOnly
//          类型:PGDLocalTransform      类型:MoveSpeed
```

---

### 3️⃣ **分析过滤特性（Attributes）**

- [ ] 解析 `[WithAll(typeof(...))]` 特性
- [ ] 解析 `[WithAny(typeof(...))]` 特性  
- [ ] 解析 `[WithNone(typeof(...))]` 特性
- [ ] 支持多个同类型特性叠加
- [ ] 区分组件（`IComponent`）和标签（`ITag`）

**示例：**
```csharp
[WithAll(typeof(EnemyTag))]
[WithNone(typeof(DeadTag), typeof(StunnedTag))]
public partial struct TurnTowardTargetJob : IJobParallel
```

---

### 4️⃣ **生成查询逻辑**

- [ ] 生成基于签名和特性的实体查询代码
- [ ] 根据 Execute 参数生成主要查询条件
- [ ] 根据特性添加额外的过滤条件
- [ ] 生成查询实体列表的代码

**生成类似：**
```csharp
private static List<IEntity> QueryEntities(IECSWorld world)
{
    var entities = new List<IEntity>();
    foreach (var entity in world.Entities)
    {
        // 检查 Execute 参数的组件
        if (!entity.Has<PGDLocalTransform>()) continue;
        if (!entity.Has<MoveSpeed>()) continue;
        
        // 检查 WithAll 特性
        if (!entity.Has<EnemyTag>()) continue;
        
        // 检查 WithNone 特性
        if (entity.Has<DeadTag>()) continue;
        
        entities.Add(entity);
    }
    return entities;
}
```

---

### 5️⃣ **生成上下文类（Context）**

- [ ] 为每个 Job 生成专属的上下文类
- [ ] 包含所有组件的 `NativeArray<T>` 字段
- [ ] 包含实体数量信息
- [ ] 实现数据收集逻辑（从 ECS World 复制到 NativeArray）
- [ ] 实现数据回写逻辑（从 NativeArray 写回到 ECS World，仅 `ref` 参数）
- [ ] 实现资源释放逻辑（Dispose NativeArray）

**生成类似：**
```csharp
internal class MoveForwardJobContext
{
    public NativeArray<PGDLocalTransform> transforms;  // ref 参数
    public NativeArray<MoveSpeed> speeds;              // in 参数（只读）
    public List<IEntity> entities;
    public int count;
    
    public void CollectData(IECSWorld world, Allocator allocator)
    {
        entities = QueryEntities(world);
        count = entities.Count;
        
        transforms = new NativeArray<PGDLocalTransform>(count, allocator);
        speeds = new NativeArray<MoveSpeed>(count, allocator);
        
        for (int i = 0; i < count; i++)
        {
            transforms[i] = entities[i].Get<PGDLocalTransform>();
            speeds[i] = entities[i].Get<MoveSpeed>();
        }
    }
    
    public void WriteBack(IECSWorld world)
    {
        // 只回写 ref 参数
        for (int i = 0; i < count; i++)
        {
            entities[i].Set(transforms[i]);
        }
    }
    
    public void Dispose()
    {
        if (transforms.IsCreated) transforms.Dispose();
        if (speeds.IsCreated) speeds.Dispose();
    }
}
```

---

### 6️⃣ **生成 IJobParallelFor 包装**

- [ ] 生成实现 `IJobParallelFor` 的包装 struct
- [ ] 包装应该持有原始 Job 和上下文的引用
- [ ] 在 `Execute(int index)` 中调用原始 Job 的 `Execute` 方法
- [ ] 从上下文的 NativeArray 中提取对应索引的数据传递给 Job

**生成类似：**
```csharp
[BurstCompile]
internal struct MoveForwardJobWrapper : IJobParallelFor
{
    public MoveForwardJob job;
    public NativeArray<PGDLocalTransform> transforms;
    public NativeArray<MoveSpeed> speeds;
    
    public void Execute(int index)
    {
        var transform = transforms[index];
        var speed = speeds[index];
        
        // 调用原始 Execute（注意 ref 参数）
        job.Execute(ref transform, in speed);
        
        // 写回 ref 参数
        transforms[index] = transform;
    }
}
```

---

### 7️⃣ **生成描述器类（Descriptor）**

- [ ] 实现 `IPGDParallelJobDescriptor<TJob>` 接口
- [ ] 实现 `CreateContext` 方法：创建上下文并收集数据
- [ ] 实现 `Schedule` 方法：创建包装 Job 并调度为 `IJobParallelFor`
- [ ] 实现 `OnJobCompleted` 方法：回写 `ref` 参数的数据
- [ ] 实现 `DisposeContext` 方法：释放 NativeArray

**生成类似：**
```csharp
internal class MoveForwardJobDescriptor : IPGDParallelJobDescriptor<MoveForwardJob>
{
    public object CreateContext(IECSWorld world, Allocator allocator, ref MoveForwardJob job)
    {
        var context = new MoveForwardJobContext();
        context.CollectData(world, allocator);
        return context;
    }
    
    public JobHandle Schedule(IECSWorld world, ref MoveForwardJob job, object contextObj, JobHandle dependsOn)
    {
        var context = (MoveForwardJobContext)contextObj;
        
        var wrapper = new MoveForwardJobWrapper
        {
            job = job,
            transforms = context.transforms,
            speeds = context.speeds
        };
        
        return wrapper.ScheduleParallel(context.count, 64, dependsOn);
    }
    
    public void OnJobCompleted(IECSWorld world, object contextObj)
    {
        var context = (MoveForwardJobContext)contextObj;
        context.WriteBack(world);
    }
    
    public void DisposeContext(object contextObj)
    {
        var context = (MoveForwardJobContext)contextObj;
        context.Dispose();
    }
}
```

---

### 8️⃣ **生成扩展方法**

- [ ] 为原始 Job struct 生成静态扩展类
- [ ] 生成无参的 `ScheduleParallel()` 扩展方法
- [ ] 生成带 `world` 参数的重载
- [ ] 生成带 `dependsOn` 参数的重载
- [ ] 内部调用 `PGDParallelJobScheduler.ScheduleParallel`

**生成类似：**
```csharp
public static class MoveForwardJobExtensions
{
    private static readonly MoveForwardJobDescriptor s_descriptor = new();
    
    public static JobHandle ScheduleParallel(
        this ref MoveForwardJob job, 
        JobHandle dependsOn = default)
    {
        return PGDParallelJobScheduler.ScheduleParallel(
            ref job, s_descriptor, dependsOn);
    }
    
    public static JobHandle ScheduleParallel(
        this ref MoveForwardJob job,
        IECSWorld world,
        JobHandle dependsOn = default)
    {
        return PGDParallelJobScheduler.ScheduleParallel(
            ref job, s_descriptor, world, dependsOn);
    }
}
```

---

### 9️⃣ **额外优化**

- [ ] 缓存描述器实例（避免每次调度都创建）
- [ ] 支持空 Job（没有匹配实体时不调度）
- [ ] 生成调试友好的代码（包含注释）
- [ ] 处理泛型 Job（如果需要）
- [ ] 支持嵌套类型
- [ ] 处理命名空间冲突

---

## 🎯 完整代码生成流程图

```
用户定义 Job
    ↓
[IJobParallel 检测]
    ↓
[分析 Execute 签名] ──→ 提取组件类型 + 访问模式
    ↓
[分析特性] ──→ 提取 WithAll/WithAny/WithNone
    ↓
[生成查询逻辑] ──→ QueryEntities()
    ↓
[生成上下文类] ──→ CollectData() + WriteBack() + Dispose()
    ↓
[生成包装 Job] ──→ IJobParallelFor 实现
    ↓
[生成描述器] ──→ IPGDParallelJobDescriptor 实现
    ↓
[生成扩展方法] ──→ ScheduleParallel()
    ↓
用户调用 job.ScheduleParallel() ✅
```

---

## 🔍 总结

Source Generator 本质上是**代码生成器**，它的任务是：

1. **读取** 用户的 Job 定义（类型、方法签名、特性）
2. **分析** 需要哪些组件、访问模式、过滤条件
3. **生成** 所有繁琐的胶水代码（查询、包装、调度、回写）
4. **暴露** 简洁的 API（`ScheduleParallel()`）

这样用户就只需要关注 `Execute` 方法的业务逻辑，其他都自动化了！

你现在需要实现这个 Source Generator 吗？我可以帮你一步步搭建。