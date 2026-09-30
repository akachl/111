using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>管理游戏设置、暂停、光标及返回开始场景，状态只属于本次游戏。</summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance { get; private set; }
    public static bool IsPaused => Instance != null && Instance.open;
    public static bool IsGameplayBlocked => IsPaused || (Instance != null && Instance.resumeFrame == Time.frameCount);
    public GameObject settingsPanel;
    public Button settingsButton;
    public Button continueButton;
    public Button backToStartButton;
    public CanvasGroup hud;
    public CameraFollow cameraController;
    public MemoryDialogueUI dialogue;
    public string startSceneName = "start";
    private bool open;
    private int resumeFrame = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("场景中出现重复的设置管理器，已禁用重复组件。", this);
            enabled = false;
            return;
        }
        Instance = this;
        Time.timeScale = 1f;
        settingsPanel.SetActive(false);
    }

    private void Start()
    {
        settingsButton.onClick.AddListener(Open);
        continueButton.onClick.AddListener(Resume);
        backToStartButton.onClick.AddListener(BackToStart);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (open) Resume(); else Open();
        }
    }

    public void Open()
    {
        if (open) return;
        open = true;
        Time.timeScale = 0f;
        settingsPanel.SetActive(true);
        if (dialogue != null) dialogue.SetSettingsCovered(true);
        hud.interactable = false;
        hud.blocksRaycasts = false;
        cameraController.SetMenuOpen(true);
    }

    public void Resume()
    {
        if (!open) return;
        RestorePauseState(true);
    }

    private void RestorePauseState(bool lockCursor)
    {
        open = false;
        resumeFrame = Time.frameCount;
        Time.timeScale = 1f;
        settingsPanel.SetActive(false);
        if (dialogue != null) dialogue.SetSettingsCovered(false);
        hud.interactable = true;
        hud.blocksRaycasts = true;
        if (cameraController != null) cameraController.SetMenuOpen(!lockCursor);
        PlayerPrefs.Save();
    }

    public void BackToStart()
    {
        if (!Application.CanStreamedLevelBeLoaded(startSceneName))
        {
            Debug.LogError("开始场景未加入构建列表。", this);
            return;
        }
        RestorePauseState(false);
        SceneManager.LoadScene(startSceneName);
    }

    private void OnDisable()
    {
        if (Instance != this) return;
        if (open) RestorePauseState(false);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Time.timeScale = 1f;
        Instance = null;
    }
}
