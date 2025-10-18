namespace  PGD.Jobs
{
    public interface IJobifiedSystem
    {
        void SetJobHandle(ref Dependency deps);

        void SyncDataBack();

        void Dispose();
    }
}