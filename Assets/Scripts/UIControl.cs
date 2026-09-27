using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 开始按钮的控制脚本：点击按钮后跳转到游戏场景（默认 "game"）。
///
/// 使用方式（二选一，都支持）：
/// 1. 挂在 "Button"（开始游戏）物体上；
/// 2. 挂在 Canvas 上。
/// 出于稳妥，Start() 里会自动找到开始按钮并绑定点击事件，
/// 因此不需要在 Inspector 里手动拖拽 OnClick。
/// 另需确保没有别的全屏 UI（如背景 Image）盖在按钮上方，
/// 否则点击会被它拦截（把它的 Raycast Target 取消勾选即可）。
/// </summary>
public class UIControl : MonoBehaviour
{
    [Tooltip("点击开始按钮后要进入的场景名，需已加入 Build Settings")]
    public string gameSceneName = "game";

    private void Start()
    {
        // 保证点击必然触发跳转，不依赖 Inspector 里 OnClick 的绑定
        Button button = GetComponent<Button>();
        if (button == null)
        {
            // 挂在 Canvas 上的情况：自动找到名为 "Button" 的开始按钮
            GameObject buttonObject = GameObject.Find("Button");
            if (buttonObject != null)
            {
                button = buttonObject.GetComponent<Button>();
            }
        }

        if (button == null)
        {
            Debug.LogWarning("[UIControl] 没有找到开始按钮，点击跳转未绑定。");
            return;
        }

        // Avoid loading the scene twice when a valid Inspector-persistent handler
        // is already wired, but keep the runtime fallback for missing or bad targets.
        bool hasValidStartHandler = false;
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) is UIControl
                && button.onClick.GetPersistentMethodName(i) == nameof(OnClickStart))
            {
                hasValidStartHandler = true;
                break;
            }
        }

        if (!hasValidStartHandler)
        {
            button.onClick.AddListener(OnClickStart);
            Debug.Log("[UIControl] 已为按钮 \"" + button.name + "\" 添加运行时跳转绑定。");
        }
    }

    /// <summary>
    /// 点击"开始"按钮时调用，跳转到 game 场景。
    /// </summary>
    public void OnClickStart()
    {
        LoadGameScene();
    }

    /// <summary>
    /// 加载游戏场景。场景名留空时回退为 "game"。
    /// </summary>
    public void LoadGameScene()
    {
        string sceneName = string.IsNullOrEmpty(gameSceneName) ? "game" : gameSceneName;

        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.Log("[UIControl] 开始加载场景: " + sceneName);
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError(
                "[UIControl] 无法加载场景 \"" + sceneName + "\"，请确认该场景已加入 " +
                "File > Build Settings 的 Scenes In Build 列表。");
        }
    }
}
