# PGD Jobs 实现计划

## ✅ 已完成：基础架构（Phase 0）

### 开发者体验目标
**开发者只需要把 `IJobEntity` 改成 `IJobParallel`，其他完全一样！**

### 当前状态

#### 1. 运行时框架

**`PGDJobSystemBase.cs`** - 提供静态上下文
- ✅ `CurrentSystem` - 当前执行的 System
- ✅ `CurrentWorld` - 当前的 World
- ✅ `CurrentDependency` - 当前的依赖句柄
- ✅ `RegisterJob(handle, onComplete, onDispose)` - 注册 Job 和回调
- ✅ 自动执行 SyncDataBack 和 Dispose 回调

**`IJobParallelExtensions.cs`** - 占位扩展方法
- ✅ `ScheduleParallel()` - 无参数版本（DOTS 风格）
- ✅ `ScheduleParallel(IQuery)` - 带自定义查询版本

**`IJobParallel.cs`** - Job 接口
- ✅ Marker interface

**`PGDJobAttributes.cs`** - 查询过滤属性
- ✅ `[WithAll]`, `[WithAny]`, `[WithNone]`

#### 2. Source Generator

**`PGDJobSourceGenerator.cs`** - 代码生成器
- ✅ 识别 `partial struct : IJobParallel`
- ✅ 验证必须是 `partial struct`
- ✅ 分析 `Execute` 方法（通过 `JobAnalyzer`）
- ✅ 生成扩展方法类（包含查询与数据提取逻辑）
- ✅ 编译成功（Roslyn 3.8.0）

**当前生成的代码**（Phase 2 输出）：
```csharp
internal static class MoveForwardJobExtensions
{
    private static NativeArray<MoveSpeed> s_speedArray;
    private static NativeArray<LocalTransform> s_transformArray;

    public static void ScheduleParallel(this ref MoveForwardJob job)
    {
        var world = PGDJobSystemBase.CurrentWorld;
        if (world == null)
        {
            Debug.LogError("[PGD.Jobs] MoveForwardJob.ScheduleParallel() can only be called inside PGDJobSystemBase.OnUpdate().");
            return;
        }

        var query = world.Query();
        var requiredComponents = new IComponents();
        requiredComponents.Add<MoveSpeed>();
        requiredComponents.Add<LocalTransform>();
        query = query.WithAllComponents(requiredComponents);

        var entities = query.Entities.ToEntitySet();
        var entityCount = entities.Count;
        if (entityCount == 0)
            return;

        if (s_speedArray.IsCreated) s_speedArray.Dispose();
        s_speedArray = new NativeArray<MoveSpeed>(entityCount, Allocator.TempJob);
        if (s_transformArray.IsCreated) s_transformArray.Dispose();
        s_transformArray = new NativeArray<LocalTransform>(entityCount, Allocator.TempJob);

        for (int i = 0; i < entityCount; i++)
        {
            var entity = entities[i];
            s_speedArray[i] = entity.GetComponent<MoveSpeed>();
            s_transformArray[i] = entity.GetComponent<LocalTransform>();
        }

        // TODO (Phase 3): 生成包装 Job、调度与写回。
    }
}
```

#### 3. 开发者使用方式（当前可用）

```csharp
// System
[BurstCompile]
partial class MoveForwardSystem : PGDJobSystemBase
{
    protected override void OnUpdate()
    {
        var job = new MoveForwardJob { dt = PGDGameContext.Time.DeltaTime };
        job.ScheduleParallel();  // ← IDE 不报错，可以编译！
    }
}

// Job
[BurstCompile]
[WithAll(typeof(MoveForward))]
public partial struct MoveForwardJob : IJobParallel  // ← 只改这里
{
    public float dt;
    void Execute(in MoveSpeed speed, ref LocalTransform transform)
    {
        transform.Position += dt * speed.Value * math.forward(transform.Rotation);
    }
}
```

**状态**：
- ✅ 代码可以编译
- ✅ IDE 不报错
- ✅ Source Generator 会自动生成查询与数据提取代码
- ⚠️ 尚未生成包装 Job、调度与写回逻辑（Phase 3 待完成）

