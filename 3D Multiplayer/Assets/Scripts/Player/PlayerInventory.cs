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
    [SerializeField] Transform itemTransform;            // first-person anchor (owner only)
    [SerializeField] Transform thirdPersonItemTransform; // world/body anchor others see
    [SerializeField] PlayerInput playerInput;
    [SerializeField] GameObject itemPrefab;

    WorldItem detectedItem;
    InventoryItem equippedItem;

    // Index of the item currently shown locally (-1 = none). Local-only bookkeeping.
    int shownIndex = -1;

    // Synced state: which item index is currently equipped (-1 = none)
    NetworkVariable<int> equippedIndex = new NetworkVariable<int>(-1);

    public override void OnNetworkSpawn()
    {
        // Every client keeps its own local visual inventory, so no IsOwner gate here.
        items = new List<InventoryItem>(maxItemCount);

        equippedIndex.OnValueChanged += OnEquippedIndexChanged;

        if (equippedIndex.Value >= 0)
        {
            ApplyEquip();
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
            WorldItem item = hit.collider.GetComponent<WorldItem>();
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

        WorldItem item = itemNetObj.GetComponent<WorldItem>();
        if (item == null) { return; }

        // Read the world item's data before despawning it.
        string itemName = item.itemName;
        int itemId = item.itemId;

        // Remove the world object (the real NetworkObject) from every client.
        itemNetObj.Despawn(true);

        // The new local visual will land at this index once the ClientRpc is processed.
        int newIndex = items.Count;

        // Tell every client to build its own local InventoryItem visual.
        AddItemClientRpc(itemName, itemId);

        // Equip the newly added item (server-authoritative write).
        equippedIndex.Value = newIndex;
    }

    // Runs on every client (and host). Builds the purely-local visual copy.
    [Rpc(SendTo.Everyone)]
    void AddItemClientRpc(string itemName, int itemId)
    {
        GameObject newItem = Instantiate(itemPrefab);

        InventoryItem newItemScript = newItem.GetComponent<InventoryItem>();
        newItemScript.itemName = itemName;
        newItemScript.itemId = itemId;
        newItemScript.gameObject.SetActive(false); // hidden until it's the equipped slot

        items.Add(newItemScript);

        // The equipped index may have arrived before this item existed, so reconcile now.
        ApplyEquip();
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
        if (index < 0 || index >= items.Count) { return; }

        equippedIndex.Value = index;
    }

    // Fires on ALL clients when the server changes equippedIndex.
    void OnEquippedIndexChanged(int previous, int current)
    {
        ApplyEquip();
        Debug.Log("EquippedIndex changed from " + previous + " to " + current);
    }

    // Local visual only. Idempotent and safe to call before the target item exists yet.
    void ApplyEquip()
    {
        int target = equippedIndex.Value;

        // Target not built locally yet; AddItemClientRpc will re-run this once it is.
        if (target < 0 || target >= items.Count) { return; }
        if (shownIndex == target) { return; }

        if (shownIndex >= 0 && shownIndex < items.Count && items[shownIndex] != null)
        {
            items[shownIndex].gameObject.SetActive(false);
        }

        // Owner sees the first-person viewmodel; everyone else sees the third-person anchor.
        Transform anchor = IsOwner ? itemTransform : thirdPersonItemTransform;
        items[target].transform.SetParent(anchor, false);
        items[target].transform.localPosition = Vector3.zero;
        items[target].transform.localRotation = Quaternion.identity;
        items[target].gameObject.SetActive(true);
        shownIndex = target;
    }
}