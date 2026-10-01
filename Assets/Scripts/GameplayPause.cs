using System.Collections.Generic;
using UnityEngine;

/// <summary>统一管理设置与传送的暂停所有权，以及恢复当帧的输入屏蔽。</summary>
public static class GameplayPause
{
    private static readonly HashSet<Object> owners = new HashSet<Object>();
    private static int blockedFrame = -1;
    private static float previousScale = 1f;
    private static CameraFollow cameraController;
    private static MemoryDialogueUI dialogue;
    public static bool IsPaused => owners.Count > 0;
    public static bool IsBlocked => IsPaused || blockedFrame == Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        owners.Clear();
        blockedFrame = -1;
        previousScale = 1f;
        cameraController = null;
        dialogue = null;
    }

    public static void Set(Object owner, bool paused)
    {
        if (paused)
        {
            if (owners.Count == 0) previousScale = Time.timeScale;
            owners.Add(owner);
        }
        else
        {
            owners.Remove(owner);
            blockedFrame = Time.frameCount;
        }
        Time.timeScale = IsPaused ? 0f : previousScale;
        SettingsMenu settings = SettingsMenu.Instance;
        if (settings != null)
        {
            cameraController = settings.cameraController;
            dialogue = settings.dialogue;
        }
        // 设置组件可独立销毁，传送面板关闭时仍需恢复现有相机与记忆界面。
        if (cameraController != null) cameraController.SetMenuOpen(IsPaused);
        if (dialogue != null) dialogue.SetSettingsCovered(IsPaused);
    }
}
