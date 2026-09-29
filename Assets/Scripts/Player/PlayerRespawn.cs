using UnityEngine;

/// <summary>
/// 在玩家掉落到低于关卡后，将其返回到安全的起始点。
/// </summary>
public class PlayerRespawn : MonoBehaviour
{
    public Transform respawnPoint;
    public float fallY = -10f;

    private CharacterController controller;
    private PlayerMovement playerMovement;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (respawnPoint != null && transform.position.y < fallY)
        {
            Respawn();
        }
    }

    private void Respawn()
    {
        if (controller != null)
        {
            controller.enabled = false;
        }

        transform.SetPositionAndRotation(respawnPoint.position, respawnPoint.rotation);

        if (playerMovement != null)
        {
            playerMovement.verticalVelocity = 0f;
        }

        if (controller != null)
        {
            controller.enabled = true;
        }
    }
}
