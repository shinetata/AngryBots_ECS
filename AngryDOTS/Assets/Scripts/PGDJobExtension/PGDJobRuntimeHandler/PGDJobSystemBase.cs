using System;
using Unity.Jobs;

namespace PGD.Jobs
{
    /// <summary>
    /// Base class for PGD systems that schedule jobs via the simplified job API.
    /// Implements <see cref="IJobifiedSystem"/> so derived systems no longer need to wire the Plumbing manually.
    /// </summary>
    public abstract class PGDJobSystemBase : PgdSystemBase, IJobifiedSystem
    {
        private Dependency dependency;

        /// <summary>
        /// Gets the PGD world associated with this system. Throws if the system is not registered with a world yet.
        /// </summary>
        protected internal IECSWorld World => ParentCollection?.World ?? throw new InvalidOperationException(
            $"{GetType().Name} is not bound to a PGD world. Ensure the system is registered before scheduling jobs.");

        /// <summary>
        /// Gets the current Dependency handle provided by <see cref="PGDJobManager"/>.
        /// </summary>
        protected JobHandle DependencyHandle => dependency?.jobs ?? default;

        /// <summary>
        /// Combine a newly scheduled job handle with the shared dependency provided by <see cref="PGDJobManager"/>.
        /// </summary>
        /// <param name="handle">The newly produced handle.</param>
        protected void CombineDependency(JobHandle handle)
        {
            if (dependency == null)
            {
                return;
            }

            dependency.jobs = JobHandle.CombineDependencies(dependency.jobs, handle);
        }

        void IJobifiedSystem.SetJobHandle(ref Dependency deps)
        {
            dependency = deps;
            OnJobSystemRegistered();
        }

        void IJobifiedSystem.SyncDataBack()
        {
            OnJobsSynced();
        }

        void IJobifiedSystem.Dispose()
        {
            OnJobsDisposed();
        }

        /// <summary>
        /// Called after the system is registered with the <see cref="PGDJobManager"/>.
        /// </summary>
        protected virtual void OnJobSystemRegistered() { }

        /// <summary>
        /// Called when job results should be applied back to runtime data.
        /// </summary>
        protected virtual void OnJobsSynced() { }

        /// <summary>
        /// Called when temporary resources built for the current frame should be disposed.
        /// </summary>
        protected virtual void OnJobsDisposed() { }

        protected sealed override void OnUpdateCollection()
        {
            OnUpdate();
        }

        /// <summary>
        /// Implement your per-frame logic here. Schedule jobs via the provided helper extensions.
        /// </summary>
        protected abstract void OnUpdate();
    }
}
