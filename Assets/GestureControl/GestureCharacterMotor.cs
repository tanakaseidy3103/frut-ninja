using UnityEngine;

[DisallowMultipleComponent]
public class GestureCharacterMotor : MonoBehaviour
{
    [SerializeField] private GestureUdpReceiver receiver;
    [SerializeField] private string playerId = "player-1";
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Rigidbody rigidBody;
    [SerializeField] private Animator animator;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float gravity = -18f;
    [SerializeField] private float jumpHeight = 1.3f;
    [SerializeField] private float attackCooldown = 0.35f;
    [SerializeField] private bool useLocalAxes;

    private float verticalSpeed;
    private bool jumpLatch;
    private float lastAttackTime = -999f;

    public string PlayerId
    {
        get { return playerId; }
        set { playerId = value; }
    }

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        rigidBody = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        GesturePacket packet;
        GestureCommand command = new GestureCommand();
        string gesture = "none";

        if (receiver != null && receiver.TryGetLatestPacket(playerId, out packet) && packet != null)
        {
            if (packet.command != null)
            {
                command = packet.command;
            }

            if (!string.IsNullOrWhiteSpace(packet.gesture))
            {
                gesture = packet.gesture;
            }
        }

        Vector3 moveInput = new Vector3(command.moveX, 0f, command.moveZ);
        moveInput = Vector3.ClampMagnitude(moveInput, 1f);

        bool jumpStarted = false;

        if (characterController != null)
        {
            jumpStarted = ApplyCharacterControllerMove(moveInput, command.jump);
        }
        else if (rigidBody != null)
        {
            jumpStarted = ApplyRigidBodyMove(moveInput, command.jump);
        }
        else
        {
            Vector3 worldMove = ResolveMoveDirection(moveInput);
            transform.position += worldMove * moveSpeed * Time.deltaTime;
            jumpLatch = command.jump;
        }

        HandleAttack(command.attack);
        UpdateAnimator(moveInput, gesture, command.jump, command.attack, jumpStarted);
    }

    private Vector3 ResolveMoveDirection(Vector3 moveInput)
    {
        if (!useLocalAxes)
        {
            return moveInput;
        }

        return transform.TransformDirection(moveInput);
    }

    private bool ApplyCharacterControllerMove(Vector3 moveInput, bool jumpRequested)
    {
        Vector3 worldMove = ResolveMoveDirection(moveInput);
        bool grounded = characterController.isGrounded;
        bool jumpStarted = false;

        if (grounded && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }

        if (jumpRequested && grounded && !jumpLatch)
        {
            verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpStarted = true;
        }

        jumpLatch = jumpRequested;
        characterController.Move(worldMove * moveSpeed * Time.deltaTime);

        verticalSpeed += gravity * Time.deltaTime;
        characterController.Move(Vector3.up * verticalSpeed * Time.deltaTime);

        return jumpStarted;
    }

    private bool ApplyRigidBodyMove(Vector3 moveInput, bool jumpRequested)
    {
        Vector3 worldMove = ResolveMoveDirection(moveInput);
        Vector3 velocity = rigidBody.linearVelocity;
        bool jumpStarted = false;

        velocity.x = worldMove.x * moveSpeed;
        velocity.z = worldMove.z * moveSpeed;
        rigidBody.linearVelocity = velocity;

        if (jumpRequested && !jumpLatch && Mathf.Abs(rigidBody.linearVelocity.y) < 0.05f)
        {
            rigidBody.AddForce(Vector3.up * Mathf.Sqrt(jumpHeight * -2f * gravity), ForceMode.VelocityChange);
            jumpStarted = true;
        }

        jumpLatch = jumpRequested;
        return jumpStarted;
    }

    private void HandleAttack(bool attackRequested)
    {
        if (!attackRequested)
        {
            return;
        }

        if (Time.time - lastAttackTime < attackCooldown)
        {
            return;
        }

        lastAttackTime = Time.time;

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }

    private void UpdateAnimator(Vector3 moveInput, string gesture, bool jumpRequested, bool attackRequested, bool jumpStarted)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetFloat("MoveX", moveInput.x);
        animator.SetFloat("MoveZ", moveInput.z);
        animator.SetFloat("Speed", moveInput.magnitude);
        animator.SetBool("JumpPressed", jumpRequested);
        animator.SetBool("AttackPressed", attackRequested);
        animator.SetInteger("GestureId", GestureToId(gesture));

        if (jumpStarted)
        {
            animator.SetTrigger("Jump");
        }
    }

    private int GestureToId(string gesture)
    {
        switch (gesture)
        {
            case "open":
                return 1;
            case "fist":
                return 2;
            case "swipe_left":
                return 3;
            case "swipe_right":
                return 4;
            case "swipe_up":
                return 5;
            default:
                return 0;
        }
    }
}