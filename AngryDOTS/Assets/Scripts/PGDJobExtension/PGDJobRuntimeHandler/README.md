# PGD Job Runtime Handler

**让 PGD 拥有像 Unity DOTS `IJobEntity` 一样简洁的 Job API！**

## 核心理念

### 开发者只需要做一件事：把 `IJobEntity` 改成 `IJobParallel`

```csharp
// DOTS 版本
public partial struct MyJob : IJobEntity
{
    void Execute(ref Component c) { /* ... */ }
}

// PGD 版本 - 只改这里！
public partial struct MyJob : IJobParallel
{
    void Execute(ref Component c) { /* ... */ }
}
```

**调度方式完全一样**：
```csharp
var job = new MyJob { /* ... */ };
job.ScheduleParallel();  // ← 一样的！
```

## 快速开始

### 1. 定义 System

```csharp
using PGD;
using PGD.Jobs;

partial class MoveForwardSystem : PGDJobSystemBase
{
    protected override void OnUpdate()
    {
        var job = new MoveForwardJob
        {
            dt = PGDGameContext.Time.DeltaTime
        };
        job.ScheduleParallel();  // 就这么简单！
    }
}
```

### 2. 定义 Job

```csharp
using PGD.Jobs;

[WithAll(typeof(MoveForward))]  // 可选：查询过滤
public partial struct MoveForwardJob : IJobParallel
//                                     ^^^^^^^^^^^^
//                                     改这里！
{
    public float dt;
    
    // Execute 参数决定查询哪些组件
    void Execute(in MoveSpeed speed, ref LocalTransform transform)
    {
        transform.Position += dt * speed.Value * math.forward(transform.Rotation);
    }
}
```

### 3. 完成！

框架自动处理：
- ✅ 查询实体
- ✅ 提取数据到 NativeArray
- ✅ 调度 Job
- ✅ 写回数据到实体
- ✅ 清理资源

## 与 DOTS 的对比

| 特性 | DOTS | PGD Jobs |
|------|------|----------|
| Job 接口 | `IJobEntity` | `IJobParallel` ← 唯一区别 |
| 调度方式 | `job.ScheduleParallel()` | `job.ScheduleParallel()` |
| Execute 签名 | `Execute(ref/in Component)` | `Execute(ref/in Component)` |
| 查询过滤 | `[WithAll]` 等 | `[WithAll]` 等 |
| 自动管理 | ✅ | ✅ |

## Execute 方法规则

### 组件参数

```csharp
void Execute(ref Component c)  // 可修改
void Execute(in Component c)   // 只读
```

- `ref` → 可修改，自动写回
- `in` → 只读，不写回

### 可选参数

```csharp
void Execute(ref Component c, IEntity entity)      // 访问实体
void Execute(ref Component c, int entityIndex)    // 访问索引
```

## 查询过滤

使用属性控制查询：

```csharp
[WithAll(typeof(Tag1), typeof(Tag2))]    // 必须有
[WithAny(typeof(Tag3), typeof(Tag4))]    // 至少一个
[WithNone(typeof(Tag5))]                  // 不能有
public partial struct MyJob : IJobParallel
{
    void Execute(ref Component c) { }
}
```

## 高级用法

### 带自定义查询

```csharp
protected override void OnUpdate()
{
    var customQuery = World.Query<Component1, Component2>();
    
    var job = new MyJob { /* ... */ };
    job.ScheduleParallel(customQuery);  // 使用自定义查询
}
```

## 工作原理

### 编写代码时
- `IJobParallelExtensions.cs` 提供占位方法
- IDE 识别方法，不报错
- 代码可以编译

### 编译时
- Source Generator 扫描 `partial struct : IJobParallel`
- 自动生成完整的扩展方法
- 生成代码包括：查询、提取、调度、写回、清理

### 运行时
- 使用生成的优化代码
- 性能与手写代码相当
- 完全自动化

## 核心文件

### IJobParallel.cs
Job 接口定义（marker interface）

### IJobParallelExtensions.cs
扩展方法占位实现（让 IDE 不报错）

### PGDJobSystemBase.cs
System 基类，提供：
- `CurrentWorld` - 当前 World
- `CurrentDependency` - 当前依赖链
- `RegisterJob()` - 注册 Job 回调
- 自动处理数据写回和清理

### PGDJobAttributes.cs
查询过滤属性：
- `[PGDJob]`
- `[WithAll]`
- `[WithAny]`
- `[WithNone]`

## 注意事项

1. **必须标记为 partial struct**
   ```csharp
   public partial struct MyJob : IJobParallel  // ← partial 必须有
   ```

2. **System 必须继承 PGDJobSystemBase**
   ```csharp
   partial class MySystem : PGDJobSystemBase  // ← 使用这个基类
   ```

3. **在 OnUpdate 中调度**
   ```csharp
   protected override void OnUpdate()
   {
       job.ScheduleParallel();  // ← 必须在 OnUpdate 中
   }
   ```

## 示例

完整示例请查看 `USAGE_EXAMPLE.md`

## 开发状态

### ✅ 已完成
- 基础架构
- 占位扩展方法
- System 基类
- Source Generator 框架

### ⏳ 进行中
- 完整代码生成逻辑
- 查询推导
- 数据自动提取和写回

详细计划请查看 `../../../SourceGenerator/IMPLEMENTATION_PLAN.md`

## 总结

**一句话总结**：把 `IJobEntity` 改成 `IJobParallel`，其他完全一样！
