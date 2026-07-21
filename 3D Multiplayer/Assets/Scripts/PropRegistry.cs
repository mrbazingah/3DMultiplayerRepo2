using System.Collections.Generic;
using UnityEngine;

public class PropRegistry : MonoBehaviour
{
    [System.Serializable]
    public struct PropEntry
    {
        public Prop.PropType type;
        public GameObject prefab;
    }

    [SerializeField] List<PropEntry> entries = new List<PropEntry>();

    Dictionary<Prop.PropType, GameObject> propDict = new Dictionary<Prop.PropType, GameObject>();

    void Awake()
    {
        foreach (PropEntry entry in entries)
        {
            if (!propDict.TryAdd(entry.type, entry.prefab))
            {
                Debug.LogError("Duplicate prop ID in registry: " + entry.type, this);
            }
        }
    }

    public GameObject GetPrefab(Prop.PropType type)
    {
        return propDict[type];
    }
}