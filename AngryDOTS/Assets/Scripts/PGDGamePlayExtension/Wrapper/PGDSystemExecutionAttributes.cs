using System;
using System.Collections.Generic;

namespace PGD
{
    /// <summary>
    /// Declares that the decorated PGD system should run before the specified systems.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class PGDUpdateBeforeAttribute : Attribute
    {
        public PGDUpdateBeforeAttribute(params Type[] systemTypes)
        {
            TargetTypes = systemTypes ?? Array.Empty<Type>();
        }

        public IReadOnlyList<Type> TargetTypes { get; }
    }

    /// <summary>
    /// Declares that the decorated PGD system should run after the specified systems.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class PGDUpdateAfterAttribute : Attribute
    {
        public PGDUpdateAfterAttribute(params Type[] systemTypes)
        {
            TargetTypes = systemTypes ?? Array.Empty<Type>();
        }

        public IReadOnlyList<Type> TargetTypes { get; }
    }

    /// <summary>
    /// Prevents the decorated PGD system from being auto-registered by <see cref="PGDContextBootstrap"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PGDDisableAutoRegisterAttribute : Attribute
    {
    }
}
