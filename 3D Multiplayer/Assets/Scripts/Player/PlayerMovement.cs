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
    [SerializeField] NetworkVariable<GameManager.Team> playerTeam;

    bool isRunning;

    float currentSpeed;
    float rotationX;
    float rotationY;

    Vector3 moveDirection;
    Vector2 movementInput;
    Vector2 lookInput;

    Camera currentCam;

    Rigidbody myRigidbody;
    Animator myAnimator;
    GameManager gameManager;
    Collider myCollider;

    public override void OnNetworkSpawn()
    {
        myRigidbody = GetComponent<Rigidbody>();
        myAnimator = GetComponentInChildren<Animator>();
        gameManager = FindFirstObjectByType<GameManager>();
        myCollider = GetComponent<Collider>();

        // Runs on the server for every player object, owned or not
        if (IsServer && gameManager != null)
        {
            gameManager.RegisterPlayer(this);
        }

        if (!IsOwner)
        {
            myRigidbody.isKinematic = true;

            thridPersonCam.gameObject.SetActive(false);
            firstPersonCam.gameObject.SetActive(false);

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

        playerTeam.OnValueChanged += OnTeamChanged;
        OnTeamChanged(playerTeam.Value, playerTeam.Value);

        rotationY = transform.rotation.eulerAngles.y;
        currentSpeed = walkSpeed;
    }

    public void SetPlayerTeam(GameManager.Team newTeam)
    {
        if (!IsServer) { return; }
        playerTeam.Value = newTeam;
    }

    void OnTeamChanged(GameManager.Team previous, GameManager.Team current)
    {
        if (!IsOwner) { return; }
        ApplyTeamVisuals(current);
    }

    void ApplyTeamVisuals(GameManager.Team team)
    {
        bool hunter = team == GameManager.Team.Hunters;
        firstPersonCam.enabled = hunter;
        thridPersonCam.enabled = !hunter;
        camPivot = hunter ? firstPersonCam.transform : camPivot;
        currentCam = camPivot.GetComponent<Camera>();
    }

    // Server-authoritative: the server owns position, so this runs there directly
    public void TeleportTo(Vector3 pos)
    {
        if (!IsServer) { return; }

        if (myRigidbody != null)
        {
            myRigidbody.linearVelocity = Vector3.zero;
            myRigidbody.angularVelocity = Vector3.zero;
            myRigidbody.position = pos;
        }
        else
        {
            transform.position = pos;
        }
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
        if (myCollider == null) return false;

        Vector3 colOrigin = myCollider.bounds.center;
        colOrigin.y = myCollider.bounds.min.y + 0.1f; // Make sure ray sits above ground

        return Physics.Raycast(colOrigin, Vector3.down, rayDistance, groundLayer);
    }

    public void SetPlayerCollider(Collider newCol)
    {
        myCollider = newCol;
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

    public Camera GetCurrentCam()
    {
        return currentCam;
    }

    public NetworkVariable<GameManager.Team> GetPlayerTeam()
    {
        return playerTeam;
    }

    public void OnStartGame(InputValue value)
    {
        if (!IsOwner || !IsServer) { return; }

        gameManager.StartGame();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && gameManager != null)
        {
            gameManager.UnregisterPlayer(this);
        }

        playerTeam.OnValueChanged -= OnTeamChanged;
    }
}