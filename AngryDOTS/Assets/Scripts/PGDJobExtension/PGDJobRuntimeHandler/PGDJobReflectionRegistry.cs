using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Jobs;

namespace PGD.Jobs
{
    /// <summary>
    /// 运行时反射注册表，为没有 Source Generator 生成代码的 Job 提供回退支持
    /// </summary>
    internal static class PGDJobReflectionRegistry
    {
        private static readonly Dictionary<Type, object> s_descriptorCache = new Dictionary<Type, object>();

        /// <summary>
        /// 获取或创建运行时描述器（使用反射）
        /// </summary>
        public static IPGDParallelJobDescriptor<TJob> GetOrCreateDescriptor<TJob>()
            where TJob : struct, IJobParallel
        {
            var jobType = typeof(TJob);
            
            lock (s_descriptorCache)
            {
                if (s_descriptorCache.TryGetValue(jobType, out var cached))
                {
                    return (IPGDParallelJobDescriptor<TJob>)cached;
                }

                // 创建反射描述器
                var descriptor = new ReflectionJobDescriptor<TJob>();
                s_descriptorCache[jobType] = descriptor;
                return descriptor;
            }
        }

        /// <summary>
        /// 基于反射的 Job 描述器（运行时回退实现）
        /// </summary>
        private class ReflectionJobDescriptor<TJob> : IPGDParallelJobDescriptor<TJob>
            where TJob : struct, IJobParallel
        {
            private readonly MethodInfo executeMethod;
            private readonly ParameterInfo[] parameters;

            public ReflectionJobDescriptor()
            {
                // 查找 Execute 方法
                executeMethod = typeof(TJob).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "Execute");

                if (executeMethod == null)
                {
                    throw new InvalidOperationException(
                        $"Job '{typeof(TJob).Name}' must have an Execute method.");
                }

                parameters = executeMethod.GetParameters();
            }

            public object CreateContext(IECSWorld world, Allocator allocator, ref TJob job)
            {
                // 创建运行时上下文
                return new ReflectionJobContext
                {
                    world = world,
                    allocator = allocator,
                    executeMethod = executeMethod,
                    parameters = parameters
                };
            }

            public JobHandle Schedule(IECSWorld world, ref TJob job, object contextObj, JobHandle dependsOn)
            {
                var context = (ReflectionJobContext)contextObj;
                
                // 查询实体
                var entities = QueryEntities(world, parameters);
                
                if (entities.Count == 0)
                {
                    // 没有匹配的实体，直接返回
                    return dependsOn;
                }

                // 创建 GCHandle 数组来存储托管对象引用
                context.entityHandles = new NativeArray<System.IntPtr>(entities.Count, Allocator.TempJob);
                for (int i = 0; i < entities.Count; i++)
                {
                    var handle = System.Runtime.InteropServices.GCHandle.Alloc(entities[i]);
                    context.entityHandles[i] = System.Runtime.InteropServices.GCHandle.ToIntPtr(handle);
                }

                // 为 MethodInfo 和 ParameterInfo[] 创建 GCHandle
                context.executeMethodHandle = System.Runtime.InteropServices.GCHandle.Alloc(executeMethod);
                context.parametersHandle = System.Runtime.InteropServices.GCHandle.Alloc(parameters);

                // 创建包装 Job
                var wrapper = new ReflectionJobWrapper<TJob>
                {
                    job = job,
                    entityHandles = context.entityHandles,
                    executeMethodPtr = System.Runtime.InteropServices.GCHandle.ToIntPtr(context.executeMethodHandle),
                    parametersPtr = System.Runtime.InteropServices.GCHandle.ToIntPtr(context.parametersHandle)
                };

                // 调度为 IJobParallelFor（不阻塞，异步执行）
                return wrapper.Schedule(entities.Count, 64, dependsOn);
            }

            public void OnJobCompleted(IECSWorld world, object contextObj)
            {
                // Job 完成后清理资源
                var context = (ReflectionJobContext)contextObj;
                if (context.entityHandles.IsCreated)
                {
                    // 反射模式下，修改已经在 Job 执行时直接应用
                }
            }

