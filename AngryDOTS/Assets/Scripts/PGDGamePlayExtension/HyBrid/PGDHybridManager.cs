using PGD;
using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-500)]
public class PGDHybridManager : MonoBehaviour
{
    [SerializeField] private List<GameObject> prefabList = new List<GameObject>();
    [SerializeField] private int prewarmPerPrefab = 0;
    
    void Awake() 
    {
        Initialize();
    }
    
    private void Initialize()
    {
        if (prefabList == null || prefabList.Count == 0)
        {
            return;
        }
        var world = PGDGameContext.GetWorld();
        if (world == null)
        {
            return;
        }
        foreach (var prefab in prefabList)
        {
            if (prefab == null) continue;
            switch (prefab.name)
            {
                case "Example":
                    // 为不同的Prefab配置模板Entity，配置参数并绑定
                    var entity = world.CreateEntity(new PGDPosition(0, 0, 0));
                    PGDObjectPool.Instance.InitializePool(prefab, entity);
                    // 预热池
                    PGDObjectPool.Instance.WarmupPool(prefab, 10);
                    break;
                case "Cube":
                    PGDObjectPool.Instance.InitializePool(prefab);
                    break;
                default:
                    PGDObjectPool.Instance.InitializePool(prefab);
                    SetPool(prefab);
                    break;
            }
        }   
    }

    private void SetPool(GameObject prefab)
    {
        if (prewarmPerPrefab > 0)
        {
            PGDObjectPool.Instance.WarmupPool(prefab, prewarmPerPrefab);
        }
    }
}