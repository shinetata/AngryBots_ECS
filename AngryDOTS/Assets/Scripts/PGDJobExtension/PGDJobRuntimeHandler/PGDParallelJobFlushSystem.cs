using PGD;
using Unity.Jobs;

namespace PGD.Jobs
{
    /// <summary>
    /// 自动刷写并释放 PGD 并行 Job 的结果。
    /// </summary>
    public sealed class PGDParallelJobFlushSystem : PgdSystemBase, IJobifiedSystem
    {
        private Dependency dependency;
        private readonly IECSWorld world;

        internal PGDParallelJobFlushSystem(IECSWorld world)
        {
            this.world = world;
        }

        void IJobifiedSystem.SetJobHandle(ref Dependency deps)
        {
            dependency = deps;
        }

        void IJobifiedSystem.SyncDataBack()
        {
            var worldContext = world ?? ParentCollection?.World;
            if (worldContext != null)
            {
                PGDParallelJobRegistry.FlushCompleted(worldContext, dependency.jobs);
            }
            else
            {
                dependency.jobs.Complete();
            }

            dependency.jobs = default;
        }

        void IJobifiedSystem.Dispose()
        {
        }

        internal void CombineHandle(JobHandle handle)
        {
            dependency.jobs = JobHandle.CombineDependencies(dependency.jobs, handle);
        }

        protected override void OnUpdateCollection()
        {
        }
    }
}
