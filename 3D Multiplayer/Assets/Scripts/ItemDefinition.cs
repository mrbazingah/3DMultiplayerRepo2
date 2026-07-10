using UnityEngine;

[CreateAssetMenu(fileName = "ItemDefinition", menuName = "Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    public int itemId;
    public string itemName;
    public int itemValue;
    public GameObject modelPrefab;
}