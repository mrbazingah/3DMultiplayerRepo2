using Unity.Netcode;
using UnityEngine;

public class WorldItem : NetworkBehaviour
{
    [field: SerializeField] public string itemName { get; private set; }
    [field:SerializeField] public int itemId { get; private set; }
    [field:SerializeField] public ItemDefinition definition { get; private set; }
}
