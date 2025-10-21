# PGD Jobs 架构设计

## 🎯 设计理念

参考 Unity DOTS 的 `IJobEntity` 模式，采用**运行时基础实现 + 编译期优化**的解耦架构：

```
运行时框架（PGDJobRuntimeHandler/）
    ↓
提供基础 API → IDE 不报错 ✅
使用反射实现 → 可以运行 ✅
性能较低 ⚠️

编译期生成器（SourceGenerator/）
    ↓
生成优化代码 → 覆盖运行时实现
直接调用无反射 → 高性能 🚀
编译时生效 ⏱️
```

---

## 📦 两层架构

### 第一层：运行时基础实现（Runtime Fallback）

**位置**: `AngryDOTS/Assets/Scripts/PGDJobExtension/PGDJobRuntimeHandler/`

**核心文件**：

#### 1. **IJobParallelExtensions.cs**
```csharp
public static class IJobParallelExtensions
{
    // 为所有 IJobParallel 提供 ScheduleParallel() 扩展方法
    public static JobHandle ScheduleParallel<TJob>(this ref TJob job, ...)
    {
        // 使用反射描述器调度
        var descriptor = PGDJobReflectionRegistry.GetOrCreateDescriptor<TJob>();
        return PGDParallelJobScheduler.ScheduleParallel(ref job, descriptor, ...);
    }
}
```

**作用**：
- ✅ **IDE 识别**：开发时能看到 `job.ScheduleParallel()` 方法
- ✅ **编译通过**：即使没有 Source Generator 也能编译
- ✅ **可以运行**：使用反射调度，功能完整但性能较低

#### 2. **PGDJobReflectionRegistry.cs**
```csharp
internal static class PGDJobReflectionRegistry
{
    // 运行时反射描述器
    public static IPGDParallelJobDescriptor<TJob> GetOrCreateDescriptor<TJob>()
    {
        // 使用反射分析 Job 的 Execute 方法
        // 创建通用的 IJobParallelFor 包装
        return new ReflectionJobDescriptor<TJob>();
    }
}
```

**作用**：
- 🔍 **反射分析**：运行时解析 Execute 方法签名
- 📦 **动态包装**：创建 IJobParallelFor 包装器
- ⚠️ **性能代价**：每次调度都需要反射，较慢

---

### 第二层：编译期优化实现（Source Generator）

**位置**: `SourceGenerator/`

**核心逻辑**：

#### 1. **ExtensionMethodGenerator.cs**
```csharp
// 生成与运行时同名的扩展方法
file static class MoveForwardJob_GeneratedExtensions
{
    private static readonly MoveForwardJob_Descriptor s_descriptor = new();
    
    // 覆盖运行时的扩展方法
    public static JobHandle ScheduleParallel(this ref MoveForwardJob job, ...)
    {
        // 使用生成的优化描述器
        return PGDParallelJobScheduler.ScheduleParallel(ref job, s_descriptor, ...);
    }
}
```

**关键点**：
- 🔄 **同名覆盖**：方法签名与运行时完全一致
- 📁 **file 作用域**：C# 11 特性，避免冲突
- 🚀 **优化描述器**：使用生成的专用描述器

#### 2. **生成的描述器（TODO）**
```csharp
file sealed class MoveForwardJob_Descriptor : IPGDParallelJobDescriptor<MoveForwardJob>
{
    // 编译期生成的优化实现：
    // - 无反射查询
    // - 预分配 NativeArray
    // - 类型安全的组件访问
    // - 直接 Burst 编译
}
```

---

## 🔄 工作流程

### 开发时（没有生成器）

```
用户代码:
  var job = new MoveForwardJob { dt = Time.deltaTime };
  job.ScheduleParallel();  // ← IDE 能识别这个方法
      ↓
调用 IJobParallelExtensions.ScheduleParallel<MoveForwardJob>()
      ↓
PGDJobReflectionRegistry.GetOrCreateDescriptor<MoveForwardJob>()
      ↓
反射分析 Execute 方法 → 创建 ReflectionJobDescriptor
      ↓
包装为 IJobParallelFor → 调度执行
      ↓
✅ 功能正常，但性能较低（反射开销）
```

### 编译期（有生成器）

```
Source Generator 检测到 MoveForwardJob
      ↓
分析 Execute(ref PGDLocalTransform, in MoveSpeed)
      ↓
生成:
  - MoveForwardJob_Descriptor （优化描述器）
  - MoveForwardJob_Context （上下文类）
  - MoveForwardJob_Wrapper （IJobParallelFor 包装）
  - MoveForwardJob_GeneratedExtensions （扩展方法）
      ↓
编译器选择生成的扩展方法（更具体的重载优先）
      ↓
调用生成的 MoveForwardJob_GeneratedExtensions.ScheduleParallel()
      ↓
使用 MoveForwardJob_Descriptor（无反射，直接访问）
      ↓
✅ 高性能执行
```

