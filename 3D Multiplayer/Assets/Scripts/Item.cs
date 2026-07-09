using UnityEngine;

public class Item : MonoBehaviour
{
    [field: SerializeField] public string itemName { get; private set; }
    [field:SerializeField] public int value { get; private set; }
}
