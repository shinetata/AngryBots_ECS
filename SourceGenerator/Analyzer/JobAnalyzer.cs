using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PGD.Jobs.SourceGenerator.Models;
using System.Linq;
using System.Collections.Generic;

namespace PGD.Jobs.SourceGenerator.Analyzer
{
    /// <summary>
    /// Analyze job structs and collect metadata for code generation.
    /// </summary>
    internal static class JobAnalyzer
    {
        /// <summary>
        /// Analyze a job struct and extract all required information.
        /// </summary>
        public static JobInfo? AnalyzeJob(
            INamedTypeSymbol jobSymbol,
            StructDeclarationSyntax structSyntax,
            Compilation compilation)
        {
            // Find the Execute method
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

            // Analyze Execute parameters
            AnalyzeParameters(executeMethod, jobInfo);

            // Analyze Execute body to detect modified members
            AnalyzeExecuteMethodBody(executeMethod, structSyntax, jobInfo, compilation);

            // Analyze job attributes
            AnalyzeAttributes(jobSymbol, jobInfo);

            return jobInfo;
        }

        /// <summary>
        /// Find the instance Execute method declared on the job struct.
        /// </summary>
        private static IMethodSymbol? FindExecuteMethod(INamedTypeSymbol jobSymbol)
        {
            return jobSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.Name == "Execute" && !m.IsStatic);
        }

        /// <summary>
        /// Collect metadata for every parameter of the Execute method.
        /// </summary>
        private static void AnalyzeParameters(IMethodSymbol executeMethod, JobInfo jobInfo)
        {
            foreach (var parameter in executeMethod.Parameters)
            {
                var parameterType = parameter.Type;
                var typeFullName = parameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var parameterInfo = new ParameterInfo
                {
                    Symbol = parameter,
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
        /// Inspect the Execute method body and capture which ref parameters are mutated.
        /// </summary>
        private static void AnalyzeExecuteMethodBody(
            IMethodSymbol executeMethod,
            StructDeclarationSyntax structSyntax,
            JobInfo jobInfo,
            Compilation compilation)
        {
            // Locate the Execute method declaration
            var executeSyntax = structSyntax.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == "Execute");

            if (executeSyntax == null)
                return;

            var semanticModel = compilation.GetSemanticModel(executeSyntax.SyntaxTree);
            BlockSyntax? methodBody = executeSyntax.Body;

            if (methodBody == null && executeSyntax.ExpressionBody != null)
            {
                var expressionStatement = SyntaxFactory.ExpressionStatement(executeSyntax.ExpressionBody.Expression);
                methodBody = SyntaxFactory.Block(expressionStatement);
            }

            if (methodBody == null)
                return;

            // For each writable component parameter, record modified members
            foreach (var parameter in jobInfo.Parameters.Where(p => p.RequiresWriteBack))
            {
                var modifiedMembers = FindModifiedMembers(methodBody, parameter, semanticModel);
                parameter.ModifiedMembers.AddRange(modifiedMembers);

                if (parameter.ModifiedMembers.Count == 0)
                {
                    parameter.IsFullyModified = true;
                }
            }
        }

        /// <summary>
        /// Determine which component members are mutated inside Execute.
        /// </summary>
        private static HashSet<string> FindModifiedMembers(BlockSyntax methodBody, ParameterInfo parameterInfo, SemanticModel semanticModel)
        {
            var modifiedMembers = new HashSet<string>(StringComparer.Ordinal);
            var parameterSymbol = parameterInfo.Symbol;
            var parameterName = parameterInfo.Name;

            bool IsParameterExpression(ExpressionSyntax expression)
            {
                if (parameterSymbol != null)
                {
                    var symbol = semanticModel.GetSymbolInfo(expression).Symbol;
                    if (symbol != null && SymbolEqualityComparer.Default.Equals(symbol, parameterSymbol))
                        return true;
                }

                if (expression is IdentifierNameSyntax identifierName)
                    return string.Equals(identifierName.Identifier.Text, parameterName, StringComparison.Ordinal);

                return false;
            }

            string? ExtractMemberName(ExpressionSyntax expression)
            {
                switch (expression)
                {
                    case MemberAccessExpressionSyntax memberAccess:
                        if (IsParameterExpression(memberAccess.Expression))
                            return memberAccess.Name.Identifier.Text;

                        return ExtractMemberName(memberAccess.Expression);
                    case ElementAccessExpressionSyntax elementAccess:
                        return ExtractMemberName(elementAccess.Expression);
                    case ParenthesizedExpressionSyntax parenthesized:
                        return ExtractMemberName(parenthesized.Expression);
                    default:
                        return null;
                }
            }

            foreach (var assignment in methodBody.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                var memberName = ExtractMemberName(assignment.Left);
                if (!string.IsNullOrEmpty(memberName))
                    modifiedMembers.Add(memberName);
            }

            foreach (var unary in methodBody.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>())
            {
                if (unary.IsKind(SyntaxKind.PreIncrementExpression) || unary.IsKind(SyntaxKind.PreDecrementExpression))
                {
                    var memberName = ExtractMemberName(unary.Operand);
                    if (!string.IsNullOrEmpty(memberName))
                        modifiedMembers.Add(memberName);
                }
            }

            foreach (var unary in methodBody.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>())
            {
                if (unary.IsKind(SyntaxKind.PostIncrementExpression) || unary.IsKind(SyntaxKind.PostDecrementExpression))
                {
                    var memberName = ExtractMemberName(unary.Operand);
                    if (!string.IsNullOrEmpty(memberName))
                        modifiedMembers.Add(memberName);
                }
            }

            foreach (var invocation in methodBody.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var memberName = ExtractMemberName(memberAccess.Expression);
                    if (!string.IsNullOrEmpty(memberName))
                        modifiedMembers.Add(memberName);
                }

                if (invocation.ArgumentList != null)
                {
                    foreach (var argument in invocation.ArgumentList.Arguments)
                    {
                        if (argument.RefOrOutKeyword.Kind() == SyntaxKind.None)
                            continue;

                        var memberName = ExtractMemberName(argument.Expression);
                        if (!string.IsNullOrEmpty(memberName))
                            modifiedMembers.Add(memberName);
                    }
                }
            }

            return modifiedMembers;
        }

        /// <summary>
        /// Analyze job-level attributes and record component filtering metadata.
        /// </summary>
        private static void AnalyzeAttributes(INamedTypeSymbol jobSymbol, JobInfo jobInfo)
        {
            foreach (var attribute in jobSymbol.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass == null)
                    continue;

                var attributeName = attributeClass.Name;

                if (attributeName == "BurstCompileAttribute")
                {
                    jobInfo.HasBurstCompile = true;
                }
                else if (attributeName == "WithAllAttribute")
                {
                    ExtractTypesFromAttribute(attribute, jobInfo.WithAllTypes);
                }
                else if (attributeName == "WithAnyAttribute")
                {
                    ExtractTypesFromAttribute(attribute, jobInfo.WithAnyTypes);
                }
                else if (attributeName == "WithNoneAttribute")
                {
                    ExtractTypesFromAttribute(attribute, jobInfo.WithNoneTypes);
                }
            }
        }

        /// <summary>
        /// Extract type arguments from WithAll/WithAny/WithNone attributes.
        /// </summary>
        private static void ExtractTypesFromAttribute(AttributeData attribute, List<ITypeSymbol> targetList)
        {
            // The constructor packs type arguments into the first params Type[] argument.
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
