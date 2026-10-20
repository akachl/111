using UnityEngine;

public sealed class IslandTravel : MonoBehaviour
{
    public PlayerRespawn player;
    public PlayerMovement movement;
    public Transform[] safePoints = new Transform[3];
    public LighthouseController[] lighthouses = new LighthouseController[3];
    public TeleportUI ui;
    public int CurrentIsland { get; private set; } = 1;

    private void Start() { player.respawnPoint = safePoints[0]; }

    public bool CanTravel(int source, int destination)
    {
        StardustManager progress = StardustManager.Instance;
        if (progress == null || source != CurrentIsland) return false;
        if (source == 1 && destination == 2) return progress.IsIslandComplete(1);
        if (source == 2 && destination == 1 || source == 3 && destination == 2) return true;
        return source == 2 && destination == 3 && progress.IsIslandComplete(1) && progress.IsIslandComplete(2);
    }

    public bool TryTravel(int source, int destination)
    {
        // 按钮显示状态不能替代真正传送时的权限及落点校验。
        if (SettingsMenu.IsPaused || !CanTravel(source, destination) || destination < 1 || destination > safePoints.Length || safePoints[destination - 1] == null) return false;
        player.MoveTo(safePoints[destination - 1]);
        CurrentIsland = destination;
        player.respawnPoint = safePoints[destination - 1];
        if (destination == 3 && !movement.GlideUnlocked)
        {
            movement.UnlockGlide();
            if (ui != null) ui.Notify("滑翔已解锁：下降时按住空格，WASD 调整方向；松开空格恢复下落。", 9f);
        }
        if (ui != null) ui.Close();
        return true;
    }
}