---

## 📋 待实现：完整代码生成（Phase 1-3）

### Phase 1：分析 Execute 方法参数 ⏳

**目标**：从 `Execute` 方法推导需要的组件和访问模式

**需要实现**：
1. **参数分析**（`JobAnalyzer.cs` 需要增强）
   ```csharp
   void Execute(in MoveSpeed speed, ref LocalTransform transform)
   //           ^^             ^^^
   //           只读           可写
   ```
   
   提取信息：
   - `speed`: `MoveSpeed`, 只读（`in` 修饰符）
   - `transform`: `LocalTransform`, 可写（`ref` 修饰符）

2. **特殊参数处理**
   - `IEntity entity` - 实体引用
   - `int entityIndex` - 实体索引

3. **存储到 JobInfo**
   ```csharp
   class JobInfo
   {
       public List<ComponentParameter> Parameters { get; set; }
       // ...
   }
   
   class ComponentParameter
   {
       public string TypeName { get; set; }
       public bool IsReadOnly { get; set; }  // in 修饰符
       public bool IsWritable { get; set; }  // ref 修饰符
       public bool IsEntity { get; set; }
       public bool IsIndex { get; set; }
   }
   ```

**修改文件**：
- `Analyzer/JobAnalyzer.cs`
- `Models/JobInfo.cs`

---

### Phase 2：生成查询和数据提取代码 ✅

**成果摘要**：
1. Source Generator 会根据 `Execute` 里的组件参数，自动构建查询所需的 `IComponents` 并调用 `WithAllComponents`。
2. 为所有组件参数生成静态 `NativeArray<T>` 缓存字段，且支持名称去重。
3. 自动分配、填充 `EntitySet`，把组件数据复制进 `NativeArray`，为后续调度做好准备。
4. 在进入 Phase 3 前，若找到现有缓冲会先释放，避免内存泄漏。

**限制 / 后续工作**：
- 尚未接入包装 Job、调度、写回（由 Phase 3 负责）。
- 过滤属性（`WithAll`/`WithAny`/`WithNone`）仍待实现（Phase 4）。
- 当前查询使用 `Query()` + `WithAllComponents`，后续可按需优化为特化调用。

**涉及文件**：
- `PGDJobSourceGenerator.cs` - `GenerateExtensionMethods()` 及辅助方法。

---

### Phase 3：生成包装 Job 和调度逻辑 ⏳

**目标**：创建 `IJobParallelFor` 包装 Job 并调度

**需要生成的代码**：

#### 3.1 包装 Job 结构体

```csharp
[Unity.Burst.BurstCompile]
file struct MoveForwardJob_Wrapper : Unity.Jobs.IJobParallelFor
{
    // 原始 Job
    public MoveForwardJob innerJob;
    
    // NativeArray 字段
    [Unity.Collections.ReadOnly]
    public Unity.Collections.NativeArray<MoveSpeed> speedArray;
    public Unity.Collections.NativeArray<LocalTransform> transformArray;
    
    public void Execute(int index)
    {
        // 从 NativeArray 提取参数
        var speed = speedArray[index];
        var transform = transformArray[index];
        
        // 调用原始 Execute（通过生成的适配方法）
        innerJob.ExecuteGenerated(in speed, ref transform);
        
        // 写回可写参数
        transformArray[index] = transform;
    }
}

// 生成适配方法
partial struct MoveForwardJob
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void ExecuteGenerated(in MoveSpeed speed, ref LocalTransform transform)
    {
        Execute(in speed, ref transform);
    }
}
```

#### 3.2 调度和回调注册

