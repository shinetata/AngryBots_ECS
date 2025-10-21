using PGD.Jobs.SourceGenerator.Models;

namespace PGD.Jobs.SourceGenerator.CodeGen
{
    /// <summary>
    /// 生成扩展方法（覆盖运行时反射版本）
    /// </summary>
    internal static class ExtensionMethodGenerator
    {
        /// <summary>
        /// 生成优化的 ScheduleParallel 扩展方法
        /// 注意：此方法会覆盖运行时的反射版本
        /// </summary>
        public static void Generate(CodeBuilder builder, JobInfo jobInfo)
        {
            var jobName = jobInfo.JobName;
            var descriptorName = $"{jobName}_Descriptor";

            builder.AppendXmlComment($"Generated optimized extensions for {jobName}");
            builder.AppendComment("Note: This overrides the runtime reflection fallback in IJobParallelExtensions");
            builder.AppendLine($"file static class {jobName}_GeneratedExtensions");
            builder.OpenBrace();

            // 静态描述器实例
            builder.AppendLine($"private static readonly {descriptorName} s_descriptor = new {descriptorName}();");
            builder.AppendLine();

            // ScheduleParallel() 无参版本
            builder.AppendXmlComment("Schedule parallel job (optimized, generated version)");
            builder.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            builder.AppendLine($"public static global::Unity.Jobs.JobHandle ScheduleParallel(");
            builder.IncreaseIndent();
            builder.AppendLine($"this ref {jobName} job,");
            builder.AppendLine($"global::Unity.Jobs.JobHandle dependsOn = default)");
            builder.DecreaseIndent();
            builder.OpenBrace();
            builder.AppendLine($"return global::PGD.Jobs.PGDParallelJobScheduler.ScheduleParallel(");
            builder.IncreaseIndent();
            builder.AppendLine("ref job,");
            builder.AppendLine("s_descriptor,");
            builder.AppendLine("dependsOn);");
            builder.DecreaseIndent();
            builder.CloseBrace();
            builder.AppendLine();

            // ScheduleParallel(world) 版本
            builder.AppendXmlComment("Schedule parallel job to specific world (optimized, generated version)");
            builder.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            builder.AppendLine($"public static global::Unity.Jobs.JobHandle ScheduleParallel(");
            builder.IncreaseIndent();
            builder.AppendLine($"this ref {jobName} job,");
            builder.AppendLine($"global::PGD.IECSWorld world,");
            builder.AppendLine($"global::Unity.Jobs.JobHandle dependsOn = default)");
            builder.DecreaseIndent();
            builder.OpenBrace();
            builder.AppendLine($"return global::PGD.Jobs.PGDParallelJobScheduler.ScheduleParallel(");
            builder.IncreaseIndent();
            builder.AppendLine("ref job,");
            builder.AppendLine("s_descriptor,");
            builder.AppendLine("world,");
            builder.AppendLine("dependsOn);");
            builder.DecreaseIndent();
            builder.CloseBrace();

            builder.CloseBrace();
        }
    }
}

