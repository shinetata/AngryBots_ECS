using System;
using System.Collections.Generic;
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
        
        // 静态上下文，供生成的代码使用
        [ThreadStatic] private static PGDJobSystemBase s_currentSystem;
        private static readonly List<System.Action> s_syncCallbacks = new List<System.Action>();
        private static readonly List<System.Action> s_disposeCallbacks = new List<System.Action>();

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
        /// 当前执行的 System（供生成的代码使用）
        /// </summary>
        public static PGDJobSystemBase CurrentSystem => s_currentSystem 
            ?? throw new InvalidOperationException("No active PGDJobSystemBase. Ensure jobs are scheduled from within OnUpdate().");

        /// <summary>
        /// 当前的 World（供生成的代码使用）
        /// </summary>
        public static IECSWorld CurrentWorld => s_currentSystem?.World;

        /// <summary>
        /// 当前的依赖句柄（供生成的代码使用）
        /// </summary>
        public static JobHandle CurrentDependency => s_currentSystem?.DependencyHandle ?? default;

        /// <summary>
        /// 立即更新依赖链（用于多个 Job 顺序调度）
        /// </summary>
        public static void UpdateDependency(JobHandle handle)
        {
            if (s_currentSystem == null)
                throw new InvalidOperationException("Cannot update dependency outside of OnUpdate().");

            s_currentSystem.CombineDependency(handle);
        }

        /// <summary>
        /// 注册 Job 完成后的回调
        /// </summary>
        public static void RegisterCallbacks(System.Action onComplete = null, System.Action onDispose = null)
        {
            if (s_currentSystem == null)
                throw new InvalidOperationException("Cannot register callbacks outside of OnUpdate().");
            
            if (onComplete != null)
                s_syncCallbacks.Add(onComplete);
            
            if (onDispose != null)
                s_disposeCallbacks.Add(onDispose);
        }

        /// <summary>
        /// 注册一个 Job 句柄，并添加完成后的回调（旧版本兼容）
        /// </summary>
        public static void RegisterJob(JobHandle handle, System.Action onComplete = null, System.Action onDispose = null)
        {
            UpdateDependency(handle);
            RegisterCallbacks(onComplete, onDispose);
        }

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
            // 执行所有注册的同步回调
            foreach (var callback in s_syncCallbacks)
            {
                callback?.Invoke();
            }
            s_syncCallbacks.Clear();
            
            OnJobsSynced();
        }

        void IJobifiedSystem.Dispose()
        {
            // 执行所有注册的清理回调
            foreach (var callback in s_disposeCallbacks)
            {
                callback?.Invoke();
            }
            s_disposeCallbacks.Clear();
            
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
            s_currentSystem = this;
            try
            {
                OnUpdate();
            }
            finally
            {
                s_currentSystem = null;
            }
        }

        /// <summary>
        /// Implement your per-frame logic here. Schedule jobs via the provided helper extensions.
        /// </summary>
        protected abstract void OnUpdate();
    }
}
