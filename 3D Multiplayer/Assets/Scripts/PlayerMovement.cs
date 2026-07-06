using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public static PlayerMovement instance { get; private set; }

    #region Variables
    [SerializeField] float moveSpeed;
    [SerializeField] float lookSpeed;
    [SerializeField] float lookXLimit;

    float rotationX; // Pitch (vertical)
    float rotationY; // Yaw (horizontal)
    
    Camera cam;

    Vector3 moveDirection;
    Vector2 movementInput;

    Rigidbody myRigidbody;
    CapsuleCollider myBodyCollider;
    #endregion

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    void Start()
    {
        myRigidbody = GetComponent<Rigidbody>();
        myBodyCollider = GetComponent<CapsuleCollider>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Get the main camera.
        cam = Camera.main;

        // Initialize yaw based on current rotation.
        rotationY = transform.rotation.eulerAngles.y;
    }

    void Update()
    {
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, rotationY, transform.rotation.eulerAngles.z);

        Movement();
    }

    #region Movement
    public void OnMove(InputValue value)
    {
        movementInput = value.Get<Vector2>();
    }

    void Movement()
    {
        // Since transform.rotation has already been updated,
        // transform.forward/right reflect the correct updated yaw.
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        moveDirection = (forward * movementInput.y) + (right * movementInput.x);
        myRigidbody.linearVelocity = new Vector3(moveDirection.x, myRigidbody.linearVelocity.y, moveDirection.z) * moveSpeed;
    }

    public void OnLook(InputValue value)
    {
        Vector2 lookInput = value.Get<Vector2>();
        rotationX -= lookInput.y * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        cam.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        rotationY += (lookInput.x * lookSpeed);
    }
    #endregion
}
