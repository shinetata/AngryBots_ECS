using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using PGD;

namespace PGD.Jobs
{
    // Lightweight, job-safe entity reference for identifying entities inside jobs
    public readonly struct EntityRef
    {
        public readonly int Id;
        public EntityRef(int id) => Id = id;
    }

    // IJobEntity-like interfaces. Keep signatures simple and ergonomic.
    public interface IJobQuery<T1>
        where T1 : struct, IComponent
    {
        void Execute(ref T1 c1, EntityRef entity);
    }

    public interface IJobQuery<T1, T2>
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        void Execute(ref T1 c1, ref T2 c2, EntityRef entity);
    }

    public interface IJobQuery<T1, T2, T3>
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        void Execute(ref T1 c1, ref T2 c2, ref T3 c3, EntityRef entity);
    }

    // Public extensions — user entrypoints to schedule jobs in an IJobEntity-like way.
    public static class JobQueryExtensions
    {
        public static void Schedule<T1, TJob>(this IQuery<T1> query, TJob job)
            where T1 : struct, IComponent
            where TJob : struct, IJobQuery<T1>
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var handle = new ScheduledQuery1<T1, TJob>(query, job);
            PGDGameContext.GetJobManager().Register(handle);
        }

        public static void Schedule<T1, T2, TJob>(this IQuery<T1, T2> query, TJob job)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where TJob : struct, IJobQuery<T1, T2>
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var handle = new ScheduledQuery2<T1, T2, TJob>(query, job);
            PGDGameContext.GetJobManager().Register(handle);
        }

        public static void Schedule<T1, T2, T3, TJob>(this IQuery<T1, T2, T3> query, TJob job)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
            where TJob : struct, IJobQuery<T1, T2, T3>
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var handle = new ScheduledQuery3<T1, T2, T3, TJob>(query, job);
            PGDGameContext.GetJobManager().Register(handle);
        }
    }

    #region Internal scheduling adapters
    // These bridge the user job to Unity's job system, and own NativeArrays + sync.

    internal sealed class ScheduledQuery1<T1, TJob> : IJobifiedSystem
        where T1 : struct, IComponent
        where TJob : struct, IJobQuery<T1>
    {
        private readonly IQuery<T1> query;
        private readonly TJob job;
        private NativeArray<T1> c1;
        private NativeArray<int> entityIds;
        private int length;

        public ScheduledQuery1(IQuery<T1> query, TJob job)
        {
            this.query = query;
            this.job = job;
            length = query.EntityCount;

            c1 = new NativeArray<T1>(length, Allocator.TempJob);
            entityIds = new NativeArray<int>(length, Allocator.TempJob);

            int i = 0;
            foreach (var e in query.Entities)
            {
                c1[i] = e.GetComponent<T1>();
                entityIds[i] = e.Id;
                i++;
            }
        }

        public void SetJobHandle(ref Dependency deps)
        {
            if (length == 0) return;
            var wrapper = new QueryJob1<T1, TJob>
            {
                c1 = c1,
                entityIds = entityIds,
                job = job
            };
            // Schedule and chain into the shared dependency
            deps.jobs = wrapper.ScheduleAndCombine(length, deps.jobs, deps.jobs);
        }

        public void SyncDataBack()
        {
            int i = 0;
            foreach (var e in query.Entities)
            {
                var v1 = c1[i];
                e.Set(v1);
                i++;
            }
        }

        public void Dispose()
        {
            if (c1.IsCreated) c1.Dispose();
            if (entityIds.IsCreated) entityIds.Dispose();
        }
    }

    internal sealed class ScheduledQuery2<T1, T2, TJob> : IJobifiedSystem
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where TJob : struct, IJobQuery<T1, T2>
    {
        private readonly IQuery<T1, T2> query;
        private readonly TJob job;
        private NativeArray<T1> c1;
        private NativeArray<T2> c2;
        private NativeArray<int> entityIds;
        private int length;

        public ScheduledQuery2(IQuery<T1, T2> query, TJob job)
        {
            this.query = query;
            this.job = job;
            length = query.EntityCount;

            c1 = new NativeArray<T1>(length, Allocator.TempJob);
            c2 = new NativeArray<T2>(length, Allocator.TempJob);
            entityIds = new NativeArray<int>(length, Allocator.TempJob);

            int i = 0;
            foreach (var e in query.Entities)
            {
                c1[i] = e.GetComponent<T1>();
                c2[i] = e.GetComponent<T2>();
                entityIds[i] = e.Id;
                i++;
            }
        }

        public void SetJobHandle(ref Dependency deps)
        {
            if (length == 0) return;
            var wrapper = new QueryJob2<T1, T2, TJob>
            {
                c1 = c1,
                c2 = c2,
                entityIds = entityIds,
                job = job
            };
            deps.jobs = wrapper.ScheduleAndCombine(length, deps.jobs, deps.jobs);
        }

        public void SyncDataBack()
        {
            int i = 0;
            foreach (var e in query.Entities)
            {
                e.Set(c1[i]);
                e.Set(c2[i]);
                i++;
            }
        }

        public void Dispose()
        {
            if (c1.IsCreated) c1.Dispose();
            if (c2.IsCreated) c2.Dispose();
            if (entityIds.IsCreated) entityIds.Dispose();
        }
    }

    internal sealed class ScheduledQuery3<T1, T2, T3, TJob> : IJobifiedSystem
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
        where TJob : struct, IJobQuery<T1, T2, T3>
    {
        private readonly IQuery<T1, T2, T3> query;
        private readonly TJob job;
        private NativeArray<T1> c1;
        private NativeArray<T2> c2;
        private NativeArray<T3> c3;
        private NativeArray<int> entityIds;
        private int length;

        public ScheduledQuery3(IQuery<T1, T2, T3> query, TJob job)
        {
            this.query = query;
            this.job = job;
            length = query.EntityCount;

            c1 = new NativeArray<T1>(length, Allocator.TempJob);
            c2 = new NativeArray<T2>(length, Allocator.TempJob);
            c3 = new NativeArray<T3>(length, Allocator.TempJob);
            entityIds = new NativeArray<int>(length, Allocator.TempJob);

            int i = 0;
            foreach (var e in query.Entities)
            {
                c1[i] = e.GetComponent<T1>();
                c2[i] = e.GetComponent<T2>();
                c3[i] = e.GetComponent<T3>();
                entityIds[i] = e.Id;
                i++;
            }
        }

        public void SetJobHandle(ref Dependency deps)
        {
            if (length == 0) return;
            var wrapper = new QueryJob3<T1, T2, T3, TJob>
            {
                c1 = c1,
                c2 = c2,
                c3 = c3,
                entityIds = entityIds,
                job = job
            };
            deps.jobs = wrapper.ScheduleAndCombine(length, deps.jobs, deps.jobs);
        }

        public void SyncDataBack()
        {
            int i = 0;
            foreach (var e in query.Entities)
            {
                e.Set(c1[i]);
                e.Set(c2[i]);
                e.Set(c3[i]);
                i++;
            }
        }

        public void Dispose()
        {
            if (c1.IsCreated) c1.Dispose();
            if (c2.IsCreated) c2.Dispose();
            if (c3.IsCreated) c3.Dispose();
            if (entityIds.IsCreated) entityIds.Dispose();
        }
    }

    #endregion

    #region Wrapper jobs
    // These are Burstable IJobFor wrappers that invoke the user job per index.

    [BurstCompile]
    internal struct QueryJob1<T1, TJob> : IJobFor
        where T1 : struct, IComponent
        where TJob : struct, IJobQuery<T1>
    {
        [ReadOnly] internal NativeArray<int> entityIds;
        internal NativeArray<T1> c1;
        internal TJob job;

        public void Execute(int index)
        {
            var a = c1[index];
            job.Execute(ref a, new EntityRef(entityIds[index]));
            c1[index] = a;
        }
    }

    [BurstCompile]
    internal struct QueryJob2<T1, T2, TJob> : IJobFor
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where TJob : struct, IJobQuery<T1, T2>
    {
        [ReadOnly] internal NativeArray<int> entityIds;
        internal NativeArray<T1> c1;
        internal NativeArray<T2> c2;
        internal TJob job;

        public void Execute(int index)
        {
            var a = c1[index];
            var b = c2[index];
            job.Execute(ref a, ref b, new EntityRef(entityIds[index]));
            c1[index] = a;
            c2[index] = b;
        }
    }

    [BurstCompile]
    internal struct QueryJob3<T1, T2, T3, TJob> : IJobFor
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
        where TJob : struct, IJobQuery<T1, T2, T3>
    {
        [ReadOnly] internal NativeArray<int> entityIds;
        internal NativeArray<T1> c1;
        internal NativeArray<T2> c2;
        internal NativeArray<T3> c3;
        internal TJob job;

        public void Execute(int index)
        {
            var a = c1[index];
            var b = c2[index];
            var c = c3[index];
            job.Execute(ref a, ref b, ref c, new EntityRef(entityIds[index]));
            c1[index] = a;
            c2[index] = b;
            c3[index] = c;
        }
    }
    #endregion
}

