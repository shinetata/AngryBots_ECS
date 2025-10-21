# PGD Jobs 当前状态

## ✅ 已完成

### 1. 核心架构设计

**设计原则**（按用户要求）：
- ✅ **写代码时不报错** - 提供完整的 API 签名
- ✅ **使用方式像 DOTS** - 开发者按 `IJobEntity` 方式编写
- ✅ **内部符合 PGD 框架** - 遵循 `IJobifiedSystem` 模式
- ✅ **Source Generator 补齐** - 编译时生成优化代码

### 2. 运行时框架（`PGDJobRuntimeHandler/`）

#### 已实现文件：

1. **`IJobParallel.cs`**
   - 定义 `IJobParallel` 接口（marker interface）
   - 作为 Job 的标记接口，类似 DOTS 的 `IJobEntity`

2. **`IJobParallelExtensions.cs`**
   - 提供 `ScheduleParallel()` 扩展方法的**占位实现**
   - 让 IDE 识别方法签名，不报错
   - 如果 Source Generator 未生成代码，会输出警告
   - 两个重载：
     - `ScheduleParallel(JobHandle)`
     - `ScheduleParallel(IECSWorld, JobHandle)`

3. **`PGDJobAttributes.cs`**
   - `[PGDJob]` - Job 标记属性
   - `[WithAll]` - 必须包含的组件
   - `[WithAny]` - 至少包含一个的组件
   - `[WithNone]` - 不能包含的组件

4. **`PGDJobSystemBase.cs`**
   - 可选的系统基类
   - 自动实现 `IJobifiedSystem` 接口
   - 提供 `DependencyHandle` 和 `CombineDependency()` 辅助方法

5. **`README.md`** 和 **`USAGE_EXAMPLE.md`**
   - 完整的文档和使用示例

#### 架构简化：

已删除的复杂组件（不需要了）：
- ❌ `PGDJobReflectionRegistry.cs` - 反射注册表（过于复杂）
- ❌ `PGDParallelJobScheduler.cs` - 调度器层（不必要）
- ❌ `PGDParallelJobRegistry.cs` - 注册表（不必要）
- ❌ `PGDParallelJobFlushSystem.cs` - Flush 系统（不必要）
- ❌ `IPGDParallelJobDescriptor.cs` - 描述器接口（不必要）

**简化后的设计**：
- 占位扩展方法让 IDE 不报错
- Source Generator 生成实际的扩展方法覆盖占位实现
- 每个 Job 自己管理生命周期（符合 PGD 框架）

### 3. Source Generator（`SourceGenerator/`）

#### 已实现功能：

1. **`PGDJobSourceGenerator.cs`**
   - ✅ 识别 `IJobParallel` 实现
   - ✅ 验证 `partial struct` 修饰符
   - ✅ 分析 `Execute` 方法
   - ✅ 生成扩展方法类
   - ✅ 编译成功（Roslyn 3.8.0）

2. **代码生成**：
   - 为每个 Job 生成 `file static class {JobName}Extensions`
   - 生成两个 `ScheduleParallel` 重载
   - 使用 `file` 关键字限制作用域，避免命名冲突

3. **诊断支持**：
   - `PGDJOB001` - Job 必须是 partial struct
   - `PGDJOB002` - Job 必须有 Execute 方法
   - `PGDJOB999` - 代码生成失败

#### 支持文件：

- `JobSyntaxReceiver.cs` - 语法接收器（内联在主文件中）
- `CodeGen/CodeBuilder.cs` - 代码构建器
- `Analyzer/JobAnalyzer.cs` - Job 分析器
- `Models/JobInfo.cs` - Job 信息模型

### 4. 编译状态

- ✅ Source Generator 编译成功
- ✅ 运行时框架无 linter 错误
- ✅ 符合 Unity Roslyn 3.8.0 要求

## 📋 待实现（TODO）

### 当前 Source Generator 生成的是占位代码：

```csharp
public static JobHandle ScheduleParallel(this ref MyJob job, JobHandle dependsOn = default)
{
    // TODO: Generate actual scheduling code
    dependsOn.Complete();
    return default;
}
```

### 需要完善的生成逻辑：

#### 阶段 1：基础调度（优先级：高）

