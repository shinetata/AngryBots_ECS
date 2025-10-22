using Microsoft.CodeAnalysis;
using PGD.Jobs.SourceGenerator.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PGD.Jobs.SourceGenerator.CodeGen
{
    /// <summary>
    /// Job 代码生成器 - 使用模板系统生成代码
    /// </summary>
    internal class JobCodeGenerator
    {
        private readonly JobInfo _jobInfo;
        private readonly List<ParameterInfo> _componentParameters;
        private readonly Dictionary<ParameterInfo, string> _fieldNames;
        private readonly bool _hasEntityParameter;
        private readonly bool _hasEntityIndexParameter;

        public JobCodeGenerator(JobInfo jobInfo)
        {
            _jobInfo = jobInfo;
            _componentParameters = jobInfo.Parameters.Where(p => p.IsComponent).ToList();
            _fieldNames = BuildNativeArrayFieldNames(_componentParameters);
            _hasEntityParameter = jobInfo.Parameters.Any(p => p.IsEntity);
            _hasEntityIndexParameter = jobInfo.Parameters.Any(p => p.IsEntityIndex);
        }

        /// <summary>
        /// 生成完整的 Job 代码
        /// </summary>
        public string Generate()
        {
            var builder = new TemplateDataBuilder()
                .WithJobName(_jobInfo.JobName)
                .WithNamespace(_jobInfo.Namespace ?? string.Empty)
                .AddUsings(
                    Constants.Namespaces.UnityJobs,
                    Constants.Namespaces.UnityCollections,
                    Constants.Namespaces.UnityEngine,
                    Constants.Namespaces.PGD,
                    Constants.Namespaces.PGDJobs,
                    Constants.Namespaces.SystemLinq
                )
                .WithWrapperFields(GenerateWrapperFields())
                .WithWrapperExecuteBody(GenerateWrapperExecuteBody())
                .WithExecuteGeneratedSignature(GenerateExecuteGeneratedSignature())
                .WithExecuteGeneratedCall(GenerateExecuteGeneratedCall())
                .WithExtensionStaticFields(GenerateExtensionStaticFields())
                .WithScheduleParallelBody(GenerateScheduleParallelBody())
                .WithScheduleParallelWithQueryBody(GenerateScheduleParallelWithQueryBody());

            var templateData = builder.Build();
            return TemplateEngine.ApplyTemplate(templateData);
        }

        /// <summary>
        /// 生成 Wrapper 字段定义
        /// </summary>
        private string GenerateWrapperFields()
        {
            var sb = new StringBuilder();
            var indent = TemplateEngine.Indent(1);

            // 添加组件 NativeArray 字段
            if (_componentParameters.Count > 0)
            {
                sb.AppendLine($"{indent}/// <summary>");
                sb.AppendLine($"{indent}/// {Constants.Comments.ComponentDataArrays}");
                sb.AppendLine($"{indent}/// </summary>");

                foreach (var parameter in _componentParameters)
                {
                    var fieldName = _fieldNames[parameter];
                    if (parameter.IsReadOnly)
                    {
                        sb.AppendLine($"{indent}{Constants.Attributes.ReadOnly}");
                    }
                    sb.AppendLine($"{indent}{Constants.Modifiers.Public} {Constants.TypeNames.NativeArray}<{parameter.TypeFullName}> {fieldName};");
                }
            }

            // 添加实体数组字段（如果需要）
            if (_hasEntityParameter)
            {
                if (sb.Length > 0)
                    sb.AppendLine();

                sb.AppendLine($"{indent}/// <summary>");
                sb.AppendLine($"{indent}/// {Constants.Comments.EntityReferencesArray}");
                sb.AppendLine($"{indent}/// </summary>");
                sb.AppendLine($"{indent}{Constants.Attributes.ReadOnly}");
                sb.AppendLine($"{indent}{Constants.Modifiers.Public} {Constants.TypeNames.NativeArray}<{Constants.TypeNames.IEntity}> {Constants.FieldNames.EntityArray};");
            }

            return sb.ToString().TrimEnd('\r', '\n');
        }

        /// <summary>
        /// 生成 Wrapper Execute 方法体
        /// </summary>
        private string GenerateWrapperExecuteBody()
        {
            var sb = new StringBuilder();
            var indent = TemplateEngine.Indent(2);

            // 从 NativeArray 中提取参数
            foreach (var parameter in _componentParameters)
            {
                var fieldName = _fieldNames[parameter];
                var localVarName = parameter.Name;
                sb.AppendLine($"{indent}var {localVarName} = {fieldName}[{Constants.FieldNames.Index}];");
            }

            if (_hasEntityParameter)
            {
                var entityParam = _jobInfo.Parameters.First(p => p.IsEntity);
                sb.AppendLine($"{indent}var {entityParam.Name} = {Constants.FieldNames.EntityArray}[{Constants.FieldNames.Index}];");
            }

            if (_componentParameters.Count > 0 || _hasEntityParameter)
                sb.AppendLine();

            // 调用原始 Execute 方法
            sb.AppendLine($"{indent}// {Constants.Comments.CallOriginalExecute}");
            var executeArgs = GenerateExecuteCallArguments();
            sb.AppendLine($"{indent}{Constants.FieldNames.InnerJob}.{Constants.MethodNames.ExecuteGenerated}({executeArgs});");
            sb.AppendLine();

            // 写回可写参数
            var writableParameters = _componentParameters.Where(p => p.RequiresWriteBack).ToList();
            if (writableParameters.Count > 0)
            {
                sb.AppendLine($"{indent}// {Constants.Comments.WriteBackModifiedComponents}");
                foreach (var parameter in writableParameters)
                {
                    var fieldName = _fieldNames[parameter];
                    sb.AppendLine($"{indent}{fieldName}[{Constants.FieldNames.Index}] = {parameter.Name};");
                }
            }

            return sb.ToString().TrimEnd('\r', '\n');
        }

        /// <summary>
        /// 生成 ExecuteGenerated 方法签名
        /// </summary>
        private string GenerateExecuteGeneratedSignature()
        {
            var parts = new List<string>();
            foreach (var parameter in _jobInfo.Parameters)
            {
                var refModifier = GetRefModifier(parameter.RefKind);
                parts.Add($"{refModifier}{parameter.TypeFullName} {parameter.Name}");
            }
            return string.Join(", ", parts);
        }

        /// <summary>
        /// 生成 ExecuteGenerated 调用参数
        /// </summary>
        private string GenerateExecuteGeneratedCall()
        {
            return GenerateExecuteCallArguments();
        }

        /// <summary>
        /// 生成 Execute 调用参数
        /// </summary>
        private string GenerateExecuteCallArguments()
        {
            var parts = new List<string>();
            foreach (var parameter in _jobInfo.Parameters)
            {
                var refModifier = GetRefModifier(parameter.RefKind);

                if (parameter.IsEntityIndex)
                {
                    // 对于 entityIndex 参数，直接传递 index
                    parts.Add($"{refModifier}{Constants.FieldNames.Index}");
                }
                else
                {
                    parts.Add($"{refModifier}{parameter.Name}");
                }
            }
            return string.Join(", ", parts);
        }

        /// <summary>
        /// 生成 Extension 静态字段
        /// </summary>
        private string GenerateExtensionStaticFields()
        {
            var sb = new StringBuilder();
            var indent = TemplateEngine.Indent(1);

            if (_componentParameters.Count > 0)
            {
                foreach (var parameter in _componentParameters)
                {
                    var fieldName = _fieldNames[parameter];
                    sb.AppendLine($"{indent}{Constants.Modifiers.Private} {Constants.Modifiers.Static} NativeArray<{parameter.TypeFullName}> {fieldName};");
                }
            }

            if (_hasEntityParameter)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.AppendLine($"{indent}{Constants.Modifiers.Private} {Constants.Modifiers.Static} NativeArray<{Constants.TypeNames.IEntity}> {Constants.FieldNames.EntityArray};");
            }

            return sb.ToString().TrimEnd('\r', '\n');
        }

        /// <summary>
        /// 生成 ScheduleParallel 方法体（无参数版本，自动创建查询）
        /// </summary>
        private string GenerateScheduleParallelBody()
        {
            var sb = new StringBuilder();
            var indent = TemplateEngine.Indent(2);

            // 获取 World
            sb.AppendLine($"{indent}var {Constants.FieldNames.World} = {Constants.TypeNames.PGDJobSystemBase}.{Constants.FieldNames.CurrentWorld};");
            sb.AppendLine($"{indent}if ({Constants.FieldNames.World} == null)");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    {Constants.TypeNames.Debug}.{Constants.MethodNames.LogError}(\"{string.Format(Constants.LogMessages.ScheduleParallelError, _jobInfo.JobName)}\");");
            sb.AppendLine($"{indent}    return;");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();

            // 创建查询
            sb.AppendLine($"{indent}var {Constants.FieldNames.Query} = {Constants.FieldNames.World}.{Constants.MethodNames.Query}();");

            // 添加必需的组件
            if (_componentParameters.Count > 0)
            {
                sb.AppendLine($"{indent}var {Constants.FieldNames.RequiredComponents} = new {Constants.TypeNames.IComponents}();");
                var distinctComponentTypes = _componentParameters
                    .Select(p => p.TypeFullName)
                    .Distinct()
                    .ToList();
                foreach (var componentType in distinctComponentTypes)
                {
                    sb.AppendLine($"{indent}{Constants.FieldNames.RequiredComponents}.{Constants.MethodNames.Add}<{componentType}>();");
                }
                sb.AppendLine($"{indent}{Constants.FieldNames.Query} = {Constants.FieldNames.Query}.{Constants.MethodNames.WithAllComponents}({Constants.FieldNames.RequiredComponents});");
                sb.AppendLine();
            }

            // Apply filters
            GenerateWithAllFilter(sb, indent);
            GenerateWithAnyFilter(sb, indent);
            GenerateWithNoneFilter(sb, indent);

            // 检查实体数量
            sb.AppendLine($"{indent}var {Constants.FieldNames.EntityCount} = {Constants.FieldNames.Query}.{Constants.FieldNames.EntityCount_Property};");
            sb.AppendLine($"{indent}if ({Constants.FieldNames.EntityCount} == 0)");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    return;");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();

            // 分配 NativeArrays
            GenerateNativeArrayAllocation(sb, indent);

            // 提取组件数据
            GenerateComponentExtraction(sb, indent);

            // 生成调度逻辑
            GenerateJobScheduling(sb, indent);

            return sb.ToString().TrimEnd('\r', '\n');
        }

        /// <summary>
        /// 生成 ScheduleParallel 方法体（带自定义查询版本）
        /// </summary>
        private string GenerateScheduleParallelWithQueryBody()
        {
            var sb = new StringBuilder();
            var indent = TemplateEngine.Indent(2);

            // 获取 World（用于注册回调）
            sb.AppendLine($"{indent}var {Constants.FieldNames.World} = {Constants.TypeNames.PGDJobSystemBase}.{Constants.FieldNames.CurrentWorld};");
            sb.AppendLine($"{indent}if ({Constants.FieldNames.World} == null)");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    {Constants.TypeNames.Debug}.{Constants.MethodNames.LogError}(\"{string.Format(Constants.LogMessages.ScheduleParallelError, _jobInfo.JobName)}\");");
            sb.AppendLine($"{indent}    return;");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();

            // 验证查询参数
            sb.AppendLine($"{indent}if ({Constants.FieldNames.Query} == null)");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    {Constants.TypeNames.Debug}.{Constants.MethodNames.LogError}(\"{_jobInfo.JobName}.ScheduleParallel: query parameter is null\");");
            sb.AppendLine($"{indent}    return;");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();

            // 检查实体数量
            sb.AppendLine($"{indent}var {Constants.FieldNames.EntityCount} = {Constants.FieldNames.Query}.{Constants.FieldNames.EntityCount_Property};");
            sb.AppendLine($"{indent}if ({Constants.FieldNames.EntityCount} == 0)");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    return;");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();

            // 分配 NativeArrays
            GenerateNativeArrayAllocation(sb, indent);

            // 提取组件数据
            GenerateComponentExtraction(sb, indent);

            // 生成调度逻辑
            GenerateJobScheduling(sb, indent);

            return sb.ToString().TrimEnd('\r', '\n');
        }

        /// <summary>
        /// 生成 WithAll 过滤
        /// </summary>
        private void GenerateWithAllFilter(StringBuilder sb, string indent)
        {
            if (_jobInfo.WithAllTypes.Count == 0)
                return;

            sb.AppendLine($"{indent}var {Constants.FieldNames.WithAllComponents} = new {Constants.TypeNames.IComponents}();");
            foreach (var typeSymbol in _jobInfo.WithAllTypes)
            {
                var typeFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                sb.AppendLine($"{indent}{Constants.FieldNames.WithAllComponents}.{Constants.MethodNames.Add}<{typeFullName}>();");
            }
            sb.AppendLine($"{indent}{Constants.FieldNames.Query} = {Constants.FieldNames.Query}.{Constants.MethodNames.WithAllComponents}({Constants.FieldNames.WithAllComponents});");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成 WithAny 过滤
        /// </summary>
        private void GenerateWithAnyFilter(StringBuilder sb, string indent)
        {
            if (_jobInfo.WithAnyTypes.Count == 0)
                return;

            sb.AppendLine($"{indent}var {Constants.FieldNames.WithAnyComponents} = new {Constants.TypeNames.IComponents}();");
            foreach (var typeSymbol in _jobInfo.WithAnyTypes)
            {
                var typeFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                sb.AppendLine($"{indent}{Constants.FieldNames.WithAnyComponents}.{Constants.MethodNames.Add}<{typeFullName}>();");
            }
            sb.AppendLine($"{indent}{Constants.FieldNames.Query} = {Constants.FieldNames.Query}.{Constants.MethodNames.WithAnyComponents}({Constants.FieldNames.WithAnyComponents});");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成 WithNone 过滤
        /// </summary>
        private void GenerateWithNoneFilter(StringBuilder sb, string indent)
        {
            if (_jobInfo.WithNoneTypes.Count == 0)
                return;

            sb.AppendLine($"{indent}var {Constants.FieldNames.WithNoneComponents} = new {Constants.TypeNames.IComponents}();");
            foreach (var typeSymbol in _jobInfo.WithNoneTypes)
            {
                var typeFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                sb.AppendLine($"{indent}{Constants.FieldNames.WithNoneComponents}.{Constants.MethodNames.Add}<{typeFullName}>();");
            }
            sb.AppendLine($"{indent}{Constants.FieldNames.Query} = {Constants.FieldNames.Query}.{Constants.MethodNames.WithoutAnyComponents}({Constants.FieldNames.WithNoneComponents});");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成 NativeArray 分配
        /// </summary>
        private void GenerateNativeArrayAllocation(StringBuilder sb, string indent)
        {
            foreach (var parameter in _componentParameters)
            {
                var fieldName = _fieldNames[parameter];
                sb.AppendLine($"{indent}if ({fieldName}.{Constants.FieldNames.IsCreated})");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    {fieldName}.{Constants.MethodNames.Dispose}();");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine($"{indent}{fieldName} = new {Constants.TypeNames.NativeArray}<{parameter.TypeFullName}>({Constants.FieldNames.EntityCount}, {Constants.TypeNames.Allocator}.{Constants.Misc.TempJobAllocator});");
            }

            if (_hasEntityParameter)
            {
                sb.AppendLine($"{indent}if ({Constants.FieldNames.EntityArray}.{Constants.FieldNames.IsCreated})");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    {Constants.FieldNames.EntityArray}.{Constants.MethodNames.Dispose}();");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine($"{indent}{Constants.FieldNames.EntityArray} = new {Constants.TypeNames.NativeArray}<{Constants.TypeNames.IEntity}>({Constants.FieldNames.EntityCount}, {Constants.TypeNames.Allocator}.{Constants.Misc.TempJobAllocator});");
            }

            if (_componentParameters.Count > 0 || _hasEntityParameter)
                sb.AppendLine();
        }

        /// <summary>
        /// 生成组件提取
        /// </summary>
        private void GenerateComponentExtraction(StringBuilder sb, string indent)
        {
            if (_componentParameters.Count == 0 && !_hasEntityParameter)
                return;

            sb.AppendLine($"{indent}// {Constants.Comments.ExtractComponentData}");
            sb.AppendLine($"{indent}int {Constants.FieldNames.Index} = 0;");
            sb.AppendLine($"{indent}foreach (var {Constants.FieldNames.Entity} in {Constants.FieldNames.Query}.{Constants.FieldNames.Entities})");
            sb.AppendLine($"{indent}{{");

            if (_hasEntityParameter)
            {
                sb.AppendLine($"{indent}    {Constants.FieldNames.EntityArray}[{Constants.FieldNames.Index}] = {Constants.FieldNames.Entity};");
            }

            foreach (var parameter in _componentParameters)
            {
                var fieldName = _fieldNames[parameter];
                sb.AppendLine($"{indent}    {fieldName}[{Constants.FieldNames.Index}] = {Constants.FieldNames.Entity}.{Constants.MethodNames.GetComponent}<{parameter.TypeFullName}>();");
            }

            sb.AppendLine($"{indent}    {Constants.FieldNames.Index}++;");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成 Job 调度逻辑
        /// </summary>
        private void GenerateJobScheduling(StringBuilder sb, string indent)
        {
            var writableParameters = _componentParameters.Where(p => p.RequiresWriteBack).ToList();

            // Create wrapper job
            sb.AppendLine($"{indent}// {Constants.Comments.CreateWrapperJob}");
            sb.AppendLine($"{indent}var {Constants.FieldNames.Wrapper} = new {_jobInfo.JobName}{Constants.CodeGen.WrapperSuffix}");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    {Constants.FieldNames.InnerJob} = job,");

            foreach (var parameter in _componentParameters)
            {
                var fieldName = _fieldNames[parameter];
                sb.AppendLine($"{indent}    {fieldName} = {fieldName},");
            }

            if (_hasEntityParameter)
            {
                sb.AppendLine($"{indent}    {Constants.FieldNames.EntityArray} = {Constants.FieldNames.EntityArray},");
            }

            sb.AppendLine($"{indent}}};");
            sb.AppendLine();

            // Schedule the job
            sb.AppendLine($"{indent}// {Constants.Comments.ScheduleTheJob}");
            sb.AppendLine($"{indent}var {Constants.FieldNames.Dependency} = {Constants.TypeNames.PGDJobSystemBase}.{Constants.FieldNames.CurrentDependency};");
            sb.AppendLine($"{indent}var {Constants.FieldNames.Handle} = {Constants.FieldNames.Wrapper}.{Constants.MethodNames.Schedule}({Constants.FieldNames.EntityCount}, {Constants.Misc.DefaultBatchSize}, {Constants.FieldNames.Dependency});");
            sb.AppendLine();

            // Register callbacks
            sb.AppendLine($"{indent}// {Constants.Comments.RegisterCallbacks}");
            sb.AppendLine($"{indent}{Constants.TypeNames.PGDJobSystemBase}.{Constants.MethodNames.RegisterJob}(");
            sb.AppendLine($"{indent}    {Constants.FieldNames.Handle},");

            // onComplete callback
            sb.AppendLine($"{indent}    onComplete: () =>");
            sb.AppendLine($"{indent}    {{");

            if (writableParameters.Count > 0)
            {
                GenerateWriteBackLogic(sb, indent + "        ", writableParameters);
            }
            else
            {
                sb.AppendLine($"{indent}        // {Constants.Comments.NoWritableComponents}");
            }

            sb.AppendLine($"{indent}    }},");

            // onDispose callback
            sb.AppendLine($"{indent}    onDispose: () =>");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        // {Constants.Comments.DisposeNativeArrays}");

            foreach (var parameter in _componentParameters)
            {
                var fieldName = _fieldNames[parameter];
                sb.AppendLine($"{indent}        if ({fieldName}.{Constants.FieldNames.IsCreated}) {fieldName}.{Constants.MethodNames.Dispose}();");
            }

            if (_hasEntityParameter)
            {
                sb.AppendLine($"{indent}        if ({Constants.FieldNames.EntityArray}.{Constants.FieldNames.IsCreated}) {Constants.FieldNames.EntityArray}.{Constants.MethodNames.Dispose}();");
            }

            sb.AppendLine($"{indent}    }});");
        }

        /// <summary>
        /// 生成写回逻辑
        /// </summary>
        private void GenerateWriteBackLogic(StringBuilder sb, string indent, List<ParameterInfo> writableParameters)
        {
            sb.AppendLine($"{indent}// {Constants.Comments.WriteBackToEntities}");
            sb.AppendLine($"{indent}int {Constants.FieldNames.Index} = 0;");
            sb.AppendLine($"{indent}foreach (var {Constants.FieldNames.Entity} in {Constants.FieldNames.Query}.{Constants.FieldNames.Entities})");
            sb.AppendLine($"{indent}{{");

            foreach (var parameter in writableParameters)
            {
                var fieldName = _fieldNames[parameter];

                // 如果检测到具体被修改的成员，只写回这些成员
                if (parameter.ModifiedMembers.Count > 0)
                {
                    sb.AppendLine($"{indent}    // {string.Format(Constants.Comments.WriteBackModifiedMembers, parameter.TypeName)}");
                    sb.AppendLine($"{indent}    {Constants.Modifiers.Ref} var {parameter.Name} = {Constants.Modifiers.Ref} {Constants.FieldNames.Entity}.{Constants.MethodNames.GetComponent}<{parameter.TypeFullName}>();");
                    foreach (var memberName in parameter.ModifiedMembers)
                    {
                        sb.AppendLine($"{indent}    {parameter.Name}.{memberName} = {fieldName}[{Constants.FieldNames.Index}].{memberName};");
                    }
                }
                // 否则写回整个组件（兜底方案）
                else if (parameter.IsFullyModified)
                {
                    sb.AppendLine($"{indent}    // {string.Format(Constants.Comments.WriteBackEntireComponent, parameter.TypeName)}");
                    sb.AppendLine($"{indent}    {Constants.Modifiers.Ref} var {parameter.Name} = {Constants.Modifiers.Ref} {Constants.FieldNames.Entity}.{Constants.MethodNames.GetComponent}<{parameter.TypeFullName}>();");
                    sb.AppendLine($"{indent}    {parameter.Name} = {fieldName}[{Constants.FieldNames.Index}];");
                }
            }

            sb.AppendLine($"{indent}    {Constants.FieldNames.Index}++;");
            sb.AppendLine($"{indent}}}");
        }

        /// <summary>
        /// 获取 ref 修饰符字符串
        /// </summary>
        private string GetRefModifier(RefKind refKind)
        {
            return refKind switch
            {
                RefKind.Ref => Constants.Modifiers.Ref + " ",
                RefKind.In => Constants.Modifiers.In + " ",
                RefKind.Out => Constants.Modifiers.Out + " ",
                _ => string.Empty
            };
        }

        /// <summary>
        /// 为每个参数生成唯一的 NativeArray 字段名
        /// </summary>
        private static Dictionary<ParameterInfo, string> BuildNativeArrayFieldNames(IReadOnlyList<ParameterInfo> parameters)
        {
            var result = new Dictionary<ParameterInfo, string>(parameters.Count);
            var usedNames = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                result[parameter] = GetNativeArrayFieldName(parameter, usedNames);
            }
            return result;
        }

        private static string GetNativeArrayFieldName(ParameterInfo parameter, HashSet<string> usedNames)
        {
            var baseName = GetSanitizedIdentifier(parameter.Name);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = parameter.TypeName;
            }

            var candidate = $"{Constants.CodeGen.ArrayPrefix}{baseName}{Constants.CodeGen.ArraySuffix}";
            var index = 1;
            while (!usedNames.Add(candidate))
            {
                candidate = $"{Constants.CodeGen.ArrayPrefix}{baseName}{index}{Constants.CodeGen.ArraySuffix}";
                index++;
            }

            return candidate;
        }

        private static string GetSanitizedIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return identifier;

            return identifier[0] == '@'
                ? identifier.Substring(1)
                : identifier;
        }
    }
}