            public void DisposeContext(object contextObj)
            {
                // 释放 GCHandle 和 NativeArray
                var context = (ReflectionJobContext)contextObj;
                
                if (context.entityHandles.IsCreated)
                {
                    for (int i = 0; i < context.entityHandles.Length; i++)
                    {
                        var gcHandle = System.Runtime.InteropServices.GCHandle.FromIntPtr(context.entityHandles[i]);
                        if (gcHandle.IsAllocated)
                            gcHandle.Free();
                    }
                    context.entityHandles.Dispose();
                }
                
                if (context.executeMethodHandle.IsAllocated)
                    context.executeMethodHandle.Free();
                    
                if (context.parametersHandle.IsAllocated)
                    context.parametersHandle.Free();
            }

            private List<IEntity> QueryEntities(IECSWorld world, ParameterInfo[] parameters)
            {
                var entities = new List<IEntity>();
                
                // 提取所需的组件类型
                var requiredTypes = parameters.Select(p => p.ParameterType.IsByRef 
                    ? p.ParameterType.GetElementType() 
                    : p.ParameterType).ToList();

                // 简单查询：检查每个实体是否有所有需要的组件
                foreach (var entity in world.Entities)
                {
                    bool hasAll = true;
                    foreach (var type in requiredTypes)
                    {
                        // 使用反射检查组件
                        var hasMethod = typeof(IEntity).GetMethod("Has")?.MakeGenericMethod(type);
                        if (hasMethod != null)
                        {
                            var hasComponent = (bool)hasMethod.Invoke(entity, null);
                            if (!hasComponent)
                            {
                                hasAll = false;
                                break;
                            }
                        }
                    }

                    if (hasAll)
                    {
                        entities.Add(entity);
                    }
                }

                return entities;
            }
        }

        /// <summary>
        /// 运行时上下文
        /// </summary>
        private class ReflectionJobContext
        {
            public IECSWorld world;
            public Allocator allocator;
            public MethodInfo executeMethod;
            public ParameterInfo[] parameters;
            
            // 用于清理的 GCHandle
            public NativeArray<System.IntPtr> entityHandles;
            public System.Runtime.InteropServices.GCHandle executeMethodHandle;
            public System.Runtime.InteropServices.GCHandle parametersHandle;
        }

        /// <summary>
        /// 反射 Job 包装器（使用 NativeArray 存储实体指针）
        /// 注意：此 Job 包含托管代码，无法被 Burst 编译
        /// </summary>
        private struct ReflectionJobWrapper<TJob> : IJobParallelFor
            where TJob : struct, IJobParallel
        {
            public TJob job;
            
            // 使用 NativeArray 存储实体的托管引用（通过 GCHandle）
            [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
            public NativeArray<System.IntPtr> entityHandles;
            
            [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
            public System.IntPtr executeMethodPtr;
            
            [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
            public System.IntPtr parametersPtr;

            public void Execute(int index)
            {
                // 从 GCHandle 恢复托管对象
                var entityHandle = System.Runtime.InteropServices.GCHandle.FromIntPtr(entityHandles[index]);
                var entity = (IEntity)entityHandle.Target;
                
                var executeMethod = System.Runtime.InteropServices.GCHandle.FromIntPtr(executeMethodPtr).Target as MethodInfo;
                var parameters = System.Runtime.InteropServices.GCHandle.FromIntPtr(parametersPtr).Target as ParameterInfo[];
                
                if (entity == null || executeMethod == null || parameters == null)
                    return;

                var args = new object[parameters.Length];

                // 收集参数
                for (int i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;
                    var componentType = paramType.IsByRef ? paramType.GetElementType() : paramType;
                    
                    // 使用反射获取组件
                    var getMethod = typeof(IEntity).GetMethod("Get")?.MakeGenericMethod(componentType);
                    if (getMethod != null)
                    {
                        args[i] = getMethod.Invoke(entity, null);
                    }
                }

                // 调用 Execute 方法
                var jobBoxed = (object)job;
                executeMethod.Invoke(jobBoxed, args);

                // 回写修改的组件（ref 参数）
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType.IsByRef)
                    {
                        var componentType = parameters[i].ParameterType.GetElementType();
                        var setMethod = typeof(IEntity).GetMethod("Set")?.MakeGenericMethod(componentType);
                        if (setMethod != null)
                        {
                            setMethod.Invoke(entity, new[] { args[i] });
                        }
                    }
                }
            }
        }
    }
}

