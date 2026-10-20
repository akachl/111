using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public Transform cameraTransform;
    public float moveSpeed = 5f;
    public float jumpHeight = 1.6f;
    public float gravity = -20f;
    public float verticalVelocity;
    [Header("滑翔参数")]
    [Min(0.1f)] public float glideFallSpeed = 2f;
    public float glideGravity = -4f;
    [Min(0.1f)] public float glideTransition = 18f;
    public bool GlideUnlocked { get; private set; }
    public bool IsGliding { get; private set; }
    public WindField CurrentWind { get; private set; }
    private bool ceilingBlocked;

    public void UnlockGlide() { GlideUnlocked = true; }
    public void ResetMotion()
    {
        verticalVelocity = 0f;
        IsGliding = false;
        CurrentWind = null;
        ceilingBlocked = false;
    }

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (SettingsMenu.IsGameplayBlocked || controller == null || !controller.enabled)
        {
            return;
        }

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 input = Vector3.ClampMagnitude(new Vector3(horizontal, 0f, vertical), 1f);

        Vector3 cameraForward = cameraTransform != null ? cameraTransform.forward : transform.forward;
        Vector3 cameraRight = cameraTransform != null ? cameraTransform.right : transform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        if (cameraForward.sqrMagnitude > 0.0001f)
        {
            cameraForward.Normalize();
        }

        if (cameraRight.sqrMagnitude > 0.0001f)
        {
            cameraRight.Normalize();
        }

        Vector3 moveDirection = cameraForward * input.z + cameraRight * input.x;
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (controller.isGrounded && Input.GetButtonDown("Jump"))
        {
            verticalVelocity = Mathf.Sqrt(Mathf.Max(0f, jumpHeight * -2f * gravity));
        }

        StepMovement(moveDirection, Input.GetButton("Jump"), Time.deltaTime);
    }

    public void StepMovement(Vector3 direction, bool holdJump, float deltaTime)
    {
        if (SettingsMenu.IsGameplayBlocked || controller == null || !controller.enabled || deltaTime <= 0f) return;
        CurrentWind = WindField.FindStrongest(controller);
        IsGliding = false;
        if (CurrentWind != null)
        {
            // 接地粘附速度不能每帧抵消风场的初始加速，否则玩家会一直贴在地面。
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = 0f;
            verticalVelocity = ceilingBlocked ? 0f : Mathf.Min(CurrentWind.liftSpeed, Mathf.MoveTowards(verticalVelocity, CurrentWind.liftSpeed, CurrentWind.acceleration * deltaTime));
        }
        else if (GlideUnlocked && holdJump && !controller.isGrounded && verticalVelocity <= 0f)
        {
            IsGliding = true;
            verticalVelocity += glideGravity * deltaTime;
            if (verticalVelocity < -glideFallSpeed)
                verticalVelocity = Mathf.MoveTowards(verticalVelocity, -glideFallSpeed, glideTransition * deltaTime);
        }
        else verticalVelocity += gravity * deltaTime;

        // 脱离风场后沿用向上速度，再由普通重力自然消耗惯性。
        CollisionFlags flags = controller.Move((Vector3.ClampMagnitude(direction, 1f) * moveSpeed + Vector3.up * verticalVelocity) * deltaTime);
        ceilingBlocked = (flags & CollisionFlags.Above) != 0;
        if (ceilingBlocked && verticalVelocity > 0f) verticalVelocity = 0f;
        if ((flags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
            IsGliding = false;
        }
    }
}
