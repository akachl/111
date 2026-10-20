using UnityEngine;

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
