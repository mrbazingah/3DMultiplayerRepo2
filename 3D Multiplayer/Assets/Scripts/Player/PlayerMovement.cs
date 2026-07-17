using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] float moveSpeed;
    [SerializeField] float lookSpeed;
    [SerializeField] float lookXLimit;
    [SerializeField] Camera playerCam;
    [SerializeField] AudioListener playerAudioListener;
    [SerializeField] PlayerInput playerInput;

    float rotationX; 
    float rotationY; 

    Vector3 moveDirection;
    Vector2 movementInput;

    Camera cam;

    Rigidbody myRigidbody;
    Animator myAnimator;
    ChunkManager chunkManager;
    GameManager gameManager;
    
    public override void OnNetworkSpawn()
    {
        myRigidbody = GetComponent<Rigidbody>();
        myAnimator = GetComponentInChildren<Animator>();
        chunkManager = FindFirstObjectByType<ChunkManager>();
        gameManager = FindFirstObjectByType<GameManager>();

        if (!IsOwner)
        {
            myRigidbody.isKinematic = true;

            playerCam.gameObject.SetActive(false);

            if (playerAudioListener != null)
            {
                playerAudioListener.enabled = false;
            }

            if (playerInput != null)
            {
                playerInput.enabled = false;
            }

            enabled = false;
            return;
        }

        myRigidbody.isKinematic = false;

        cam = playerCam;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        rotationY = transform.rotation.eulerAngles.y;

        SetLayerRecursively(gameObject, LayerMask.NameToLayer("Self Visuals"));

        chunkManager.AssignPlayer(transform);
        gameManager.AssignPlayer(transform);
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    void Update()
    {
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, rotationY, transform.rotation.eulerAngles.z);

        Movement();
    }

    public void OnMove(InputValue value)
    {
        if (!IsOwner) { return; }

        movementInput = value.Get<Vector2>();
    }

    void Movement()
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        moveDirection = (forward * movementInput.y) + (right * movementInput.x);

        myAnimator.SetBool("isWalking", moveDirection.magnitude > 0);

        Vector3 targetVelocity = moveDirection * moveSpeed;
        myRigidbody.linearVelocity = new Vector3(targetVelocity.x, myRigidbody.linearVelocity.y, targetVelocity.z);
    }

    public void OnLook(InputValue value)
    {
        if (!IsOwner) { return; }

        Vector2 lookInput = value.Get<Vector2>();
        rotationX -= lookInput.y * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        cam.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        rotationY += (lookInput.x * lookSpeed);
    }
}
