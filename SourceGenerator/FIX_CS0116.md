# CS0116 错误修复

## 问题描述
编译时出现错误：
```
error CS0116: A namespace cannot directly contain members such as fields, methods or statements
```

## 根本原因
源生成器在 `PGDJobSourceGenerator.cs` 第 172 行使用了 C# 11 的 `file` 关键字：
```csharp
file struct {JobName}_Wrapper : global::Unity.Jobs.IJobParallelFor
```

`file` 关键字是 C# 11 新特性，用于限制类型作用域到当前文件。但 Unity 当前使用的 C# 版本不支持此特性，导致编译器将 `file` 视为变量名，使得 `struct` 声明被认为是在命名空间级别的直接成员，触发了 CS0116 错误。

## 修复方案
将 `file struct` 改为 `internal struct`：
```csharp
internal struct {JobName}_Wrapper : global::Unity.Jobs.IJobParallelFor
```

## 修改文件
- `SourceGenerator/PGDJobSourceGenerator.cs` (第 172 行)

## 部署步骤
1. ✅ 修改源代码
2. ✅ 重新编译源生成器 (`dotnet build -c Release`)
3. ✅ 复制 DLL 到 Unity 工程 (`AngryDOTS/Assets/Plugins/PGD/Analyzers/PGD.Jobs.SourceGenerator.dll`)

## 下一步
请在 Unity 编辑器中：
1. 重新打开 Unity 项目或刷新资源 (Ctrl+R)
2. 等待 Unity 重新编译
3. 检查控制台是否还有错误

## 注意事项
使用 `internal` 可见性对功能没有影响，因为生成的包装结构体只在内部使用，不需要暴露给外部。

