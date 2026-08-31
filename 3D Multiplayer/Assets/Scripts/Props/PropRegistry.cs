using System.Collections.Generic;
using UnityEngine;

public class PropRegistry : MonoBehaviour
{
    [System.Serializable]
    public struct PropEntry
    {
        public Prop.PropType type;
        public GameObject prefab;
        public int maxHealth;
        public float yOffset;
    }

    [SerializeField] List<PropEntry> entries = new List<PropEntry>();

    Dictionary<Prop.PropType, PropEntry> propDict = new Dictionary<Prop.PropType, PropEntry>();

    void Awake()
    {
        foreach (PropEntry entry in entries)
        {
            if (!propDict.TryAdd(entry.type, entry))
            {
                Debug.LogError("Duplicate prop ID in registry: " + entry.type, this);
            }
        }
    }

    public GameObject GetPrefab(Prop.PropType type)
    {
        if (propDict.TryGetValue(type, out PropEntry entry))
        {
            return entry.prefab;
        }

        Debug.LogError("Prop type not found in registry: " + type, this);
        return null;
    }

    public int GetHealth(Prop.PropType type)
    {
        if (propDict.TryGetValue(type, out PropEntry entry))
        {
            return entry.maxHealth;
        }

        Debug.LogError("Prop type not found in registry: " + type, this);
        return 0;
    }

    public float GetYOffset(Prop.PropType type)
    {
        if (propDict.TryGetValue(type, out PropEntry entry))
        {
            return entry.yOffset;
        }

        Debug.LogError("Prop type not found in registry: " + type, this);
        return 0;
    }
}