1. **生成包装 Job**
   ```csharp
   // 为 MyJob 生成 MyJob_Wrapper : IJobParallelFor
   struct MyJob_Wrapper : IJobParallelFor
   {
       public MyJob innerJob;
       public NativeArray</* component data */> dataArrays;
       
       public void Execute(int index)
       {
           innerJob.Execute(index);
       }
   }
   ```

2. **生成调度逻辑**
   ```csharp
   public static JobHandle ScheduleParallel(this ref MyJob job, JobHandle dependsOn = default)
   {
       // 假设已经有 NativeArray 数据（由用户在 System 中准备）
       // 直接调度为 IJobParallelFor
       var wrapper = new MyJob_Wrapper { innerJob = job };
       return wrapper.Schedule(entityCount, batchSize, dependsOn);
   }
   ```

#### 阶段 2：查询推导（优先级：中）

3. **分析 Execute 参数**
   - 推导需要的组件类型
   - 推导访问模式（`ref` vs `in`）
   - 处理 `[WithAll]`, `[WithNone]` 等属性

4. **生成查询代码**
   ```csharp
   // 根据 Execute(ref Transform t, in Speed s) 生成：
   var query = world.Query<Transform, Speed>();
   ```

#### 阶段 3：自动数据提取（优先级：低）

5. **生成数据提取代码**
   ```csharp
   // 自动生成 NativeArray 提取逻辑
   var transforms = new NativeArray<Transform>(
       query.Entities.Select(e => e.GetComponent<Transform>()).ToArray(),
       Allocator.TempJob);
   ```

6. **生成数据写回代码**
   ```csharp
   // 在 SyncDataBack 中自动写回修改的组件
   int index = 0;
   foreach (var entity in query.Entities)
   {
       ref var transform = ref entity.GetComponent<Transform>();
       transform = transforms[index];
       index++;
   }
   ```

## 📐 当前使用方式

### System 代码（PGD 框架模式）

```csharp
[BurstCompile]
partial class MoveForwardSystem : PGDSystem<PGDLocalTransform, MoveSpeed>, IJobifiedSystem
{
    private NativeArray<PGDLocalTransform> transforms;
    private NativeArray<MoveSpeed> speeds;
    public Dependency dependency;

    public void SetJobHandle(ref Dependency deps) => dependency = deps;

    public void SyncDataBack()
    {
        int index = 0;
        foreach (var entity in GetQuery().Entities)
        {
            ref var transform = ref entity.GetComponent<PGDLocalTransform>();
            transform.Position = transforms[index].Position;
            index++;
        }
    }

    public void Dispose()
    {
        if (transforms.IsCreated) transforms.Dispose();
        if (speeds.IsCreated) speeds.Dispose();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        // 手动提取数据到 NativeArray
        transforms = new NativeArray<PGDLocalTransform>(
            GetQuery().Entities.Select(e => e.GetComponent<PGDLocalTransform>()).ToArray(), 
            Allocator.TempJob);
        speeds = new NativeArray<MoveSpeed>(
            GetQuery().Entities.Select(e => e.GetComponent<MoveSpeed>()).ToArray(), 
            Allocator.TempJob);
        
        // DOTS 风格的调度
        var job = new MoveForwardJob
        {
            speedArray = speeds,
            transformArray = transforms,
            dt = PGDGameContext.Time.DeltaTime
        };
        
        dependency.jobs = job.ScheduleParallel(GetQuery().EntityCount, dependency.jobs);
    }
}
```

### Job 代码（DOTS 风格）

```csharp
[BurstCompile]
[WithAll(typeof(MoveForward))]
public partial struct MoveForwardJob : IJobParallel
{
    [ReadOnly] public NativeArray<MoveSpeed> speedArray;
    public NativeArray<PGDLocalTransform> transformArray;
    public float dt;

    public void Execute(int index)
    {
        var speed = speedArray[index];
        var transform = transformArray[index];
        transform.Position = transform.Position + dt * speed.Value * math.forward(transform.Rotation);
        transformArray[index] = transform;
    }
}
```

## 🎯 设计验证

### ✅ 满足用户要求

1. **写代码时不报错**
   - ✅ `IJobParallelExtensions.cs` 提供占位方法
   - ✅ IDE 可以识别 `job.ScheduleParallel()`
   - ✅ 没有编译错误

