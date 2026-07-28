using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Walking")]
    [SerializeField] float walkSpeed;
    [SerializeField] float runSpeed;
    [Header("Jumping")]
    [SerializeField] float jumpForce;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float rayDistance;
    [Header("Camera")]
    [SerializeField] float lookSpeed;
    [SerializeField] float lookXLimit;
    [SerializeField] Camera firstPersonCam;
    [SerializeField] Camera thridPersonCam;
    [SerializeField] Transform camPivot;
    [SerializeField] AudioListener playerAudioListener;
    [Space]
    [SerializeField] PlayerInput playerInput;
    [SerializeField] GameManager.Team playerTeam;

    bool isRunning;

    float currentSpeed;
    float rotationX; 
    float rotationY;

    Vector3 moveDirection;
    Vector2 movementInput;
    Vector2 lookInput;

    Rigidbody myRigidbody;
    Animator myAnimator;
    GameManager gameManager;
    Collider playerCollider;
    
    public override void OnNetworkSpawn()
    {
        myRigidbody = GetComponent<Rigidbody>();
        myAnimator = GetComponentInChildren<Animator>();
        gameManager = FindFirstObjectByType<GameManager>();
        playerCollider = GetComponent<Collider>();

        if (!IsOwner)
        {
            myRigidbody.isKinematic = true;

            thridPersonCam.gameObject.SetActive(false);

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

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerTeam == GameManager.Team.Hunters)
        {
            firstPersonCam.enabled = true;
            thridPersonCam.enabled = false;

            camPivot = firstPersonCam.transform;
        }
        else
        {
            firstPersonCam.enabled = false;
            thridPersonCam.enabled = true;
        }

        rotationY = transform.rotation.eulerAngles.y;
        currentSpeed = walkSpeed;

        gameManager.AssignPlayer(transform);
    }

    void FixedUpdate()
    {
        Movement();

        myRigidbody.MoveRotation(Quaternion.Euler(0, rotationY, 0));
    }

    public void OnMove(InputValue value)
    {
        if (!IsOwner) { return; }

        movementInput = value.Get<Vector2>();
    }

    // Pass through input
    public void OnRun(InputValue value)
    {
        if (!IsOwner) { return; }

        isRunning = value.isPressed;
        currentSpeed = isRunning ? runSpeed : walkSpeed;
    }

    void Movement()
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        moveDirection = (forward * movementInput.y) + (right * movementInput.x);

        //myAnimator.SetBool("isWalking", moveDirection.magnitude > 0);

        Vector3 targetVelocity = moveDirection * currentSpeed;
        myRigidbody.linearVelocity = new Vector3(targetVelocity.x, myRigidbody.linearVelocity.y, targetVelocity.z);
    }

    public void OnJump(InputValue value)
    {
        if (!IsOwner || !IsGrounded()) { return; }

        myRigidbody.AddForce(new Vector3(0, jumpForce, 0), ForceMode.Impulse);
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, rayDistance, groundLayer);
    }

    public void OnLook(InputValue value)
    {
        if (!IsOwner) { return; }

        lookInput = value.Get<Vector2>();
    }

    void LateUpdate()
    {
        rotationX -= lookInput.y * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        camPivot.localRotation = Quaternion.Euler(rotationX, 0, 0);
        rotationY += (lookInput.x * lookSpeed);

        lookInput = Vector2.zero;
    }

    public Quaternion GetPlayerRotation()
    {
        return transform.rotation;
    }
}
