using UnityEngine;

/// <summary>
/// 第一人称视角的鼠标查看和光标控制，用于玩家摄像机。
/// </summary>
[DefaultExecutionOrder(-100)]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    [Min(0f)] public float mouseSensitivity = 2f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    private Transform playerBody;
    private Renderer playerVisualRenderer;
    private bool originalVisualEnabled;
    private bool visualHidden;
    private float pitch;
    private bool cursorLocked;
    private bool menuOpen;

    private void Awake()
    {
        if (target != null)
        {
            playerBody = target.parent;
            if (playerBody != null)
            {
                Transform playerVisual = playerBody.Find("PlayerVisual");
                if (playerVisual != null)
                {
                    playerVisualRenderer = playerVisual.GetComponent<Renderer>();
                }
            }
        }

        pitch = 0f;
    }

    private void Start()
    {
        if (playerVisualRenderer != null)
        {
            originalVisualEnabled = playerVisualRenderer.enabled;
            playerVisualRenderer.enabled = false;
            visualHidden = true;
        }

        LockCursor();
        ApplyCameraRotation();
    }

    private void Update()
    {
        if (menuOpen || SettingsMenu.IsGameplayBlocked) return;
        if (SettingsMenu.Instance == null && Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
            return;
        }

        if (!cursorLocked)
        {
            if (Input.GetMouseButtonDown(0))
            {
                LockCursor();
            }

            return;
        }

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        if (playerBody != null && mouseX != 0f)
        {
            playerBody.Rotate(Vector3.up * mouseX, Space.World);
        }

        pitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);
        ApplyCameraRotation();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.position = target.position;
        ApplyCameraRotation();
    }

    private void ApplyCameraRotation()
    {
        Quaternion bodyYaw = playerBody != null
            ? Quaternion.Euler(0f, playerBody.eulerAngles.y, 0f)
            : Quaternion.identity;

        transform.rotation = bodyYaw * Quaternion.Euler(pitch, 0f, 0f);
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cursorLocked = true;
    }

    /// <summary>设置菜单独占鼠标，关闭菜单后恢复第一人称操作。</summary>
    public void SetMenuOpen(bool value)
    {
        menuOpen = value;
        if (value) UnlockCursor(); else LockCursor();
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorLocked = false;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            UnlockCursor();
        }
    }

    private void OnDisable()
    {
        if (visualHidden && playerVisualRenderer != null)
        {
            playerVisualRenderer.enabled = originalVisualEnabled;
            visualHidden = false;
        }

        UnlockCursor();
    }
}