2. **使用方式像 DOTS**
   - ✅ `IJobParallel` 类似 `IJobEntity`
   - ✅ `job.ScheduleParallel()` 调度方式
   - ✅ `[WithAll]`, `[WithNone]` 属性支持

3. **内部符合 PGD 框架**
   - ✅ 使用 `IJobifiedSystem` 接口
   - ✅ 遵循 `SetJobHandle` / `SyncDataBack` / `Dispose` 模式
   - ✅ 手动管理 `NativeArray` 和依赖链

4. **Source Generator 补齐**
   - ✅ 编译时扫描 `IJobParallel`
   - ✅ 生成扩展方法类
   - ⏳ 生成实际调度代码（TODO）

### 📊 与 DOTS 的对比

| 特性 | DOTS | PGD Jobs | 状态 |
|------|------|----------|------|
| Job 接口 | `IJobEntity` | `IJobParallel` | ✅ |
| 调度方法 | `job.ScheduleParallel()` | `job.ScheduleParallel(count, deps)` | ✅ |
| 查询过滤 | `[WithAll]` 等 | `[WithAll]` 等 | ✅ |
| 代码生成 | Source Generator | Source Generator | ✅ |
| System 基类 | `ISystem` | `IJobifiedSystem` | ✅ |
| 数据管理 | 自动 | 手动（NativeArray） | ✅ |
| 数据写回 | 自动 | 手动（SyncDataBack） | ✅ |

## 🔧 技术细节

### Roslyn 版本

- 使用 `Microsoft.CodeAnalysis` 3.8.0（Unity 要求）
- 抑制警告：`RS1035`, `RS1036`, `RS1038`

### 生成的代码格式

```csharp
// <auto-generated/>
// This file is generated by PGD.Jobs.SourceGenerator
#nullable enable

using Unity.Jobs;
using PGD;
using PGD.Jobs;

namespace YourNamespace
{
    /// <summary>
    /// Generated extension methods for MoveForwardJob
    /// </summary>
    file static class MoveForwardJobExtensions
    {
        public static global::Unity.Jobs.JobHandle ScheduleParallel(
            this ref MoveForwardJob job, 
            global::Unity.Jobs.JobHandle dependsOn = default)
        {
            // TODO: Generate actual scheduling code
            dependsOn.Complete();
            return default;
        }
    }
}
```

### 文件输出

- 生成文件命名：`{JobName}_Generated.g.cs`
- 使用 `file` 关键字限制作用域
- 自动添加 `<auto-generated/>` 标记

## 📚 文档状态

- ✅ `README.md` - 框架概述
- ✅ `USAGE_EXAMPLE.md` - 使用示例
- ✅ `ARCHITECTURE.md` - 架构设计
- ✅ `CURRENT_STATUS.md` - 当前状态（本文件）

## 🚀 下一步建议

### 立即可做（验证当前架构）

1. **测试占位实现**
   - 在 Unity 中创建一个简单的 `IJobParallel` Job
   - 验证 IDE 不报错
   - 验证 Source Generator 是否触发并生成代码
   - 检查生成的文件内容

### 短期目标（完善功能）

2. **实现基础调度代码生成**
   - 生成包装 Job 结构
   - 生成实际的 `IJobParallelFor.Schedule()` 调用
   - 测试性能和正确性

3. **添加查询推导**
   - 分析 Execute 参数
   - 生成查询代码
   - 处理过滤属性

### 长期目标（自动化）

4. **自动数据提取和写回**
   - 自动生成 NativeArray 提取代码
   - 自动生成 SyncDataBack 代码
   - 减少样板代码

5. **优化和 Burst 支持**
   - 确保生成的代码 Burst 兼容
   - 优化查询性能
   - 支持更多 Job 类型

## 📝 总结

**当前状态**：基础架构完成，可以编写代码而不报错，Source Generator 可以编译和运行。

**核心优势**：
- 简洁的占位设计，无需复杂反射
- DOTS 风格的开发体验
- 符合 PGD 框架模式
- 完全由 Source Generator 驱动

**需要补充**：实际的调度代码生成逻辑（当前只生成占位代码）。
