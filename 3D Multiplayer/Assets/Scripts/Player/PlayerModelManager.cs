using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerModelManager : NetworkBehaviour
{
    [SerializeField] GameObject defaultVisuals;
    [SerializeField] float detectionRange;
    [SerializeField] LayerMask propLayer;
    [SerializeField] Camera cam;
    [SerializeField] Prop detectedProp;
    [SerializeField] GameObject currentPropModel;

    NetworkVariable<Prop.PropType> currentProp = new NetworkVariable<Prop.PropType>();

    NetworkVariable<bool> lockRotation = new NetworkVariable<bool>();
    NetworkVariable<Quaternion> savedRotation = new NetworkVariable<Quaternion>();

    PropRegistry propRegistry;
    PlayerMovement playerMovement;
    CapsuleCollider playerCollider;
    Rigidbody myRigidbody;

    public override void OnNetworkSpawn()
    {
        propRegistry = FindFirstObjectByType<PropRegistry>();
        playerMovement = FindFirstObjectByType<PlayerMovement>();
        playerCollider = GetComponent<CapsuleCollider>();
        myRigidbody = GetComponent<Rigidbody>();

        currentProp.OnValueChanged += OnCurrentPropChanged;

        SetLayerRecursively(defaultVisuals, LayerMask.NameToLayer("Player Visuals"));
    }

    void Update()
    {
        if (!IsOwner) { return; }
        DetectItem();
    }

    void DetectItem()
    {
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, detectionRange, propLayer))
        {
            Prop prop = hit.collider.GetComponent<Prop>();
            if (prop != null)
            {
                detectedProp = prop;
                Debug.Log("Detected prop");
                return;
            }
        }

        detectedProp = null;
    }

    public void OnInteract(InputValue value)
    {
        if (!IsOwner || detectedProp == null) { return; }

        SwapModelServerRpc(detectedProp.propType);

        Debug.Log("Interacted with prop");
    }

    [Rpc(SendTo.Server)]
    void SwapModelServerRpc(Prop.PropType propType)
    {
        currentProp.Value = propType;
    }

    void OnCurrentPropChanged(Prop.PropType oldValue, Prop.PropType newValue)
    {
        ApplyPropModel();
        Debug.Log("Prop changed from " + oldValue + " to " + newValue);
    }

    void ApplyPropModel()
    {
        Debug.Log("Applied prop model");

        GameObject spawnedProp = null;

        if (currentProp.Value != Prop.PropType.None)
        {
            GameObject propPrefab = propRegistry.GetPrefab(currentProp.Value);
            spawnedProp = Instantiate(propPrefab, transform);

            defaultVisuals.SetActive(false);
            playerCollider.enabled = false;
        }
        else
        {
            defaultVisuals.SetActive(true);
            playerCollider.enabled = true;
        }

        Collider spawnedCollider = spawnedProp != null ? spawnedProp.GetComponent<Collider>() : playerCollider;
        Collider previousCollider = currentPropModel != null ? currentPropModel.GetComponent<Collider>() : playerCollider;
        AlignPropToGround(spawnedCollider, previousCollider);

        if (currentPropModel != null)
        {
            currentPropModel.SetActive(false);
            Destroy(currentPropModel);
        }

        if (spawnedProp != null)
        {
            currentPropModel = spawnedProp;
            SetLayerRecursively(currentPropModel, LayerMask.NameToLayer("Player Prop"));
        }
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    void AlignPropToGround(Collider propCollider1, Collider propCollider2)
    {
        float propLowPoint = propCollider1.bounds.min.y;
        float otherLowPoint = propCollider2.bounds.min.y;

        float distance = otherLowPoint - propLowPoint;

        myRigidbody.position += new Vector3(0, distance, 0);
    }

    public void OnDiscard(InputValue value)
    {
        if (!IsOwner) { return; }

        currentProp.Value = Prop.PropType.None;

        Debug.Log("Discarded prop");
    }

    public void OnLock(InputValue value)
    {
        if (!IsOwner) { return; }

        lockRotation.Value = !lockRotation.Value;

        savedRotation.Value = currentPropModel != null ? currentPropModel.transform.rotation : defaultVisuals.transform.rotation;
    }

    void LateUpdate()
    {
        LockRotation();
    }

    void LockRotation()
    {
        savedRotation.Value = lockRotation.Value ? savedRotation.Value : playerMovement.GetPlayerRotation();

        if (currentPropModel != null)
        {
            currentPropModel.transform.rotation = savedRotation.Value;
        }
        else
        {
            defaultVisuals.transform.rotation = savedRotation.Value;
        }
    }

    public override void OnNetworkDespawn()
    {
        currentProp.OnValueChanged -= OnCurrentPropChanged;
    }
}
