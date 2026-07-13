using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Items/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemDefinition> definitions;
    public ItemDefinition Get(int id) => definitions.Find(d => d.itemId == id);
}