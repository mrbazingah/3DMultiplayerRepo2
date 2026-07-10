using UnityEngine;

public class InventoryItem : MonoBehaviour
{
    [field: SerializeField] public string itemName { get; set; }
    [field:SerializeField] public int itemId { get; set; }
}
