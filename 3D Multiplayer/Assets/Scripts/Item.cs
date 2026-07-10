using Unity.Netcode;
using UnityEngine;

public class Item : NetworkBehaviour
{
    [field: SerializeField] public string itemName { get; private set; }
    [field:SerializeField] public int itemId { get; private set; }
}
