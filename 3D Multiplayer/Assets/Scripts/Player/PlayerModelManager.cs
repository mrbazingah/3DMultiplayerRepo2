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

    PropRegistry propRegistry;

    public override void OnNetworkSpawn()
    {
        propRegistry = FindFirstObjectByType<PropRegistry>();

        currentProp.OnValueChanged += OnCurrentPropChanged;
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

        Debug.Log("Server Rpc triggered");
    }

    void OnCurrentPropChanged(Prop.PropType oldValue, Prop.PropType newValue)
    {
        ApplyPropModel();
        Debug.Log("Prop changed from " + oldValue + " to " + newValue);
    }

    void ApplyPropModel()
    {
        Debug.Log("Applied prop model");

        defaultVisuals.SetActive(false);
        GameObject propPrefab = propRegistry.GetPrefab(currentProp.Value);
        GameObject spawnedProp = Instantiate(propPrefab, transform);
        
        if (currentPropModel != null)
        {
            currentPropModel.SetActive(false);
            Destroy(currentPropModel);
        }

        currentPropModel = spawnedProp;
    }

    public override void OnNetworkDespawn()
    {
        currentProp.OnValueChanged -= OnCurrentPropChanged;
    }
}
