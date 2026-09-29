using UnityEngine;

/// <summary>
/// 玩家的相机相对移动、跳跃与重力控制。
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public Transform cameraTransform;
    public float moveSpeed = 5f;
    public float jumpHeight = 1.6f;
    public float gravity = -20f;
    public float verticalVelocity;

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
        if (controller == null || !controller.enabled)
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

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDirection * moveSpeed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }
}
