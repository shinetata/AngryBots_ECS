using System;
using Unity.Jobs;

namespace PGD.Jobs
{
    /// <summary>
    /// 为 IJobParallel 提供运行时扩展方法（基础实现）
    /// Source Generator 会在编译期生成优化版本覆盖此实现
    /// </summary>
    public static class IJobParallelExtensions
    {
        /// <summary>
        /// 调度并行 Job（运行时回退实现）
        /// 注意：此方法使用反射，性能较低。编译期 Source Generator 会生成优化版本。
        /// </summary>
        public static JobHandle ScheduleParallel<TJob>(
            this ref TJob job,
            JobHandle dependsOn = default)
            where TJob : struct, IJobParallel
        {
            return ScheduleParallelInternal(ref job, null, dependsOn);
        }

        /// <summary>
        /// 调度并行 Job 到指定 World（运行时回退实现）
        /// </summary>
        public static JobHandle ScheduleParallel<TJob>(
            this ref TJob job,
            IECSWorld world,
            JobHandle dependsOn = default)
            where TJob : struct, IJobParallel
        {
            return ScheduleParallelInternal(ref job, world, dependsOn);
        }

        /// <summary>
        /// 内部实现：使用反射调度 Job
        /// 注意：这是运行时回退路径，仅在 Source Generator 未生成代码时使用
        /// </summary>
        private static JobHandle ScheduleParallelInternal<TJob>(
            ref TJob job,
            IECSWorld world,
            JobHandle dependsOn)
            where TJob : struct, IJobParallel
        {
            // 获取或创建运行时描述器
            var descriptor = PGDJobReflectionRegistry.GetOrCreateDescriptor<TJob>();
            
            // 使用调度器调度
            return PGDParallelJobScheduler.ScheduleParallel(
                ref job,
                descriptor,
                world,
                dependsOn);
        }
    }
}

