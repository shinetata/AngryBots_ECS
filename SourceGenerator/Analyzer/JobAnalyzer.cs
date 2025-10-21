using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PGD.Jobs.SourceGenerator.Models;
using System.Linq;

namespace PGD.Jobs.SourceGenerator.Analyzer
{
    /// <summary>
    /// 分析 Job 结构并提取信息
    /// </summary>
    internal static class JobAnalyzer
    {
        /// <summary>
        /// 分析 Job 结构体，提取所有需要的信息
        /// </summary>
        public static JobInfo? AnalyzeJob(
            INamedTypeSymbol jobSymbol,
            StructDeclarationSyntax structSyntax,
            Compilation compilation)
        {
            // 查找 Execute 方法
            var executeMethod = FindExecuteMethod(jobSymbol);
            if (executeMethod == null)
                return null;

            var jobInfo = new JobInfo
            {
                JobSymbol = jobSymbol,
                JobName = jobSymbol.Name,
                Namespace = jobSymbol.ContainingNamespace?.IsGlobalNamespace == false
                    ? jobSymbol.ContainingNamespace.ToDisplayString()
                    : null
            };

            // 分析 Execute 方法参数
            AnalyzeParameters(executeMethod, jobInfo);

            // 分析特性
            AnalyzeAttributes(jobSymbol, jobInfo);

            return jobInfo;
        }

        /// <summary>
        /// 查找 Execute 方法
        /// </summary>
        private static IMethodSymbol? FindExecuteMethod(INamedTypeSymbol jobSymbol)
        {
            return jobSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.Name == "Execute" && !m.IsStatic);
        }

        /// <summary>
        /// 分析 Execute 方法的参数
        /// </summary>
        private static void AnalyzeParameters(IMethodSymbol executeMethod, JobInfo jobInfo)
        {
            foreach (var parameter in executeMethod.Parameters)
            {
                var parameterType = parameter.Type;
                var typeFullName = parameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var parameterInfo = new ParameterInfo
                {
                    Name = parameter.Name,
                    Type = parameterType,
                    TypeName = parameterType.Name,
                    TypeFullName = typeFullName,
                    RefKind = parameter.RefKind
                };

                if (IsEntityParameter(parameterType, typeFullName))
                {
                    parameterInfo.IsEntity = true;
                }
                else if (IsEntityIndexParameter(parameter))
                {
                    parameterInfo.IsEntityIndex = true;
                }

                jobInfo.Parameters.Add(parameterInfo);
            }
        }

        private static bool IsEntityParameter(ITypeSymbol parameterType, string typeFullName)
        {
            if (parameterType.Name == "IEntity")
                return true;

            if (typeFullName == "global::PGD.IEntity")
                return true;

            return typeFullName.EndsWith(".IEntity", StringComparison.Ordinal);
        }

        private static bool IsEntityIndexParameter(IParameterSymbol parameter)
        {
            if (parameter.Type.SpecialType != SpecialType.System_Int32)
                return false;

            return string.Equals(parameter.Name, "entityIndex", StringComparison.Ordinal);
        }

        /// <summary>
        /// 分析 Job 上的特性
        /// </summary>
        private static void AnalyzeAttributes(INamedTypeSymbol jobSymbol, JobInfo jobInfo)
        {
            foreach (var attribute in jobSymbol.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass == null)
                    continue;

                var attributeName = attributeClass.Name;

                // 检查 BurstCompile
                if (attributeName == "BurstCompileAttribute")
                {
                    jobInfo.HasBurstCompile = true;
                }
                // 检查 WithAll
                else if (attributeName == "WithAllAttribute")
                {
                    ExtractTypesFromAttribute(attribute, jobInfo.WithAllTypes);
                }
                // 检查 WithAny
                else if (attributeName == "WithAnyAttribute")
                {
                    ExtractTypesFromAttribute(attribute, jobInfo.WithAnyTypes);
                }
                // 检查 WithNone
                else if (attributeName == "WithNoneAttribute")
                {
                    ExtractTypesFromAttribute(attribute, jobInfo.WithNoneTypes);
                }
            }
        }

        /// <summary>
        /// 从特性中提取类型参数
        /// </summary>
        private static void ExtractTypesFromAttribute(AttributeData attribute, System.Collections.Generic.List<ITypeSymbol> targetList)
        {
            // WithAll/WithAny/WithNone 特性的构造函数接受 params Type[]
            if (attribute.ConstructorArguments.Length > 0)
            {
                var typesArgument = attribute.ConstructorArguments[0];
                if (typesArgument.Kind == TypedConstantKind.Array)
                {
                    foreach (var typeConstant in typesArgument.Values)
                    {
                        if (typeConstant.Value is ITypeSymbol typeSymbol)
                        {
                            targetList.Add(typeSymbol);
                        }
                    }
                }
            }
        }
    }
}

