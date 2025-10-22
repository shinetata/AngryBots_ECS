using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using PGD.Jobs.SourceGenerator.Analyzer;
using PGD.Jobs.SourceGenerator.CodeGen;
using PGD.Jobs.SourceGenerator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PGD.Jobs.SourceGenerator
{
    /// <summary>
    /// 为实现 IJobParallel 接口的 Job 生成调度器代码
    /// </summary>
    [Generator]
    public class PGDJobSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // 注册语法接收器，用于收集候选的 Job 类型
            context.RegisterForSyntaxNotifications(() => new JobSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // 获取语法接收器
            if (context.SyntaxReceiver is not JobSyntaxReceiver receiver)
                return;

            // 遍历所有候选的 Job
            foreach (var candidateStruct in receiver.CandidateStructs)
            {
                var model = context.Compilation.GetSemanticModel(candidateStruct.SyntaxTree);
                var structSymbol = model.GetDeclaredSymbol(candidateStruct) as INamedTypeSymbol;

                if (structSymbol == null)
                    continue;

                // 验证是否实现了 IJobParallel 接口
                if (!ImplementsIJobParallel(structSymbol, context.Compilation))
                    continue;

                // 验证是否为 partial
                if (!candidateStruct.Modifiers.Any(m => m.ValueText == Constants.Modifiers.Partial))
                {
                    // 报告诊断错误：必须是 partial struct
                    var diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            Constants.DiagnosticIds.JobMustBePartial,
                            Constants.DiagnosticMessages.JobMustBePartialTitle,
                            Constants.DiagnosticMessages.JobMustBePartialMessage,
                            Constants.DiagnosticMessages.Category,
                            DiagnosticSeverity.Error,
                            true),
                        candidateStruct.GetLocation(),
                        structSymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                    continue;
                }

                // 分析 Job
                var jobInfo = JobAnalyzer.AnalyzeJob(structSymbol, candidateStruct, context.Compilation);
                if (jobInfo == null)
                {
                    // 报告诊断错误：缺少 Execute 方法
                    var diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            Constants.DiagnosticIds.JobMustHaveExecute,
                            Constants.DiagnosticMessages.JobMustHaveExecuteTitle,
                            Constants.DiagnosticMessages.JobMustHaveExecuteMessage,
                            Constants.DiagnosticMessages.Category,
                            DiagnosticSeverity.Error,
                            true),
                        candidateStruct.GetLocation(),
                        structSymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                    continue;
                }

                // 生成代码
                try
                {
                    var generatedCode = GenerateJobCode(jobInfo);
                    var fileName = $"{jobInfo.JobName}{Constants.CodeGen.GeneratedFileSuffix}";
                    context.AddSource(fileName, SourceText.From(generatedCode, Encoding.UTF8));
                }
                catch (System.Exception ex)
                {
                    // 报告代码生成错误
                    var diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            Constants.DiagnosticIds.CodeGenerationFailed,
                            Constants.DiagnosticMessages.CodeGenerationFailedTitle,
                            Constants.DiagnosticMessages.CodeGenerationFailedMessage,
                            Constants.DiagnosticMessages.Category,
                            DiagnosticSeverity.Error,
                            true),
                        candidateStruct.GetLocation(),
                        structSymbol.Name,
                        ex.Message);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private bool ImplementsIJobParallel(INamedTypeSymbol structSymbol, Compilation compilation)
        {
            var iJobParallelSymbol = compilation.GetTypeByMetadataName(Constants.TypeNames.IJobParallel);
            if (iJobParallelSymbol == null)
                return false;

            return structSymbol.AllInterfaces.Contains(iJobParallelSymbol, SymbolEqualityComparer.Default);
        }

        private string GenerateJobCode(JobInfo jobInfo)
        {
            // 使用新的模板系统生成代码
            var generator = new JobCodeGenerator(jobInfo);
            return generator.Generate();
        }
    }

    /// <summary>
    /// 语法接收器：收集所有 partial struct 候选
    /// </summary>
    internal class JobSyntaxReceiver : ISyntaxReceiver
    {
        public List<StructDeclarationSyntax> CandidateStructs { get; } = new List<StructDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // 查找 partial struct 声明
            if (syntaxNode is StructDeclarationSyntax structDeclaration &&
                structDeclaration.Modifiers.Any(m => m.ValueText == Constants.Modifiers.Partial))
            {
                CandidateStructs.Add(structDeclaration);
            }
        }
    }
}
