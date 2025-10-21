namespace PGD.Jobs
{
    /// <summary>
    /// 替代 DOTS 中 <c>IJobEntity</c> 的 PGD 版签名接口。
    /// 使用者只需将 Job 结构体声明为 <c>partial struct XXX : IJobParallel</c> 并提供 Execute 方法，
    /// 编译器即可通过 Source Generator 生成并行调度包装。
    /// </summary>
    public interface IJobParallel
    {
    }
}