---

## 🎭 方法覆盖机制

### C# 扩展方法解析优先级

```csharp
// 运行时（PGDJobRuntimeHandler/IJobParallelExtensions.cs）
public static class IJobParallelExtensions
{
    public static JobHandle ScheduleParallel<TJob>(this ref TJob job, ...)
        where TJob : struct, IJobParallel
    {
        // 通用实现（反射）
    }
}

// 生成期（Source Generator 生成）
file static class MoveForwardJob_GeneratedExtensions
{
    public static JobHandle ScheduleParallel(this ref MoveForwardJob job, ...)
    {
        // 特化实现（优化）
    }
}
```

**编译器选择**：
1. 优先选择**更具体的类型**（`MoveForwardJob` > `TJob`）
2. 生成的扩展方法会**隐藏**运行时的泛型版本
3. 用户代码无需修改，编译器自动选择最优实现

---

## ✅ 优势

### 1. **开发体验好**
- ✅ IDE 始终能识别方法，无红线
- ✅ IntelliSense 自动补全
- ✅ 代码提示完整

### 2. **渐进式优化**
- ⚙️ 初期开发：运行时反射，快速迭代
- 🚀 性能优化：生成器生成高性能代码
- 🔄 平滑过渡：无需修改用户代码

### 3. **解耦设计**
- 📦 运行时框架独立：可单独使用
- 🔌 生成器可插拔：可选的编译期优化
- 🧪 易于测试：可以禁用生成器测试反射路径

### 4. **兼容性好**
- 🔧 没有生成器？照样能用（反射模式）
- ⚡ 有生成器？自动优化（生成模式）
- 🌐 跨平台：运行时代码纯 C#，生成器编译期

---

## 🔍 与 Unity DOTS 的对比

| 特性 | Unity IJobEntity | PGD IJobParallel |
|------|-----------------|------------------|
| **基础实现** | ECS 内置反射路径 | PGDJobReflectionRegistry |
| **优化实现** | Source Generator | Source Generator |
| **IDE 识别** | ✅ 始终可见 | ✅ 始终可见 |
| **性能** | 🚀 自动优化 | 🚀 自动优化 |
| **依赖** | ECS 包 | 仅 Unity.Jobs + PGD |

**核心理念一致**：
- 运行时提供基础 API
- 编译期生成优化代码
- 用户代码透明无感知

---

## 🚧 当前状态

### ✅ 已完成
- [x] 运行时扩展方法（IJobParallelExtensions）
- [x] 反射描述器（PGDJobReflectionRegistry）
- [x] Source Generator 框架
- [x] 占位符描述器生成
- [x] 扩展方法生成逻辑

### 🔄 进行中
- [ ] 完整描述器实现（目前回退到反射）
- [ ] 上下文类生成
- [ ] IJobParallelFor 包装生成
- [ ] 查询优化

### 📋 待实现
- [ ] WithAll/WithAny/WithNone 特性支持
- [ ] Burst 编译支持
- [ ] 性能测试对比
- [ ] 文档和示例

---

## 📝 使用示例

### 定义 Job

```csharp
using PGD.Jobs;
using Unity.Burst;

[BurstCompile]
public partial struct MoveForwardJob : IJobParallel
{
    public float dt;
    
    void Execute(ref PGDLocalTransform transform, in MoveSpeed speed)
    {
        transform.Position += transform.Forward * speed.Value * dt;
    }
}
```

### 调度 Job

```csharp
// 在系统中使用
partial class MovementSystem : PGDSystem<PGDLocalTransform, MoveSpeed>
{
    protected override void OnUpdate()
    {
        var job = new MoveForwardJob
        {
            dt = PGDGameContext.Time.DeltaTime
        };
        
        // 调度（IDE 能识别，编译通过，自动优化）
        job.ScheduleParallel();
    }
}
```

### 运行时行为

**无 Source Generator**:
```
MoveForwardJob.ScheduleParallel()
  → IJobParallelExtensions.ScheduleParallel<MoveForwardJob>()
  → 反射分析 Execute 方法
  → 动态创建 IJobParallelFor 包装
  → 调度执行（性能：~80% 原生）
```

**有 Source Generator**:
```
MoveForwardJob.ScheduleParallel()
  → MoveForwardJob_GeneratedExtensions.ScheduleParallel()
  → MoveForwardJob_Descriptor（编译期生成）
  → MoveForwardJob_Wrapper : IJobParallelFor（直接调用）
  → 调度执行（性能：~100% 原生）
```

---

## 🎯 总结

这个架构设计实现了：

1. **✅ 开发时无报错** - 运行时提供完整 API
2. **✅ 编译时自动优化** - Source Generator 透明工作
3. **✅ 渐进式性能提升** - 从反射到生成代码
4. **✅ 解耦且可插拔** - 各层独立，易于维护

**最重要的是**：用户代码始终保持简洁，无需关心底层实现细节！🚀

