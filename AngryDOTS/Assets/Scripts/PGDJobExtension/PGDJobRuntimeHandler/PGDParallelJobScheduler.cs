using System;
using System.Collections.Generic;
using PGD;
using Unity.Collections;
using Unity.Jobs;

namespace PGD.Jobs
{
    /// <summary>
    /// 将 Source Generator 生成的描述器与运行时调度逻辑衔接，完成数据收集、调度和写回。
    /// </summary>
    public static class PGDParallelJobScheduler
    {
        /// <summary>
        /// 由生成代码调用，用于发起并行调度。
        /// </summary>
        public static JobHandle ScheduleParallel<TJob, TDescriptor>(
            ref TJob job,
            TDescriptor descriptor,
            JobHandle dependsOn = default)
            where TJob : struct, IJobParallel
            where TDescriptor : IPGDParallelJobDescriptor<TJob>
        {
            return ScheduleParallel(ref job, descriptor, null, dependsOn);
        }

        public static JobHandle ScheduleParallel<TJob, TDescriptor>(
            ref TJob job,
            TDescriptor descriptor,
            IECSWorld world,
            JobHandle dependsOn)
            where TJob : struct, IJobParallel
            where TDescriptor : IPGDParallelJobDescriptor<TJob>
        {
            if (descriptor is null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var actualWorld = world ?? PGDParallelWorldProvider.GetWorld();
            var context = descriptor.CreateContext(actualWorld, Allocator.TempJob, ref job);
            var handle = descriptor.Schedule(actualWorld, ref job, context, dependsOn);
            PGDParallelJobRegistry.Register<TJob, TDescriptor>(handle, actualWorld, descriptor, context);
            return handle;
        }
    }

    /// <summary>
    /// 描述器接口由 Source Generator 实现，以便调度器获取组件、标签以及写回信息。
    /// </summary>
    public interface IPGDParallelJobDescriptor<TJob>
        where TJob : struct, IJobParallel
    {
        object CreateContext(IECSWorld world, Allocator allocator, ref TJob job);

        JobHandle Schedule(IECSWorld world, ref TJob job, object context, JobHandle dependsOn);

        void OnJobCompleted(IECSWorld world, object context);

        void DisposeContext(object context);
    }

    internal static class PGDParallelJobRegistry
    {
        private interface IPendingJob
        {
            JobHandle Handle { get; }
            IECSWorld World { get; }
            void OnComplete();
            void Dispose();
        }

        private sealed class PendingJob<TJob, TDescriptor> : IPendingJob
            where TJob : struct, IJobParallel
            where TDescriptor : IPGDParallelJobDescriptor<TJob>
        {
            private readonly IECSWorld world;
            private readonly TDescriptor descriptor;
            private readonly object context;

            public PendingJob(JobHandle handle, IECSWorld world, TDescriptor descriptor, object context)
            {
                Handle = handle;
                this.world = world;
                this.descriptor = descriptor;
                this.context = context;
            }

            public JobHandle Handle { get; }

            public IECSWorld World => world;

            public void OnComplete()
            {
                descriptor.OnJobCompleted(world, context);
            }

            public void Dispose()
            {
                descriptor.DisposeContext(context);
            }
        }

        private static readonly List<IPendingJob> pendingJobs = new();
        private static readonly Dictionary<IECSWorld, PGDParallelJobFlushSystem> flushSystems = new();

        public static void Register<TJob, TDescriptor>(JobHandle handle, IECSWorld world, TDescriptor descriptor, object context)
            where TJob : struct, IJobParallel
            where TDescriptor : IPGDParallelJobDescriptor<TJob>
        {
            lock (pendingJobs)
            {
                pendingJobs.Add(new PendingJob<TJob, TDescriptor>(handle, world, descriptor, context));
            }

            var flushSystem = GetOrCreateFlushSystem(world);
            flushSystem.CombineHandle(handle);
        }

        public static void FlushCompleted(IECSWorld world, JobHandle dependency)
        {
            dependency.Complete();

            lock (pendingJobs)
            {
                for (int i = pendingJobs.Count - 1; i >= 0; i--)
                {
                    var pending = pendingJobs[i];
                    if (!ReferenceEquals(pending.World, world))
                    {
                        continue;
                    }

                    pending.Handle.Complete();
                    pending.OnComplete();
                    pending.Dispose();
                    pendingJobs.RemoveAt(i);
                }
            }
        }

        private static PGDParallelJobFlushSystem GetOrCreateFlushSystem(IECSWorld world)
        {
            lock (flushSystems)
            {
                if (!flushSystems.TryGetValue(world, out var system))
                {
                    system = new PGDParallelJobFlushSystem(world);
                    world.RegisterSystem(system);
                    PGDGameContext.GetJobManager().Register(system);
                    flushSystems[world] = system;
                }

                return system;
            }
        }
    }

    /// <summary>
    /// 提供 PGD 世界上下文。默认使用 PGDGameContext，可在特殊环境下替换。
    /// </summary>
    public static class PGDParallelWorldProvider
    {
        private static Func<IECSWorld> worldGetter = () => PGDGameContext.GetWorld();

        public static IECSWorld GetWorld() => worldGetter();

        public static void SetWorldProvider(Func<IECSWorld> getter)
        {
            worldGetter = getter ?? throw new ArgumentNullException(nameof(getter));
        }
    }
}
