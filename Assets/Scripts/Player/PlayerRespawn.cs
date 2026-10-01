using UnityEngine;

/// <summary>
/// 玩家掉到关卡下方时回到当前岛安全落点，保留收集进度与能力。
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
        if (SettingsMenu.IsGameplayBlocked) return;
        if (respawnPoint != null && transform.position.y < fallY)
        {
            Respawn();
        }
    }

    public void Respawn()
    {
        if (respawnPoint != null) MoveTo(respawnPoint);
    }

    /// <summary>禁用控制器后移动到安全落点，并清除上一地点的运动状态。</summary>
    public void MoveTo(Transform point)
    {
        if (point == null) return;
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null)
        {
            controller.enabled = false;
        }

        transform.SetPositionAndRotation(point.position, point.rotation);

        if (playerMovement != null)
        {
            playerMovement.ResetMotion();
        }

        if (controller != null)
        {
            controller.enabled = wasEnabled;
        }
        Physics.SyncTransforms();
    }
}
