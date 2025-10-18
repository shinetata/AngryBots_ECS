using System;
using PGD.Jobs;
using Unity.Jobs;

namespace PGD.Jobs
{
    public static class UnityJobExtensions
    {
        public static JobHandle ScheduleParallel<JOB>(this JOB job, int iterations, JobHandle handles)
            where JOB : struct, IJobParallelFor
        {
            if (iterations <= 0)
            {
                return handles;
            }

            var innerloopBatchCount = ProcessorCount.BatchSize((uint)iterations);
            return job.Schedule((int)iterations, innerloopBatchCount, handles);
        }

        public static JobHandle ScheduleParallelAndCombine<JOB>(this JOB job, int iterations, JobHandle handles,
            JobHandle combinedHandles) where JOB : struct, IJobParallelFor
        {
            if (iterations == 0)
            {
                return combinedHandles;
            }
            
            var innerloopBatchCount = ProcessorCount.BatchSize((uint)iterations);
            var jobDeps = job.Schedule(iterations, innerloopBatchCount, handles);
            return JobHandle.CombineDependencies(combinedHandles, jobDeps);
        }

        public static JobHandle ScheduleAndCombine<JOB>(this JOB job, JobHandle handles, JobHandle combinedHandles)
            where JOB : struct, IJob
        {
            var jobDeps = job.Schedule(handles);
            return JobHandle.CombineDependencies(combinedHandles, jobDeps);
        }

        public static JobHandle ScheduleAndCombine<JOB>(this JOB job, int arrayLength, JobHandle handles,
            JobHandle combinedHandles) where JOB : struct, IJobFor
        {
            if (arrayLength == 0)
            {
                return combinedHandles;
            }
            
            var jobDeps = job.Schedule(arrayLength, handles);
            return JobHandle.CombineDependencies(combinedHandles, jobDeps);
        }
    }

    internal static class ProcessorCount
    {
        public static readonly int processorCount = Environment.ProcessorCount;

        public static int BatchSize(uint totalIterations)
        {
            var iterationPerBatch = totalIterations / processorCount;

            if (iterationPerBatch > 64)
            {
                return 64;
            }
            
            return (int)iterationPerBatch;
        }
    }
}