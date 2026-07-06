using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] List<Item> items = new List<Item>();
    [SerializeField] LayerMask itemLayerMask;
    [SerializeField] float detectionRange;
    [SerializeField] Item detectedItem;
    [SerializeField] Camera cam;
    [SerializeField] Transform itemTransform;
    [SerializeField] Item equippedItem;
    [SerializeField] PlayerInput playerInput;

    void Update()
    {
        DetectItem();
    }

    void DetectItem()
    {
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, detectionRange, itemLayerMask))
        {
            Item item = hit.collider.GetComponent<Item>();
            if (item != null && item.IsUnclaimed())
            {
                detectedItem = item;
                Debug.Log("Detected item");
            }
        }
        else
        {
            Debug.Log("No item detected");
        }
    }

    public void OnInteract(InputValue value)
    {
        detectedItem.PickUpItem(gameObject);
        items.Add(detectedItem);
        detectedItem.transform.SetParent(itemTransform);

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == detectedItem)
            {
                EquipItem(i);
                break;
            }
        }

        detectedItem = null;

        Debug.Log("Picked up item");
    }

    void EquipItem(int index)
    {
        if (equippedItem != null)
        {
            equippedItem.SetEquipped(false);
            equippedItem.gameObject.SetActive(false);
        }

        equippedItem = items[index];
        equippedItem.SetEquipped(true);
        equippedItem.gameObject.SetActive(true);
        equippedItem.transform.localPosition = Vector3.zero;
    }

    public void OnSwitchItem(InputValue value)
    {
        InputControl control = playerInput.actions["SwitchItem"].activeControl;

        if (control != null && int.TryParse(control.name, out int keyNumber))
        {
            int index = keyNumber - 1;
            if (index >= 0 && index < items.Count)
            {
                EquipItem(index);
            }
        }
    }
}