```csharp
public static void ScheduleParallel(this ref MoveForwardJob job)
{
    // ... 前面的查询和数据提取代码 ...
    
    // 4. 创建包装 Job
    var wrapper = new MoveForwardJob_Wrapper
    {
        innerJob = job,
        speedArray = s_speedArray,
        transformArray = s_transformArray
    };
    
    // 5. 调度
    var dependency = global::PGD.Jobs.PGDJobSystemBase.CurrentDependency;
    var handle = wrapper.ScheduleParallel(
        entityCount, 
        64,  // batchSize
        dependency);
    
    // 6. 注册数据写回和清理回调
    global::PGD.Jobs.PGDJobSystemBase.RegisterJob(
        handle,
        onComplete: () =>
        {
            // 写回数据到实体
            for (int i = 0; i < entityCount; i++)
            {
                ref var transform = ref entities[i].GetComponent<LocalTransform>();
                transform = s_transformArray[i];
            }
        },
        onDispose: () =>
        {
            // 清理 NativeArray
            if (s_speedArray.IsCreated) s_speedArray.Dispose();
            if (s_transformArray.IsCreated) s_transformArray.Dispose();
        });
}
```

**修改文件**：
- `PGDJobSourceGenerator.cs` - 新增 `GenerateWrapperJob()`
- `PGDJobSourceGenerator.cs` - `GenerateExtensionMethods()` 中添加调度逻辑

---

### Phase 4：处理查询过滤属性 ⏳

**目标**：支持 `[WithAll]`, `[WithAny]`, `[WithNone]` 属性

**示例**：
```csharp
[WithAll(typeof(MoveForward), typeof(EnemyTag))]
[WithNone(typeof(DisabledTag))]
public partial struct MyJob : IJobParallel
{
    void Execute(ref Transform t) { }
}
```

**需要生成的查询代码**：
```csharp
var query = world.Query<Transform>();
// TODO: 应用 WithAll 过滤
// query = query.WithAll<MoveForward, EnemyTag>();
// query = query.WithNone<DisabledTag>();
```

**当前问题**：需要了解 PGD 的查询 API

**修改文件**：
- `Analyzer/JobAnalyzer.cs` - 提取属性信息
- `Models/JobInfo.cs` - 存储过滤信息
- `PGDJobSourceGenerator.cs` - 生成过滤代码

---

### Phase 5：优化和错误处理 ⏳

**需要处理的情况**：

1. **没有实体时**
   ```csharp
   if (entityCount == 0)
       return;  // 不调度 Job
   ```

2. **不在 PGDJobSystemBase 中调用**
   ```csharp
   if (world == null)
   {
       Debug.LogError("Cannot schedule job outside of PGDJobSystemBase.OnUpdate()");
       return;
   }
   ```

3. **组件不存在**
   ```csharp
   // 需要添加 null 检查？
   ```

4. **性能优化**
   - 缓存 Query 对象
   - 复用 NativeArray（当前已使用静态变量）
   - 批量大小调优

---

## 🎯 实施步骤

### 立即开始（验证当前架构）

1. **在 Unity 中测试**
   - 创建一个简单的 `IJobParallel` Job
   - 验证 Source Generator 生成代码
   - 检查生成的文件是否正确

2. **验证占位实现**
   - 运行时应该看到 Debug.Log
   - 确认没有编译错误

### Phase 1 实现（1-2 天）

1. 增强 `JobAnalyzer.AnalyzeJob()`
2. 完善 `JobInfo` 模型
3. 添加单元测试

### Phase 2 实现（2-3 天）

1. 实现查询生成逻辑
2. 实现数据提取代码生成
3. 测试不同组件类型

### Phase 3 实现（3-4 天）

1. 实现包装 Job 生成
2. 实现调度逻辑生成
3. 实现数据写回生成
4. 完整端到端测试

### Phase 4 实现（1-2 天）

1. 实现属性分析
2. 生成过滤代码
3. 测试各种过滤组合

### Phase 5 实现（2-3 天）

1. 添加错误处理
2. 性能优化
3. 完整测试套件

**总计**：约 2 周的开发时间

---

## 📝 技术细节

### Execute 方法签名规则

支持的参数类型：

1. **组件参数**
   ```csharp
   void Execute(ref Component c)     // 可写
   void Execute(in Component c)      // 只读
   void Execute(Component c)         // 值传递（不推荐）
   ```

