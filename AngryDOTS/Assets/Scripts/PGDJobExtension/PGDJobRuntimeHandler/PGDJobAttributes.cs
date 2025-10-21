using System;
using System.Collections.Generic;

namespace PGD.Jobs
{
    /// <summary>
    /// Marks a struct as a PGD job that can benefit from source-generated scheduling helpers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class PGDJobAttribute : Attribute
    {
    }

    /// <summary>
    /// Requires that the generated query includes all specified component or tag types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class WithAllAttribute : Attribute
    {
        public WithAllAttribute(params Type[] types)
        {
            ComponentTypes = types ?? Array.Empty<Type>();
        }

        public IReadOnlyList<Type> ComponentTypes { get; }
    }

    /// <summary>
    /// Requires that the generated query includes at least one of the specified component or tag types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class WithAnyAttribute : Attribute
    {
        public WithAnyAttribute(params Type[] types)
        {
            ComponentTypes = types ?? Array.Empty<Type>();
        }

        public IReadOnlyList<Type> ComponentTypes { get; }
    }

    /// <summary>
    /// Requires that the generated query excludes all specified component or tag types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class WithNoneAttribute : Attribute
    {
        public WithNoneAttribute(params Type[] types)
        {
            ComponentTypes = types ?? Array.Empty<Type>();
        }

        public IReadOnlyList<Type> ComponentTypes { get; }
    }
}
