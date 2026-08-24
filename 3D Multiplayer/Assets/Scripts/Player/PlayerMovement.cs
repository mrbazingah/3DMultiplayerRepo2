using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Walking")]
    [SerializeField] float walkSpeed;
    [SerializeField] float runSpeed;
    [SerializeField] float crouchSpeed;
    [SerializeField] float crouchHeight;
    [SerializeField] float defaultHeight;

    [Header("Jumping")]
    [SerializeField] float jumpForce;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float rayDistance;
    [SerializeField] float gravity;
    [SerializeField] float coyoteTime;

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
    bool isCrouching;

    float currentSpeed;
    float rotationX;
    float rotationY;
    float coyoteCounter;

    Vector3 moveDirection;
    Vector2 movementInput;
    Vector2 lookInput;

    Camera currentCam;

    Rigidbody myRigidbody;
    Animator myAnimator;
    Collider myCollider;

    public override void OnNetworkSpawn()
    {
        myRigidbody = GetComponent<Rigidbody>();
        myAnimator = GetComponentInChildren<Animator>();
        myCollider = GetComponent<Collider>();

        // Registers before the owner check since the server runs this for every player object and not just the ones it owns
        if (IsServer && GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(this);
        }
        else
        {
            Debug.LogWarning("GameManager not found, player not registered");
        }

        // If isn't owner, disable camera, input, audiolistener, rigidbody physics and this
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
        OnTeamChanged(playerTeam.Value, playerTeam.Value); // Applies the current value since OnValueChanged only fires on a change

        rotationY = transform.rotation.eulerAngles.y;
        currentSpeed = walkSpeed;
    }

    public void SetPlayerTeam(GameManager.Team newTeam)
    {
        // Only the server can write the team, the change replicates to the clients through OnTeamChanged()
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
        bool isHunter = team == GameManager.Team.Hunters;
        firstPersonCam.enabled = isHunter;
        thridPersonCam.enabled = !isHunter;

        // Keeps the existing pivot for props, otherwise the third person camera would pivot around itself instead of the player
        camPivot = isHunter ? firstPersonCam.transform : camPivot;
        currentCam = camPivot.GetComponent<Camera>();
    }

    public void TeleportTo(Vector3 pos)
    {
        // Server owns the position so the move happens here
        if (!IsServer) { return; }

        if (myRigidbody != null)
        {
            // Clears velocity so the player doesn't keep moving after being teleported
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
        ApplyGravity();
        CoyoteTime();

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
        if (!IsOwner || isCrouching) { return; }

        isRunning = value.isPressed;
        currentSpeed = isRunning ? runSpeed : walkSpeed;
    }

    void Movement()
    {
        // Uses the player's own forward and right so movement follows where they're facing
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        moveDirection = (forward * movementInput.y) + (right * movementInput.x);

        //myAnimator.SetBool("isWalking", moveDirection.magnitude > 0);

        Vector3 targetVelocity = moveDirection * currentSpeed;

        // Keeps the current y velocity so gravity and jumping aren't overwritten
        myRigidbody.linearVelocity = new Vector3(targetVelocity.x, myRigidbody.linearVelocity.y, targetVelocity.z);
    }

    // Pass through input
    public void OnCrouch(InputValue value)
    {
        if (!IsOwner) { return; }

        isCrouching = value.isPressed;
        isRunning = false;

        currentSpeed = isCrouching ? crouchSpeed : walkSpeed;

        if (playerTeam.Value == GameManager.Team.Hunters)
        {
            CapsuleCollider collider = GetComponent<CapsuleCollider>();
            collider.height = isCrouching ? crouchHeight : defaultHeight;
        }
    }

    void ApplyGravity()
    {
        float velocity = myRigidbody.linearVelocity.y;
        if (IsGrounded() && (velocity < 0 || velocity > 0))
        {
            velocity = 0f;
        }
        else
        {
            velocity = gravity * Time.fixedDeltaTime;
        }

        myRigidbody.linearVelocity -= new Vector3(0, velocity, 0);
    }

    void CoyoteTime()
    {
        if (IsGrounded())
        {
            // Refills the timer while grounded so the player always has the full window when they walk off
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter -= Time.fixedDeltaTime;
        }
    }

    public void OnJump(InputValue value)
    {
        if (!IsOwner) { return; }

        // Allows the jump if the player left the ground within the coyote window
        if (!value.isPressed || coyoteCounter <= 0) { return; }

        // Clears any downward velocity first so a jump taken late in the coyote window reaches full height
        myRigidbody.linearVelocity = new Vector3(myRigidbody.linearVelocity.x, 0, myRigidbody.linearVelocity.z);
        myRigidbody.AddForce(new Vector3(0, jumpForce, 0), ForceMode.Impulse);

        // Stops the same window being used for a second jump before landing
        coyoteCounter = 0;
    }

    bool IsGrounded()
    {
        if (myCollider == null) return false;

        // Casts a ray down from the bottom of the current collider to check for ground
        Vector3 colOrigin = myCollider.bounds.center;
        colOrigin.y = myCollider.bounds.min.y + 0.1f; // Make sure ray sits above ground, otherwise it will go past the ground and return false

        return Physics.Raycast(colOrigin, Vector3.down, rayDistance, groundLayer);
    }

    public void SetPlayerCollider(Collider newCol)
    {
        // Called by PlayerModelManager so the ground check uses the prop model's collider after a swap
        myCollider = newCol;
    }

    public void OnLook(InputValue value)
    {
        if (!IsOwner) { return; }

        lookInput = value.Get<Vector2>();
    }

    void LateUpdate()
    {
        // Pivot handles looking up and down and gets clamped, the body handles looking left and right in FixedUpdate()
        rotationX -= lookInput.y * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        camPivot.localRotation = Quaternion.Euler(rotationX, 0, 0);
        rotationY += (lookInput.x * lookSpeed);

        // Resets input so the camera stops moving when there's no new input this frame
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

    // Change to in game menu once lobbies are implemented  
    public void OnStartGame(InputValue value)
    {
        if (!IsOwner || !IsServer) { return; }

        Debug.Log("Game Started");

        GameManager.Instance.StartGame();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterPlayer(this);
        }

        playerTeam.OnValueChanged -= OnTeamChanged;
    }
}