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
    [SerializeField] bool canSwap;

    NetworkVariable<Prop.PropType> currentProp = new NetworkVariable<Prop.PropType>();

    NetworkVariable<bool> lockRotation = new NetworkVariable<bool>();
    NetworkVariable<Quaternion> savedRotation = new NetworkVariable<Quaternion>();

    PropRegistry propRegistry;
    PlayerMovement myMovement;
    Collider myCollider;
    Rigidbody myRigidbody;

    public override void OnNetworkSpawn()
    {
        propRegistry = FindFirstObjectByType<PropRegistry>();
        myMovement = GetComponent<PlayerMovement>();
        myCollider = GetComponent<Collider>();
        myRigidbody = GetComponent<Rigidbody>();

        // Subscribes to network variable changes
        currentProp.OnValueChanged += OnCurrentPropChanged;

        // Follows the team from PlayerMovement so canSwap stays correct on every client, and applies the current value for players who spawn with a team already set
        NetworkVariable<GameManager.Team> team = myMovement.GetPlayerTeam();
        team.OnValueChanged += OnTeamChanged;
        SetCanSwap(team.Value);

        if (IsOwner)
        {
            // Makes sure local player model has correct layer
            SetLayerRecursively(defaultVisuals, LayerMask.NameToLayer("Player Visuals"));
        }
    }

    // Updates canSwap when team changes
    void OnTeamChanged(GameManager.Team previousTeam, GameManager.Team newTeam)
    {
        SetCanSwap(newTeam);
    }

    public void SetCanSwap(GameManager.Team team)
    {
        canSwap = team == GameManager.Team.Props;
    }

    void Update()
    {
        if (!IsOwner || !canSwap) { return; }
        DetectItem();
    }

    void DetectItem()
    {
        // Casts a ray from the camera to detect props in front of the player
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
        if (!IsOwner || detectedProp == null || !canSwap) { return; }

        SwapModelServerRpc(detectedProp.propType);

        Debug.Log("Interacted with prop");
    }

    // Server RPC to swap the player's model which triggers OnCurrentPropChanged() to update the model on all clients
    [Rpc(SendTo.Server)]
    void SwapModelServerRpc(Prop.PropType propType)
    {
        currentProp.Value = propType;
    }

    // Updates player model on each client when variable changes
    void OnCurrentPropChanged(Prop.PropType oldValue, Prop.PropType newValue)
    {
        ApplyPropModel();
        Debug.Log("Prop changed from " + oldValue + " to " + newValue);
    }

    void ApplyPropModel()
    {
        Debug.Log("Applied prop model");

        // Initializes prop model for player
        GameObject spawnedProp = null;

        if (currentProp.Value != Prop.PropType.None)
        {
            // Gets the prop from registry and spawns it as a child of the player
            GameObject propPrefab = propRegistry.GetPrefab(currentProp.Value);
            spawnedProp = Instantiate(propPrefab, transform);

            defaultVisuals.SetActive(false);
            myCollider.enabled = false;
        }
        else
        {
            defaultVisuals.SetActive(true);
            myCollider.enabled = true;
        }

        // If the player has a prop assigned and the prop model isn't null then use the model's collider, otherwise use the player's collider
        Collider spawnedCollider = currentProp.Value != Prop.PropType.None && spawnedProp != null ? spawnedProp.GetComponent<Collider>() : myCollider;
        Collider previousCollider = currentPropModel != null ? currentPropModel.GetComponent<Collider>() : myCollider;

        AlignPlayerToGround(previousCollider, spawnedCollider);

        if (currentPropModel != null)
        {
            currentPropModel.SetActive(false);
            Destroy(currentPropModel);
        }

        if (spawnedProp != null)
        {
            // Applies new prop model, sets layer and assigns new collider
            currentPropModel = spawnedProp;
            SetLayerRecursively(currentPropModel, LayerMask.NameToLayer("Player Prop"));

            Collider newCol = GetCurrentModelCollider();
            myMovement.SetPlayerCollider(newCol);
        }
    }

    // Sets layer on each child of an object
    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // Uses the previous and current model's collider's lowest point to calculate the distance between them and moves the player to allign to the ground
    void AlignPlayerToGround(Collider previousCollider, Collider newCollider)
    {
        float previousLowPoint = previousCollider.bounds.min.y;
        float newLowPoint = newCollider.bounds.min.y;

        float distance = newLowPoint - previousLowPoint;

        myRigidbody.position += new Vector3(0, distance, 0);
    }

    public void OnDiscard(InputValue value)
    {
        if (!IsOwner || !canSwap) { return; }

        SwapModelServerRpc(Prop.PropType.None);

        Debug.Log("Discarded prop");
    }

    public void OnLock(InputValue value)
    {
        if (!IsOwner || !canSwap) { return; }

        // Assigns prop's model or player's model before locking it
        Transform modelTransform = currentPropModel != null ? currentPropModel.transform : defaultVisuals.transform;
        ToggleLockServerRpc(modelTransform.rotation);
    }

    // Server RPC to toggle lock rotation and save the current rotation if locking
    [Rpc(SendTo.Server)]
    void ToggleLockServerRpc(Quaternion currentRotation)
    {
        lockRotation.Value = !lockRotation.Value;

        if (lockRotation.Value)
        {
            savedRotation.Value = currentRotation;
        }
    }

    // Runs on every client and not just the owner, otherwise other players wouldn't see the locked rotation or the snap back
    void LateUpdate()
    {
        LockRotation();
    }

    // Only reads the network variables and applies them to the local model, all writes go through ToggleLockServerRpc()
    void LockRotation()
    {
        // Uses the rotation assigned when locked or player's current rotation
        Quaternion targetRotation = lockRotation.Value ? savedRotation.Value : myMovement.GetPlayerRotation();

        if (currentPropModel != null)
        {
            currentPropModel.transform.rotation = targetRotation;
        }
        else
        {
            defaultVisuals.transform.rotation = targetRotation;
        }
    }

    public override void OnNetworkDespawn()
    {
        currentProp.OnValueChanged -= OnCurrentPropChanged;
        myMovement.GetPlayerTeam().OnValueChanged -= OnTeamChanged;
    }

    public Collider GetCurrentModelCollider()
    {
        // Returns either prop model's collider or player model's collider
        Collider cmCol = currentPropModel != null ? currentPropModel.GetComponent<Collider>() : myMovement.GetComponent<Collider>();
        return cmCol;
    }
}