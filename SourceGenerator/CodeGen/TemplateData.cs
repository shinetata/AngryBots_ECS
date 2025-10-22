using System.Collections.Generic;

namespace PGD.Jobs.SourceGenerator.CodeGen
{
    /// <summary>
    /// 传递给模板的数据结构
    /// </summary>
    internal class TemplateData
    {
        /// <summary>
        /// 命名空间（可选）
        /// </summary>
        public string Namespace { get; set; } = string.Empty;

        /// <summary>
        /// Job 名称
        /// </summary>
        public string JobName { get; set; } = string.Empty;

        /// <summary>
        /// Wrapper 名称
        /// </summary>
        public string WrapperName { get; set; } = string.Empty;

        /// <summary>
        /// Extensions 类名称
        /// </summary>
        public string ExtensionsName { get; set; } = string.Empty;

        /// <summary>
        /// Using 语句列表
        /// </summary>
        public List<string> Usings { get; set; } = new List<string>();

        /// <summary>
        /// Wrapper 结构体字段定义
        /// </summary>
        public string WrapperFields { get; set; } = string.Empty;

        /// <summary>
        /// Wrapper Execute 方法体
        /// </summary>
        public string WrapperExecuteBody { get; set; } = string.Empty;

        /// <summary>
        /// ExecuteGenerated 方法签名
        /// </summary>
        public string ExecuteGeneratedSignature { get; set; } = string.Empty;

        /// <summary>
        /// ExecuteGenerated 调用参数
        /// </summary>
        public string ExecuteGeneratedCall { get; set; } = string.Empty;

        /// <summary>
        /// Extension 类静态字段定义
        /// </summary>
        public string ExtensionStaticFields { get; set; } = string.Empty;

    /// <summary>
    /// ScheduleParallel 方法体
    /// </summary>
    public string ScheduleParallelBody { get; set; } = string.Empty;

    /// <summary>
    /// ScheduleParallel(IQuery) 方法体
    /// </summary>
    public string ScheduleParallelWithQueryBody { get; set; } = string.Empty;
}

    /// <summary>
    /// 构建 TemplateData 的辅助类
    /// </summary>
    internal class TemplateDataBuilder
    {
        private readonly TemplateData _data = new TemplateData();

        public TemplateDataBuilder WithNamespace(string ns)
        {
            _data.Namespace = ns;
            return this;
        }

        public TemplateDataBuilder WithJobName(string jobName)
        {
            _data.JobName = jobName;
            _data.WrapperName = jobName + Constants.CodeGen.WrapperSuffix;
            _data.ExtensionsName = jobName + Constants.CodeGen.ExtensionsSuffix;
            return this;
        }

        public TemplateDataBuilder AddUsings(params string[] usings)
        {
            _data.Usings.AddRange(usings);
            return this;
        }

        public TemplateDataBuilder WithWrapperFields(string fields)
        {
            _data.WrapperFields = fields;
            return this;
        }

        public TemplateDataBuilder WithWrapperExecuteBody(string body)
        {
            _data.WrapperExecuteBody = body;
            return this;
        }

        public TemplateDataBuilder WithExecuteGeneratedSignature(string signature)
        {
            _data.ExecuteGeneratedSignature = signature;
            return this;
        }

        public TemplateDataBuilder WithExecuteGeneratedCall(string call)
        {
            _data.ExecuteGeneratedCall = call;
            return this;
        }

        public TemplateDataBuilder WithExtensionStaticFields(string fields)
        {
            _data.ExtensionStaticFields = fields;
            return this;
        }

    public TemplateDataBuilder WithScheduleParallelBody(string body)
    {
        _data.ScheduleParallelBody = body;
        return this;
    }

    public TemplateDataBuilder WithScheduleParallelWithQueryBody(string body)
    {
        _data.ScheduleParallelWithQueryBody = body;
        return this;
    }

    public TemplateData Build()
    {
        return _data;
    }
}
}

