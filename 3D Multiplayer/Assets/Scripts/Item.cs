using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField] string itemName;
    [SerializeField] int value;
    [SerializeField] GameObject owner;
    
    bool isEquipped;

    public void PickUpItem(GameObject newOwner)
    {
        owner = newOwner;
    }

    public void SetEquipped(bool equipped)
    {
        isEquipped = equipped;
    }

    public bool IsUnclaimed()
    {
        bool canBeClaimed = owner == null;
        return canBeClaimed;
    }

    public bool IsEquipped()
    {
        return isEquipped;
    }
}
