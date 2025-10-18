using PGD;
using PGD.Jobs;
using System;
using Unity.Jobs;

namespace PGD.Jobs
{
    public class PGDJobManager
    {
        private Dependency deps;
        private IJobifiedSystem[] jobifiedSystems;
        private int count;

        public PGDJobManager()
        {
            deps = new Dependency();
            jobifiedSystems = Array.Empty<IJobifiedSystem>();
            count = 0;
        }

        public void Register(IJobifiedSystem jobifiedSystem)
        {
            if (jobifiedSystem == null)
            {
                return;
            }

            if (count == jobifiedSystems.Length)
            {
                Resize(Math.Max(4, 2 * count));
            }
            
            jobifiedSystems[count++] = jobifiedSystem;
            jobifiedSystem.SetJobHandle(ref deps);
        }

        private void Resize(int capacity)
        {
            var newArray = new IJobifiedSystem[capacity];
            var src = new ReadOnlySpan<IJobifiedSystem>(jobifiedSystems, 0, count);
            var dst = new Span<IJobifiedSystem>(newArray, 0, count);
            src.CopyTo(dst);
            jobifiedSystems = newArray;
        }

        public void Update()
        {
            if (count == 0)
            {
                return;
            }
            
            deps.jobs.Complete();
            var systems = jobifiedSystems.AsSpan(0, count);
            foreach (var jobSys in systems)
            {
                jobSys.SyncDataBack();
                jobSys.Dispose();
            }
        }
    }
    
    public class Dependency
    {
        public JobHandle jobs;
    }
}