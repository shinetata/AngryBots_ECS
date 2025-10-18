using PGD;
using UnityEngine;

namespace PGD
{
    public struct GoLink : IComponent
    {
        public UnityEngine.Transform transform;
        public GameObject gameObject;
        public int agentId; // 用于标记entity在对象池中的槽位
        public string name;
        public bool isPrefab; // 用于标记Entity是否为prefab生成的模板Entity

        public override string ToString() => gameObject == null ? "null" : gameObject.ToString();

        public GoLink(
            GameObject gameObject, 
            int agentId = 0, 
            string name = "",
            bool isPrefab = true)
        {
            this.gameObject = gameObject;
            transform       = gameObject.transform;
            this.agentId = agentId;
            this.name = name;
            this.isPrefab = isPrefab;
        }
    }
}