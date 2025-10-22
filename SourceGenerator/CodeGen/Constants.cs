namespace PGD.Jobs.SourceGenerator.CodeGen
{
    /// <summary>
    /// 统一管理所有代码生成中使用的常量字符串
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// 命名空间相关常量
        /// </summary>
        public static class Namespaces
        {
            public const string UnityJobs = "Unity.Jobs";
            public const string UnityCollections = "Unity.Collections";
            public const string UnityEngine = "UnityEngine";
            public const string PGD = "PGD";
            public const string PGDJobs = "PGD.Jobs";
            public const string SystemLinq = "System.Linq";
        }

        /// <summary>
        /// 类型名称常量
        /// </summary>
        public static class TypeNames
        {
            public const string IJobParallel = "PGD.Jobs.IJobParallel";
            public const string IJobParallelFor = "global::Unity.Jobs.IJobParallelFor";
            public const string BurstCompile = "global::Unity.Burst.BurstCompile";
            public const string ReadOnly = "global::Unity.Collections.ReadOnly";
            public const string NativeArray = "global::Unity.Collections.NativeArray";
            public const string IEntity = "global::PGD.IEntity";
            public const string IComponents = "global::PGD.IComponents";
            public const string IQuery = "global::PGD.IQuery";
            public const string Allocator = "global::Unity.Collections.Allocator";
            public const string PGDJobSystemBase = "global::PGD.Jobs.PGDJobSystemBase";
            public const string Debug = "global::UnityEngine.Debug";
        }

        /// <summary>
        /// 方法名称常量
        /// </summary>
        public static class MethodNames
        {
            public const string Execute = "Execute";
            public const string ExecuteGenerated = "ExecuteGenerated";
            public const string ScheduleParallel = "ScheduleParallel";
            public const string Schedule = "Schedule";
            public const string GetComponent = "GetComponent";
            public const string Query = "BuildHybridQuery";
            public const string WithAllComponents = "WithAllComponents";
            public const string WithAnyComponents = "WithAnyComponents";
            public const string WithoutAnyComponents = "WithoutAnyComponents";
            public const string Add = "Add";
            public const string Dispose = "Dispose";
            public const string RegisterJob = "RegisterJob";
            public const string UpdateDependency = "UpdateDependency";
            public const string RegisterCallbacks = "RegisterCallbacks";
            public const string LogError = "LogError";
            public const string Log = "Log";
        }

        /// <summary>
        /// 字段/属性名称常量
        /// </summary>
        public static class FieldNames
        {
            public const string InnerJob = "innerJob";
            public const string EntityArray = "s_entityArray";
            public const string EntityCount = "entityCount";
            public const string Index = "index";
            public const string Entity = "entity";
            public const string Query = "query";
            public const string World = "world";
            public const string Dependency = "dependency";
            public const string Handle = "handle";
            public const string Wrapper = "wrapper";
            public const string RequiredComponents = "requiredComponents";
            public const string WithAllComponents = "withAllComponents";
            public const string WithAnyComponents = "withAnyComponents";
            public const string WithNoneComponents = "withNoneComponents";
            public const string IsCreated = "IsCreated";
            public const string CurrentWorld = "CurrentWorld";
            public const string CurrentDependency = "CurrentDependency";
            public const string Entities = "Entities";
            public const string EntityCount_Property = "EntityCount";
        }

        /// <summary>
        /// 修饰符常量
        /// </summary>
        public static class Modifiers
        {
            public const string Partial = "partial";
            public const string Public = "public";
            public const string Private = "private";
            public const string Internal = "internal";
            public const string Static = "static";
            public const string Ref = "ref";
            public const string In = "in";
            public const string Out = "out";
        }

        /// <summary>
        /// 特性常量
        /// </summary>
        public static class Attributes
        {
            public const string BurstCompile = "[global::Unity.Burst.BurstCompile]";
            public const string ReadOnly = "[global::Unity.Collections.ReadOnly]";
            public const string MethodImplAggressiveInlining = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";
        }

        /// <summary>
        /// 代码生成相关常量
        /// </summary>
        public static class CodeGen
        {
            public const string AutoGeneratedComment = "// <auto-generated/>";
            public const string GeneratorComment = "// This file is generated by PGD.Jobs.SourceGenerator";
            public const string NullableEnable = "#nullable enable";
            public const string GeneratedFileSuffix = "_Generated.g.cs";
            public const string WrapperSuffix = "_Wrapper";
            public const string ExtensionsSuffix = "Extensions";
            public const string ArraySuffix = "Array";
            public const string ArrayPrefix = "s_";
        }

        /// <summary>
        /// 诊断错误代码
        /// </summary>
        public static class DiagnosticIds
        {
            public const string JobMustBePartial = "PGDJOB001";
            public const string JobMustHaveExecute = "PGDJOB002";
            public const string CodeGenerationFailed = "PGDJOB999";
        }

        /// <summary>
        /// 诊断错误消息
        /// </summary>
        public static class DiagnosticMessages
        {
            public const string JobMustBePartialTitle = "Job must be partial";
            public const string JobMustBePartialMessage = "Job '{0}' must be declared as 'partial struct' to enable code generation";
            
            public const string JobMustHaveExecuteTitle = "Job must have Execute method";
            public const string JobMustHaveExecuteMessage = "Job '{0}' must have an Execute method to enable code generation";
            
            public const string CodeGenerationFailedTitle = "Code generation failed";
            public const string CodeGenerationFailedMessage = "Failed to generate code for Job '{0}': {1}";
            
            public const string Category = "PGD.Jobs";
        }

        /// <summary>
        /// 日志消息常量
        /// </summary>
        public static class LogMessages
        {
            public const string ScheduleParallelError = "[PGD.Jobs] {0}.ScheduleParallel() can only be called inside PGDJobSystemBase.OnUpdate().";
            public const string ScheduleParallelStub = "[PGD.Jobs] {0}.ScheduleParallel(query) - Source Generator stub";
            public const string TODO_UseProvidedQuery = "TODO: Use provided query instead of auto-generating one";
        }

        /// <summary>
        /// 注释模板常量
        /// </summary>
        public static class Comments
        {
            public const string ComponentDataArrays = "Component data arrays";
            public const string EntityReferencesArray = "Entity references array";
            public const string ExecuteJobForSingleIndex = "Execute the job for a single entity index";
            public const string CallOriginalExecute = "Call the original Execute method";
            public const string WriteBackModifiedComponents = "Write back modified components";
            public const string WriteBackModifiedMembers = "Write back modified members of {0}";
            public const string WriteBackEntireComponent = "Write back entire {0} (full component modification detected)";
            public const string NoWritableComponents = "No writable components, nothing to write back";
            public const string DisposeNativeArrays = "Dispose NativeArrays";
            public const string ExtractComponentData = "Extract component data using foreach for better performance";
            public const string CreateWrapperJob = "Create wrapper job";
            public const string ScheduleTheJob = "Schedule the job";
            public const string RegisterCallbacks = "Register callbacks for data writeback and cleanup";
            public const string WriteBackToEntities = "Write back modified components to entities";
        }

        /// <summary>
        /// 模板占位符常量
        /// </summary>
        public static class Placeholders
        {
            public const string USINGS = "{{USINGS}}";
            public const string NAMESPACE = "{{NAMESPACE}}";
            public const string JOB_NAME = "{{JOB_NAME}}";
            public const string WRAPPER_NAME = "{{WRAPPER_NAME}}";
            public const string EXTENSIONS_NAME = "{{EXTENSIONS_NAME}}";
            public const string WRAPPER_FIELDS = "{{WRAPPER_FIELDS}}";
            public const string WRAPPER_EXECUTE_BODY = "{{WRAPPER_EXECUTE_BODY}}";
            public const string EXECUTE_GENERATED_SIGNATURE = "{{EXECUTE_GENERATED_SIGNATURE}}";
            public const string EXECUTE_GENERATED_CALL = "{{EXECUTE_GENERATED_CALL}}";
            public const string EXTENSION_STATIC_FIELDS = "{{EXTENSION_STATIC_FIELDS}}";
            public const string SCHEDULE_PARALLEL_BODY = "{{SCHEDULE_PARALLEL_BODY}}";
            public const string SCHEDULE_PARALLEL_WITH_QUERY_BODY = "{{SCHEDULE_PARALLEL_WITH_QUERY_BODY}}";
            public const string NAMESPACE_BEGIN = "{{NAMESPACE_BEGIN}}";
            public const string NAMESPACE_END = "{{NAMESPACE_END}}";
        }

        /// <summary>
        /// 其他常量
        /// </summary>
        public static class Misc
        {
            public const int DefaultBatchSize = 64;
            public const string TempJobAllocator = "TempJob";
        }
    }
}

