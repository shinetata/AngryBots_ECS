using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace PGD.Jobs.SourceGenerator.Models
{
    /// <summary>
    /// 存储分析后的 Job 信息
    /// </summary>
    internal class JobInfo
    {
        /// <summary>
        /// Job 的类型符号
        /// </summary>
        public INamedTypeSymbol JobSymbol { get; set; } = null!;

        /// <summary>
        /// Job 的名称
        /// </summary>
        public string JobName { get; set; } = string.Empty;

        /// <summary>
        /// Job 的命名空间
        /// </summary>
        public string? Namespace { get; set; }

        /// <summary>
        /// Execute 方法的参数列表
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();

        /// <summary>
        /// WithAll 特性中的类型
        /// </summary>
        public List<ITypeSymbol> WithAllTypes { get; set; } = new List<ITypeSymbol>();

        /// <summary>
        /// WithAny 特性中的类型
        /// </summary>
        public List<ITypeSymbol> WithAnyTypes { get; set; } = new List<ITypeSymbol>();

        /// <summary>
        /// WithNone 特性中的类型
        /// </summary>
        public List<ITypeSymbol> WithNoneTypes { get; set; } = new List<ITypeSymbol>();

        /// <summary>
        /// 是否有 BurstCompile 特性
        /// </summary>
        public bool HasBurstCompile { get; set; }
    }

    /// <summary>
    /// 参数信息
    /// </summary>
    internal class ParameterInfo
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 参数类型
        /// </summary>
        public ITypeSymbol Type { get; set; } = null!;

        /// <summary>
        /// 参数类型的名称（不包含命名空间）
        /// </summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>
        /// 参数类型的完整名称
        /// </summary>
        public string TypeFullName { get; set; } = string.Empty;

        /// <summary>
        /// 访问模式
        /// </summary>
        public RefKind RefKind { get; set; }

        /// <summary>
        /// 是否是只读访问（in 或普通参数）
        /// </summary>
        public bool IsReadOnly => RefKind == RefKind.In || RefKind == RefKind.None;

        /// <summary>
        /// 是否是读写访问（ref）
        /// </summary>
        public bool IsReadWrite => RefKind == RefKind.Ref;

        /// <summary>
        /// 是否是只写访问（out）
        /// </summary>
        public bool IsWriteOnly => RefKind == RefKind.Out;

        /// <summary>
        /// 是否是实体参数（IEntity）
        /// </summary>
        public bool IsEntity { get; set; }

        /// <summary>
        /// 是否是实体索引参数（int entityIndex）
        /// </summary>
        public bool IsEntityIndex { get; set; }

        /// <summary>
        /// 是否是组件参数（既不是实体也不是索引）
        /// </summary>
        public bool IsComponent => !IsEntity && !IsEntityIndex;

        /// <summary>
        /// 是否需要写回（ref 组件参数）
        /// </summary>
        public bool RequiresWriteBack => IsComponent && RefKind == RefKind.Ref;

        /// <summary>
        /// 是否是只读组件参数
        /// </summary>
        public bool IsReadOnlyComponent => IsComponent && IsReadOnly;

        /// <summary>
        /// 是否是可写组件参数（ref 组件）
        /// </summary>
        public bool IsWritableComponent => IsComponent && RefKind == RefKind.Ref;
    }
}

