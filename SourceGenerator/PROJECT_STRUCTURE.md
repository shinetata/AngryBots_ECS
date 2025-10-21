# PGD Jobs Source Generator - 项目结构

## 📁 目录结构

```
SourceGenerator/
├── PGD.Jobs.SourceGenerator.csproj    # 项目文件
├── PGDJobSourceGenerator.cs           # 主生成器入口
├── README.md                           # 需求文档
├── PROJECT_STRUCTURE.md               # 本文件
├── .gitignore                         # Git 忽略文件
│
├── Models/                            # 数据模型
│   └── JobInfo.cs                     # Job 信息数据类
│
├── Analyzer/                          # 分析器
│   └── JobAnalyzer.cs                 # Job 结构分析器
│
└── CodeGen/                           # 代码生成
    └── CodeBuilder.cs                 # 代码构建辅助类
```

## 🎯 主要组件

### 1. **PGDJobSourceGenerator** (主生成器)
- 实现 `ISourceGenerator` 接口
- 负责协调整个代码生成流程
- 注册语法接收器，收集候选 Job
- 调用分析器和代码生成器

### 2. **JobSyntaxReceiver** (语法接收器)
- 实现 `ISyntaxReceiver` 接口
- 遍历语法树，收集所有 `partial struct` 声明
- 过滤出实现 `IJobParallel` 接口的候选类型

### 3. **JobAnalyzer** (分析器)
- 分析 Job 的 `Execute` 方法签名
- 提取参数信息（类型、访问模式）
- 分析特性（`WithAll`、`WithAny`、`WithNone`、`BurstCompile`）
- 生成 `JobInfo` 数据结构

### 4. **JobInfo** (数据模型)
- 存储 Job 的所有分析信息
- 包含：Job 名称、命名空间、参数列表、过滤条件等
- `ParameterInfo` 存储单个参数的详细信息

### 5. **CodeBuilder** (代码构建器)
- 提供流畅的 API 来构建代码字符串
- 自动处理缩进
- 简化代码生成逻辑

## 🔄 工作流程

```
1. 编译开始
   ↓
2. JobSyntaxReceiver 收集候选 partial struct
   ↓
3. 对每个候选：
   a. 验证实现 IJobParallel 接口
   b. 验证是 partial struct
   c. 使用 JobAnalyzer 分析结构
   ↓
4. 生成代码：
   a. 生成上下文类（Context）
   b. 生成包装 Job（Wrapper）
   c. 生成描述器（Descriptor）
   d. 生成扩展方法（Extensions）
   ↓
5. 添加到编译输出
```

## 📦 依赖包

- **Microsoft.CodeAnalysis.CSharp** (4.3.0)
  - 提供 Roslyn 编译器 API
  - 用于分析和生成 C# 代码

- **Microsoft.CodeAnalysis.Analyzers** (3.3.3)
  - 分析器辅助库

## 🔨 如何使用

### 1. 在 Unity 项目中引用此 Source Generator

在 `Assembly-CSharp.csproj` 或其他需要使用 Job 的项目中添加：

```xml
<ItemGroup>
  <Analyzer Include="..\SourceGenerator\bin\Debug\netstandard2.0\PGD.Jobs.SourceGenerator.dll" />
</ItemGroup>
```

### 2. 定义 Job

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

### 3. 使用生成的扩展方法

```csharp
var job = new MoveForwardJob { dt = Time.deltaTime };
job.ScheduleParallel();
```

## 🚀 开发计划

### ✅ 已完成
- [x] 项目基础结构搭建
- [x] 主生成器框架
- [x] 语法接收器
- [x] Job 分析器
- [x] 数据模型定义
- [x] 代码构建辅助类
- [x] 错误诊断和报告

### 🔄 进行中
- [ ] 生成上下文类（Context）
- [ ] 生成包装 Job（Wrapper）
- [ ] 生成描述器（Descriptor）
- [ ] 完善扩展方法

### 📋 待开发
- [ ] 支持 `IEntity` 参数
- [ ] 优化查询性能
- [ ] 添加更多诊断信息
- [ ] 单元测试
- [ ] 集成测试

## 🐛 调试技巧

### 1. 查看生成的代码

在 Unity 项目编译后，生成的代码会在：
```
obj/Debug/generated/PGD.Jobs.SourceGenerator/
```

### 2. 启用详细日志

在 csproj 中添加：
```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

### 3. 使用 Visual Studio 调试器

- 在 Source Generator 项目属性中设置调试选项
- 附加到 `csc.exe` 或 `VBCSCompiler.exe` 进程

## 📝 注意事项

1. **netstandard2.0 限制**：Source Generator 必须针对 netstandard2.0
2. **不能引用 Unity 程序集**：生成器在编译期运行，不能直接访问 Unity 类型
3. **性能考虑**：避免在生成器中进行耗时操作
4. **错误处理**：使用 `Diagnostic` API 报告错误，不要抛出异常

## 🔗 相关文档

- [Roslyn Source Generators](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md)
- [Source Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md)

