using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    public void OnClickStart()
    {
        LoadGameScene();
    }

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
