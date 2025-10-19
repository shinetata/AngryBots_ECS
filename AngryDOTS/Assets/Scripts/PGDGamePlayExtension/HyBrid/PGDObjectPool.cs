using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;

namespace PGD
{
    // 每个预制体池的状态封装
    internal class PoolState
    {
        public GameObject Prefab { get; }
        public List<GameObject> Objects { get; } = new List<GameObject>();
        public Queue<int> AvailableIds { get; } = new Queue<int>();
        public HashSet<int> ActiveIds { get; } = new HashSet<int>(); // 跟踪活跃对象，防重复回收
        public List<IEntity> Entities { get; } = new List<IEntity>();
        public IEntity TemplateEntity;
        public int NextId { get; set; } = 0;
        public int MaxSize { get; set; } = DEFAULT_MAX_SIZE;
        private const int DEFAULT_MAX_SIZE = 10000; // 默认最大容量

        public PoolState(GameObject prefab)
        {
            Prefab = prefab;
        }

        public int TotalCount => Objects.Count;
        public int ActiveCount => ActiveIds.Count;
        public int InactiveCount => AvailableIds.Count;
        public bool IsFull => TotalCount >= MaxSize;
        public bool IsEmpty => TotalCount == 0;
    }

    public class PGDObjectPool
    {
        #region Singleton
        private static PGDObjectPool instance;
        public static PGDObjectPool Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PGDObjectPool();
                }
                return instance;
            }
        }
        #endregion

        #region Storage
		private readonly Dictionary<GameObject, PoolState> pools = new Dictionary<GameObject, PoolState>();
		// 仅维护实例 -> prefab 的全局映射，用于 O(1) 反查 prefab
		private readonly Dictionary<GameObject, GameObject> instanceToPrefab = new Dictionary<GameObject, GameObject>();
        #endregion

        private PGDObjectPool() { }

        #region Pool Setup
        // 使用GameObject初始化池
        public void InitializePool(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("InitializePool(GameObject) failed: prefab is null.");
                return;
            }

            if (IsInitialized(prefab))
            {
                Debug.LogWarning($"Pool for {prefab.name} already initialized!");
                return;
            }

            pools[prefab] = new PoolState(prefab);

            // 初始化创建模板Entity并写入GoLink，标记为prefab，后续query处理中排除prefab
            var world = PGDGameContext.GetWorld();
            if (world != null)
            {
                var template = world.CreateEntity(new GoLink(prefab, -1, prefab.name, true));
                // 让模板具备基础 Transform 组件
                template.AddComponent(new PGDLocalTransform {
                    Position = prefab.transform.position,
                    Rotation = prefab.transform.rotation
                });
                pools[prefab].TemplateEntity = template;
            }
        }

        // 使用GameObject初始化池, 并指定模板Entity
        public void InitializePool(GameObject prefab, IEntity templateEntity)
        {
            if (prefab == null)
            {
                Debug.LogError("InitializePool(GameObject) failed: prefab is null.");
                return;
            }
            if (templateEntity == null)
            {
                Debug.LogError("InitializePool(GameObject, IEntity) failed: templateEntity is null.");
                return;
            }
            if (IsInitialized(prefab))
            {
                Debug.LogWarning($"Pool for {prefab.name} already initialized!");
                return;
            }

            templateEntity.AddComponent(new GoLink(prefab, -1, prefab.name, true));
            pools[prefab] = new PoolState(prefab);
            pools[prefab].TemplateEntity = templateEntity;
        }
        #endregion

        #region InstantiateEntity
        // 判断go是否已经初始化
        public bool IsInitialized(GameObject prefab)
        {
            return pools.ContainsKey(prefab);
        }

        // 基于传入的实体克隆一个新实体，并绑定GameObject
        public IEntity InstantiateEntity(IEntity templateEntity)
        {
            if (!TryResolveSpawnArguments(templateEntity, out var prefab, out var position, out var rotation, out var parent))
            {
                return default;
            }
            return SpawnEntity(prefab, position, rotation, parent);
        }

        // 基于传入 list 的容量批量克隆实体
        public int InstantiateEntity(IEntity templateEntity, List<IEntity> entities)
        {
            if (entities == null)
            {
                Debug.LogError("InstantiateEntity failed: entities list is null.");
                return 0;
            }

            if (!TryResolveSpawnArguments(templateEntity, out var prefab, out var position, out var rotation, out var parent))
            {
                entities.Clear();
                return 0;
            }

            int targetCount = entities.Capacity;
            entities.Clear();
            if (targetCount <= 0)
            {
                return 0;
            }

            for (int i = 0; i < targetCount; i++)
            {
                var entity = SpawnEntity(prefab, position, rotation, parent);
                if (entity.Id == 0)
                {
                    break;
                }
                entities.Add(entity);
            }

            return entities.Count;
        }

        // 基于传入的NativeArray批量克隆实体
        public int InstantiateEntity(IEntity templateEntity, NativeArray<IEntity> entities)
        {
            if (entities == null)
            {
                Debug.LogError("InstantiateEntity failed: entities list is null.");
                return 0;
            }
            if (!TryResolveSpawnArguments(templateEntity, out var prefab, out var position, out var rotation, out var parent))
            {
                entities.Dispose();
                return 0;
            }

            int targetCount = entities.Length;
            entities.Dispose();
            if (targetCount <= 0)
            {
                return 0;
            }

            for (int i = 0; i < targetCount; i++)
            {
                var entity = SpawnEntity(prefab, position, rotation, parent);
                if (entity.Id == 0)
                {
                    break;
                }
            }

            return entities.Length;
        }
        #endregion

        #region Spawn & Retrieve
        // 生成对象（返回槽位 id）
        public int SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!IsInitialized(prefab))
            {
                Debug.LogError($"Pool for {prefab.name} not initialized!");
                return -1;
            }

            var state = pools[prefab];
            int id;

            if (state.AvailableIds.Count > 0)
            {
                id = state.AvailableIds.Dequeue();
            }
            else
            {
                id = CreateNewObject(prefab);
                if (id == -1) return -1;
            }

            // 自愈 + 槽位实体补建
            GameObject obj = EnsureObjectAlive(state, prefab, id);
            EnsureSlotEntity(state, prefab, id, obj);

            // 激活并设置位置
            ActivateObject(obj, position, rotation, parent);

            // 标记为活跃并同步槽位实体
            state.ActiveIds.Add(id);
            if (!SyncSlotEntity(state, prefab, id, obj, true))
            {
                Debug.LogError($"SpawnObject failed: entity sync error. Prefab={prefab.name}, Id={id}");
            }

            return id;
        }

        // 通过prefab和槽位id回收派生对象
        public void ReturnObject(GameObject go, int id, CommandQueue queue = null)
        {
            if (go == null) 
            {
                Debug.LogError($"ReturnObject failed: go is null. GoId={id}");
                return;
            }
            if (pools.TryGetValue(go, out var state))
            {
                ReturnObjectByPrefab(go, id, queue);
                return;
            }

            ReturnObjectByInstance(go, id, queue);
        }

        // 通过prefab和槽位id获取派生对象
        public GameObject GetObject(GameObject prefab, int id)
        {
            if (!IsInitialized(prefab) || id < 0 || id >= pools[prefab].Objects.Count)
                return null;
            return pools[prefab].Objects[id];
        }

        // 通过prefab和槽位id检查派生对象是否活跃
        public bool IsObjectActive(GameObject prefab, int id)
        {
            if (!IsInitialized(prefab) || id < 0 || id >= pools[prefab].Objects.Count)
                return false;
            return pools[prefab].ActiveIds.Contains(id);
        }
        #endregion

        #region Maintenance
        // 清空池，可选择是否删除模板实体，默认不删除
        public void ClearPool(GameObject prefab, bool deleteTemplate = false)
        {
            if (!IsInitialized(prefab)) return;

            var state = pools[prefab];
            foreach (var obj in state.Objects)
            {
                if (obj != null)
                {
                    instanceToPrefab.Remove(obj);
                    Object.Destroy(obj);
                }
            }

            // 清理已绑定的槽位实体
            foreach (var e in state.Entities)
            {
                if (e.Id != 0 && !e.IsDeleted()) e.DeleteEntity();
            }
            state.Entities.Clear();

            // 根据参数决定是否删除模板实体
            if (deleteTemplate)
            {
                if (state.TemplateEntity.Id != 0 && !state.TemplateEntity.IsDeleted())
                {
                    state.TemplateEntity.DeleteEntity();
                }
                state.TemplateEntity = default;
            }

            state.Objects.Clear();
            state.AvailableIds.Clear();
            state.ActiveIds.Clear();
            state.NextId = 0;
        }

        // 清空所有池，可选择是否删除模板实体，默认不删除；
        public void ClearAllPools(bool deleteTemplate = false)
        {
            foreach (var prefab in new List<GameObject>(pools.Keys))
            {
                ClearPool(prefab, deleteTemplate);
            }
        }

        // 预热池（初始化时调用）
        public void WarmupPool(GameObject prefab, int count)
        {
            if (!IsInitialized(prefab))
            {
                Debug.LogError($"Pool for {prefab.name} not initialized!");
                return;
            }

            var state = pools[prefab];
            int currentCount = state.TotalCount;
            int capacityLeft = state.MaxSize - currentCount;
            int toAdd = Mathf.Clamp(capacityLeft, 0, count);

            for (int i = 0; i < toAdd; i++)
            {
                GameObject obj = Object.Instantiate(prefab);
                obj.SetActive(false);
                state.Objects.Add(obj);
                state.AvailableIds.Enqueue(currentCount + i);
                // 建立实例 -> prefab 的全局映射
                instanceToPrefab[obj] = prefab;

                if (TryEnsureTemplateEntity(prefab, out var templateEntity))
                {
                    var id = currentCount + i;
                    var entity = templateEntity.CloneEntity();
                    entity.Set(new GoLink(obj, id, prefab.name, false));
                    entity.Active = false; // 预热创建的槽位实体默认非激活
                    while (state.Entities.Count <= id)
                    {
                        state.Entities.Add(default);
                    }
                    state.Entities[id] = entity;
                }
            }
            state.NextId = currentCount + toAdd;
        }

        // 设置最大容量（初始化时配置，不建议运行期调用！）
        public void SetMaxSize(GameObject prefab, int maxSize)
        {
            if (!IsInitialized(prefab)) return;
            var state = pools[prefab];
            int newMax = Mathf.Max(0, maxSize);
            int currentCount = state.TotalCount;
            if (newMax < currentCount)
            {
                Debug.LogWarning($"SetMaxSize({prefab.name}) clamped from {newMax} to current total {currentCount}.");
                newMax = currentCount;
            }
            state.MaxSize = newMax;
        }

        // 获取池统计信息
        public (int total, int active, int inactive, int maxSize) GetPoolStats(GameObject prefab)
        {
            if (!IsInitialized(prefab))
            {
                return (0, 0, 0, 0);
            }

            var state = pools[prefab];
            return (state.TotalCount, state.ActiveCount, state.InactiveCount, state.MaxSize);
        }

        // 获取当前已注册的所有 Prefab（副本列表，外部修改不会影响内部状态）
        public List<GameObject> GetRegisteredPrefabs()
        {
            return new List<GameObject>(pools.Keys);
        }


        // 获取池大小
        public int GetPoolSize(GameObject prefab)
        {
            return IsInitialized(prefab) ? pools[prefab].TotalCount : 0;
        }
        
        // 获取可用数量
        public int GetAvailableCount(GameObject prefab)
        {
            return IsInitialized(prefab) ? pools[prefab].InactiveCount : 0;
        }
        
        // 获取活跃数量
        public int GetActiveCount(GameObject prefab)
        {
            return IsInitialized(prefab) ? pools[prefab].ActiveCount : 0;
        }

        // 获取最大容量
        public int GetMaxSize(GameObject prefab)
        {
            return IsInitialized(prefab) ? pools[prefab].MaxSize : 0;
        }

        // 检查池是否满
        public bool IsPoolFull(GameObject prefab)
        {
            return IsInitialized(prefab) && pools[prefab].IsFull;
        }

        // 检查池是否为空
        public bool IsPoolEmpty(GameObject prefab)
        {
            return IsInitialized(prefab) && pools[prefab].IsEmpty;
        }
        #endregion

        #region Template Accessors
        // 获取该 prefab 的模板实体
        public bool TryGetTemplateEntity(GameObject prefab, out IEntity entity)
        {
            entity = default;
            if (!IsInitialized(prefab)) return false;
            var e = pools[prefab].TemplateEntity;
            if (e.Id == 0 || e.IsDeleted()) return false;
            entity = e;
            return true;
        }

        // 获取槽位实体（与 GameObject 槽位一一对应，GoLink.isPrefab=false）。
        public bool TryGetSlotEntity(GameObject prefab, int id, out IEntity entity)
        {
            entity = default;
            if (!IsInitialized(prefab)) return false;
            var state = pools[prefab];
            if (id < 0 || id >= state.Entities.Count) return false;
            var e = state.Entities[id];
            if (e.Id == 0 || e.IsDeleted()) return false;
            entity = e;
            return true;
        }
        #endregion
        
        #region Internal Helpers
        private bool TryResolveSpawnArguments(IEntity templateEntity, out GameObject prefab, out Vector3 position, out Quaternion rotation, out Transform parent)
        {
            prefab = null;
            position = default;
            rotation = default;
            parent = null;

            if (templateEntity.Id == 0 || templateEntity.IsDeleted())
            {
                Debug.LogError("InstantiateEntity failed: templateEntity is invalid.");
                return false;
            }
            if (!templateEntity.TryGetComponent<GoLink>(out var gl))
            {
                Debug.LogError("InstantiateEntity failed: templateEntity has no GoLink.");
                return false;
            }
            if (gl.gameObject == null || !gl.isPrefab)
            {
                Debug.LogError("InstantiateEntity failed: GoLink.gameObject is invalid.");
                return false;
            }

            prefab = gl.gameObject;
            position = gl.transform.position;
            rotation = gl.transform.rotation;
            parent = gl.transform.parent;
            return true;
        }
        
        private IEntity SpawnEntity(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var id = SpawnObject(prefab, position, rotation, parent);
            if (id < 0) 
            {
                Debug.LogError($"SpawnEntity failed: GoLinkId < 0. Prefab={prefab.name}");
                ReturnObject(prefab, id);
                return default;
            }
            var state = pools[prefab];
            var entity = state.Entities[id];
            if (entity.Id != 0)
            {
                return entity;
            }

            ReturnObject(prefab, id);
            Debug.LogError($"SpawnEntity failed: EntityId == 0. Prefab={prefab.name}");
            return default;
        }
        
        // 创建新对象
        private int CreateNewObject(GameObject prefab)
        {
            var state = pools[prefab];
            if (state.IsFull)
            {
                Debug.LogWarning($"Pool for {prefab.name} has reached its maximum size of {state.MaxSize}");
                return -1;
            }

            GameObject obj = Object.Instantiate(prefab);
            obj.SetActive(false);
            int id = state.NextId++;
            state.Objects.Add(obj);
            // 建立实例 -> prefab 的全局映射
            instanceToPrefab[obj] = prefab;

            // 同步创建实体并绑定GoLink（按需确保模板与列表容量）
            if (TryEnsureTemplateEntity(prefab, out var templateEntity))
            {
                var newId = state.Objects.Count - 1;
                var entity = templateEntity.CloneEntity();
                entity.Set(new GoLink(obj, newId, prefab.name, false));
                entity.Active = false;
                EnsureEntityListSize(state, newId);
                state.Entities[newId] = entity;
            }

            return id;
        }
        
        // 确保存在模板实体（在被 ClearPool 删除或其他原因失效后，按需重建）
        private bool TryEnsureTemplateEntity(GameObject prefab, out IEntity template)
        {
            template = default;
            if (!IsInitialized(prefab)) return false;
            var state = pools[prefab];
            if (state.TemplateEntity.Id != 0 && !state.TemplateEntity.IsDeleted())
            {
                template = state.TemplateEntity;
                return true;
            }
            var world = PGDGameContext.GetWorld();
            if (world == null) return false;
            var tmpl = world.CreateEntity(new GoLink(prefab, -1, prefab.name, true));
            tmpl.AddComponent(new PGDLocalTransform {
                Position = prefab.transform.position,
                Rotation = prefab.transform.rotation
            });
            state.TemplateEntity = tmpl;
            template = tmpl;
            return true;
        }

        // 确保实体列表容量至少到 id（用 default 填充缺口）
        private static void EnsureEntityListSize(PoolState state, int id)
        {
            while (state.Entities.Count <= id)
            {
                state.Entities.Add(default);
            }
        }

        // 若对象槽位被外部销毁，则自愈重建，保持原实体的 GoLink 绑定
        private GameObject EnsureObjectAlive(PoolState state, GameObject prefab, int id)
        {
            GameObject obj = state.Objects[id];
            if (obj == null)
            {
                obj = Object.Instantiate(prefab);
                obj.SetActive(false);
                state.Objects[id] = obj;
                // 更新实例 -> prefab 的全局映射
                instanceToPrefab[obj] = prefab;
                Debug.LogWarning($"Replaced destroyed object at id {id} for {prefab.name}");

                if (id < state.Entities.Count)
                {
                    var e = state.Entities[id];
                    if (e.Id != 0 && !e.IsDeleted())
                    {
                        e.Set(new GoLink(obj, id, prefab.name, false));
                    }
                }
            }
            return obj;
        }

        // 槽位实体缺失或已失效时，从模板克隆并覆盖 GoLink，回填列表
        private void EnsureSlotEntity(PoolState state, GameObject prefab, int id, GameObject obj)
        {
            if (id < state.Entities.Count)
            {
                var cur = state.Entities[id];
                if (cur.Id != 0 && !cur.IsDeleted())
                {
                    return;
                }
            }
            if (TryEnsureTemplateEntity(prefab, out var tmpl))
            {
                EnsureEntityListSize(state, id);
                var e = tmpl.CloneEntity();
                e.Set(new GoLink(obj, id, prefab.name, false));
                state.Entities[id] = e;
            }
        }

        private static void ActivateObject(GameObject obj, Vector3 pos, Quaternion rot, Transform parent)
        {
            obj.SetActive(true);
            obj.transform.SetPositionAndRotation(pos, rot);
            if (parent != null)
            {
                obj.transform.SetParent(parent, false);
            }
        }

        private static void DeactivateObject(GameObject obj)
        {
            obj.SetActive(false);
            obj.transform.SetParent(null, false);
        }

        private void ReturnObjectByPrefab(GameObject prefab, int id, CommandQueue queue = null)
        {
            if (!IsInitialized(prefab) || id < 0 || id >= pools[prefab].Objects.Count)
            {
                Debug.LogError($"ReturnObjectByPrefab failed: prefab {prefab.name} not initialized or id {id} out of range.");
                return;
            }
            var state = pools[prefab];
            if (!state.ActiveIds.Contains(id))
            {
                Debug.LogWarning($"Trying to return inactive object id {id} for {prefab.name}");
                return;
            }

            GameObject obj = state.Objects[id];
            if (obj == null) return;

            // 回收
            DeactivateObject(obj);
            if (id < state.Entities.Count)
            {
                var e = state.Entities[id];
                if (e.Id != 0) 
                {
                    if (queue != null)
                    {
                        queue.AddTag<Inactive>(e.Id);
                    } else {
                        e.Active = false;
                    }  
                }
            }
            state.AvailableIds.Enqueue(id);
            state.ActiveIds.Remove(id);
        }

        private void ReturnObjectByInstance(GameObject instance, int goId, CommandQueue queue = null)
        {
            if (instance == null) return;
            if (!instanceToPrefab.TryGetValue(instance, out var prefab))
            {
                Debug.LogWarning("ReturnObject(GameObject) failed: instance is not managed by PGDObjectPool.");
                return;
            }
            if (!IsInitialized(prefab))
            {
                // 映射存在但池已被清理
                instanceToPrefab.Remove(instance);
                Debug.LogWarning("ReturnObject(GameObject) failed: pool not initialized for mapped prefab.");
                return;
            }
            var state = pools[prefab];
            if (state.Objects[goId] != instance)
            {
                Debug.LogWarning($"ReturnObject(GameObject) failed: id {goId} does not match provided instance for prefab {prefab.name}.");
                return;
            }
            ReturnObjectByPrefab(prefab, goId, queue);
        }

        private bool SyncSlotEntity(PoolState state, GameObject prefab, int id, GameObject obj, bool activate)
        {
            if (id < 0 || id >= state.Entities.Count)
            {
                return false;
            }

            var entity = state.Entities[id];
            if (entity.Id == 0)
            {
                return false;
            }

            if (!TryEnsureTemplateEntity(prefab, out var templateEntity))
            {
                return false;
            }

            templateEntity.CopyEntityTo(entity);
            entity.Set(new GoLink(obj, id, prefab.name, false));
            entity.Active = activate;
            return true;
        }
        #endregion
    }
}
