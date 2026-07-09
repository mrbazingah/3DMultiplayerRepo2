using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] List<Item> items;
    [SerializeField] int maxItemCount;
    [SerializeField] LayerMask itemLayerMask;
    [SerializeField] float detectionRange;
    [SerializeField] Camera cam;
    [SerializeField] Transform itemTransform; 
    [SerializeField] PlayerInput playerInput;
    [SerializeField] GameObject itemPrefab;

    Item detectedItem;
    Item equippedItem;

    // Synced state: which item index is currently equipped (-1 = none)
    NetworkVariable<int> equippedIndex = new NetworkVariable<int>(-1);

    public override void OnNetworkSpawn()
    {
        items = new List<Item>(maxItemCount);

        equippedIndex.OnValueChanged += OnEquippedIndexChanged;
        if (equippedIndex.Value >= 0)
        {
            ApplyEquip(equippedIndex.Value);
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
        newItem.GetComponent<NetworkObject>().Spawn(true);
        newItem.SetActive(false);

        items.Add(newItem.GetComponent<Item>());

        itemNetObj.Despawn(true);
    }

    public void OnSwitchItem(InputValue value)
    {
        if (!IsOwner) { return; }

        InputControl control = playerInput.actions["SwitchItem"].activeControl;
        if (control != null && int.TryParse(control.name, out int keyNumber))
        {
            int index = keyNumber - 1;
            
        }
    }

    // Fires on ALL clients when the server changes equippedIndex
    void OnEquippedIndexChanged(int previous, int current)
    {
        ApplyEquip(current);
    }

    // Local visual only
    void ApplyEquip(int index)
    {
        items[index].transform.SetParent(itemTransform);
        items[index].gameObject.SetActive(true);
    }
}