2. **实体参数**（可选）
   ```csharp
   void Execute(ref Component c, IEntity entity)
   ```

3. **索引参数**（可选）
   ```csharp
   void Execute(ref Component c, int entityIndex)
   ```

### NativeArray 生成规则

- `ref` 参数 → 可写 NativeArray（需要写回）
- `in` 参数 → `[ReadOnly]` NativeArray（不写回）
- `IEntity` 参数 → 存储 entities 数组
- `int` 参数 → 直接传递 index

### 代码生成模板

```csharp
// 1. 静态变量（每个组件一个）
private static NativeArray<{ComponentType}> s_{componentName}Array;

// 2. 查询
var query = world.Query<{AllComponentTypes}>();

// 3. 提取
s_{componentName}Array = new NativeArray<{ComponentType}>(entityCount, Allocator.TempJob);
for (int i = 0; i < entityCount; i++)
    s_{componentName}Array[i] = entities[i].GetComponent<{ComponentType}>();

// 4. 包装 Job
struct {JobName}_Wrapper : IJobParallelFor
{
    public {JobName} innerJob;
    public NativeArray<{ComponentType}> {componentName}Array;
    
    public void Execute(int index)
    {
        var {componentName} = {componentName}Array[index];
        innerJob.ExecuteGenerated({params});
        {componentName}Array[index] = {componentName};  // 如果是 ref
    }
}

// 5. 调度
var handle = wrapper.ScheduleParallel(entityCount, 64, dependency);

// 6. 写回（onComplete）
for (int i = 0; i < entityCount; i++)
{
    ref var {componentName} = ref entities[i].GetComponent<{ComponentType}>();
    {componentName} = s_{componentName}Array[i];
}

// 7. 清理（onDispose）
if (s_{componentName}Array.IsCreated) s_{componentName}Array.Dispose();
```

---

## 🚀 当前可以做什么

### ✅ 开发者体验（已达成）

```csharp
// 只需要改 IJobEntity → IJobParallel
public partial struct MyJob : IJobParallel
{
    void Execute(ref Component c) { }
}

// 调度
var job = new MyJob { /* ... */ };
job.ScheduleParallel();  // IDE 不报错！
```

### ⏳ 运行时行为（待完善）

- 当前：输出 Debug.Log，但不执行 Job
- 目标：自动查询、提取、调度、写回、清理

---

## 📚 相关文件

### 运行时框架
- `PGDJobRuntimeHandler/IJobParallel.cs`
- `PGDJobRuntimeHandler/IJobParallelExtensions.cs`
- `PGDJobRuntimeHandler/PGDJobSystemBase.cs`
- `PGDJobRuntimeHandler/PGDJobAttributes.cs`

### Source Generator
- `PGDJobSourceGenerator.cs` - 主生成器
- `Analyzer/JobAnalyzer.cs` - Job 分析器
- `Models/JobInfo.cs` - Job 信息模型
- `CodeGen/CodeBuilder.cs` - 代码构建器

### 文档
- `README.md` - 概述
- `USAGE_EXAMPLE.md` - 使用示例（已更新为简洁版）
- `IMPLEMENTATION_PLAN.md` - 本文件
- `ARCHITECTURE.md` - 架构设计
- `CURRENT_STATUS.md` - 当前状态

---

## 总结

**当前状态**：
- ✅ 基础架构完成
- ✅ 开发者可以编写代码不报错
- ✅ Source Generator 可以生成 stub 代码
- ⏳ 需要实现完整的代码生成逻辑

**下一步**：
1. 在 Unity 中测试当前架构
2. 逐步实现 Phase 1-5
3. 完整端到端测试

**目标已达成**：开发者只需要把 `IJobEntity` 改成 `IJobParallel`！✅

### 后续待办
- ⛳ 支持 `partial class XXX : PGDSystem`（非 `PGDJobSystemBase`）调用 `ScheduleParallel()`，为未使用 `PGDJobSystemBase` 的系统提供运行时上下文桥接。
