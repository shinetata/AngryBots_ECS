using Unity.Jobs;
using PGD;
using UnityEngine;

namespace PGD.Jobs
{
    /// <summary>
    /// IJobParallel 的扩展方法（占位实现）
    /// 实际的扩展方法由 Source Generator 在编译时为每个 Job 类型生成
    /// </summary>
    public static class IJobParallelExtensions
    {
        /// <summary>
        /// 调度 IJobParallel Job 以进行并行执行（DOTS 风格 - 无参数）
        /// 注意：Source Generator 会为每个具体的 Job 类型生成优化的扩展方法覆盖此实现
        /// </summary>
        public static void ScheduleParallel<TJob>(this ref TJob job)
            where TJob : struct, IJobParallel
        {
            // 占位实现：如果看到这条警告，说明 Source Generator 没有生成代码
            Debug.LogWarning($"[PGD.Jobs] {typeof(TJob).Name}.ScheduleParallel() is using fallback implementation. " +
                           "Ensure Source Generator is properly configured and the Job is marked as 'partial struct'.");
        }

        /// <summary>
        /// 调度 IJobParallel Job 以进行并行执行（带自定义查询）
        /// </summary>
        public static void ScheduleParallel<TJob>(this ref TJob job, IQuery query)
            where TJob : struct, IJobParallel
        {
            Debug.LogWarning($"[PGD.Jobs] {typeof(TJob).Name}.ScheduleParallel(query) is using fallback implementation. " +
                           "Ensure Source Generator is properly configured and the Job is marked as 'partial struct'.");
        }
    }
}
