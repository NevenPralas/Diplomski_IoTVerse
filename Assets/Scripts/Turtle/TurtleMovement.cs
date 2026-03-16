using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
public class TurtleMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float runMultiplier = 1.8f;
    [SerializeField] private float backwardMultiplier = 2f;
    [SerializeField] private float rotationSpeed = 120f;

    private Animator animator;
    private Rigidbody rb;

    private float forwardInput;
    private float turnInput;
    private bool isRunning;

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Update()
    {
        ReadInput();
        HandleAnimation();
    }

    void FixedUpdate()
    {
        HandleRotation();
        HandleMovement();
    }

    void ReadInput()
    {
        forwardInput = 0f;
        turnInput = 0f;

        if (Keyboard.current.wKey.isPressed)
            forwardInput = 1f;
        else if (Keyboard.current.sKey.isPressed)
            forwardInput = -1f;

        if (Keyboard.current.aKey.isPressed)
            turnInput = -1f;
        else if (Keyboard.current.dKey.isPressed)
            turnInput = 1f;

        isRunning = Keyboard.current.leftShiftKey.isPressed;
    }

    void HandleRotation()
    {
        float turn = turnInput * rotationSpeed * Time.fixedDeltaTime;
        Quaternion deltaRotation = Quaternion.Euler(0f, turn, 0f);
        rb.MoveRotation(rb.rotation * deltaRotation);
    }

    void HandleMovement()
    {
        float speed = moveSpeed * (isRunning ? runMultiplier : 1f);

        if (forwardInput < 0f)
            speed *= backwardMultiplier;

        Vector3 horizontalVelocity = transform.forward * forwardInput * speed;

        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
    }

    void HandleAnimation()
    {
        bool isMoving = Mathf.Abs(forwardInput) > 0.1f;

        animator.SetBool("Walk", isMoving && !isRunning);
        animator.SetBool("Run", isMoving && isRunning);
        animator.SetFloat("Speed", forwardInput * (isRunning ? runMultiplier : 1f));

        animator.SetBool("Rest", !isMoving);
        animator.SetBool("Hide", false);
        animator.SetBool("Dead", false);
    }
}