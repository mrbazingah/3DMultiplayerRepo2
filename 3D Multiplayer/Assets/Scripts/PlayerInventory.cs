using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] List<Item> items = new List<Item>();
    [SerializeField] LayerMask itemLayerMask;
    [SerializeField] float detectionRange;
    [SerializeField] Camera cam;
    [SerializeField] Transform itemTransform; // local hold point
    [SerializeField] PlayerInput playerInput;

    Item detectedItem;
    Item equippedItem;

    // Synced state: which item index is currently equipped (-1 = none)
    NetworkVariable<int> equippedIndex = new NetworkVariable<int>(-1);

    public override void OnNetworkSpawn()
    {
        equippedIndex.OnValueChanged += OnEquippedIndexChanged;
        // Apply initial state for late joiners
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
            if (item != null && item.IsUnclaimed())
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

        // Wrap the item's NetworkObject in a reference the RPC can serialize
        NetworkObjectReference itemRef = detectedItem.NetworkObject;
        PickUpItemServerRpc(itemRef);

        detectedItem = null;
    }

    [Rpc(SendTo.Server)]
    void PickUpItemServerRpc(NetworkObjectReference itemRef)
    {
        // Resolve the reference back into the actual NetworkObject on the server
        if (!itemRef.TryGet(out NetworkObject itemNetObj))
        {
            return; // item no longer exists / already despawned
        }

        Item item = itemNetObj.GetComponent<Item>();
        if (item == null || !item.IsUnclaimed())
        {
            return; // not a valid, claimable item
        }

        // --- Server-authoritative validation and state update goes here ---
        // e.g. distance check, mark claimed, add to this player's inventory,
        // then set equippedIndex.Value to trigger the synced visual equip.
    }

    public void OnSwitchItem(InputValue value)
    {
        if (!IsOwner) { return; }

        InputControl control = playerInput.actions["SwitchItem"].activeControl;
        if (control != null && int.TryParse(control.name, out int keyNumber))
        {
            int index = keyNumber - 1;
            SwitchItemServerRpc(index);
        }
    }

    [Rpc(SendTo.Server)]
    void SwitchItemServerRpc(int index)
    {
        if (index >= 0 && index < items.Count)
        {
            equippedIndex.Value = index; // server-authoritative, auto-syncs
        }
    }

    // Fires on ALL clients when the server changes equippedIndex
    void OnEquippedIndexChanged(int previous, int current)
    {
        ApplyEquip(current);
    }

    // Purely visual - runs on every client, uses normal SetParent
    void ApplyEquip(int index)
    {
        if (equippedItem != null)
        {
            equippedItem.SetEquipped(false);
            equippedItem.gameObject.SetActive(false);
        }

        if (index < 0 || index >= items.Count) { return; }

        equippedItem = items[index];
        equippedItem.SetEquipped(true);
        equippedItem.gameObject.SetActive(true);

        equippedItem.transform.SetParent(itemTransform);   
        equippedItem.transform.localPosition = Vector3.zero;
        equippedItem.transform.localRotation = Quaternion.identity;
    }
}