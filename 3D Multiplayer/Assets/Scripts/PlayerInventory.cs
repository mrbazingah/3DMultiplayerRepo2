using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] List<InventoryItem> items;
    [SerializeField] int maxItemCount;
    [SerializeField] LayerMask itemLayerMask;
    [SerializeField] float detectionRange;
    [SerializeField] Camera cam;
    [SerializeField] Transform itemTransform; 
    [SerializeField] PlayerInput playerInput;
    [SerializeField] GameObject itemPrefab;

    Item detectedItem;
    InventoryItem equippedItem;

    // Synced state: which item index is currently equipped (-1 = none)
    NetworkVariable<int> equippedIndex = new NetworkVariable<int>(-1);

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { return; }

        items = new List<InventoryItem>(maxItemCount);

        equippedIndex.OnValueChanged += OnEquippedIndexChanged;
        if (equippedIndex.Value >= 0)
        {
            ApplyEquip(equippedIndex.Value, -1);
        }
        else if (items.Count == 1)
        {
            equippedIndex.Value = 0;
        }
    }

    public override void OnNetworkDespawn()
    {
        equippedIndex.OnValueChanged -= OnEquippedIndexChanged;
    }

    void Update()
    {
        if (!IsOwner) { return; } 
        DetectItem();
    }

    void DetectItem()
    {
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, detectionRange, itemLayerMask))
        {
            Item item = hit.collider.GetComponent<Item>();
            if (item != null)
            {
                detectedItem = item;
                return;
            }
        }

        detectedItem = null;
    }

    public void OnInteract(InputValue value)
    {
        if (!IsOwner || detectedItem == null) { return; }

        PickUpItemServerRpc(detectedItem.NetworkObject);
    }

    [Rpc(SendTo.Server)]
    void PickUpItemServerRpc(NetworkObjectReference itemRef)
    {
        if (!itemRef.TryGet(out NetworkObject itemNetObj)) { return; }

        Item item = itemNetObj.GetComponent<Item>();
        if (item == null) { return; }

        GameObject newItem = Instantiate(itemPrefab, itemTransform.position, Quaternion.identity);

        InventoryItem newItemScript = newItem.GetComponent<InventoryItem>();
        newItemScript.itemName = item.itemName;
        newItemScript.itemValue = item.itemValue;
        newItemScript.itemId = item.itemId;

        items.Add(newItemScript);

        itemNetObj.Despawn(true);

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == newItemScript)
            {
                equippedIndex.Value = i;
                break;
            }
        }
    }

    public void OnSwitchItem(InputValue value)
    {
        if (!IsOwner) { return; }

        InputControl control = playerInput.actions["SwitchItem"].activeControl;
        if (control != null && int.TryParse(control.name, out int keyNumber))
        {
            int index = keyNumber - 1;
            equippedIndex.Value = index;
        }
    }

    // Fires on ALL clients when the server changes equippedIndex
    void OnEquippedIndexChanged(int previous, int current)
    {
        if (!IsOwner) { return; }

        ApplyEquip(current, previous);
        Debug.Log("EquippedIndex changed from " + previous + " to " + current);
    }

    // Local visual only
    void ApplyEquip(int currentIndex, int previousIndex)
    {
        items[currentIndex].transform.SetParent(itemTransform);
        items[currentIndex].gameObject.SetActive(true);

        if (previousIndex >= 0)
        {
            items[previousIndex].gameObject.SetActive(false);
        }
    }
}