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

    //public NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();

    float rotationX; // Pitch (vertical)
    float rotationY; // Yaw (horizontal)

    Vector3 moveDirection;
    Vector2 movementInput;

    Camera cam;

    Rigidbody myRigidbody;
    CapsuleCollider myBodyCollider;
    
    public override void OnNetworkSpawn()
    {
        //Position.OnValueChanged += OnStateChanged;

        myRigidbody = GetComponent<Rigidbody>();
        myBodyCollider = GetComponent<CapsuleCollider>();

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
    }

    /*
    public void OnStateChanged(Vector3 previous, Vector3 current)
    {
        if (!IsOwner)
        {
            myRigidbody.MovePosition(Position.Value);
        }
    }
    
    [Rpc(SendTo.Server)]
    void SubmitPositionRequestServerRpc(Vector3 clientPosition, RpcParams rpcParams = default)
    {
        Position.Value = clientPosition;
    }
    */

    void Update()
    {
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, rotationY, transform.rotation.eulerAngles.z);

        Movement();

        /*
        if (IsOwner)
        {
            SubmitPositionRequestServerRpc(transform.position);
        }
        */
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
