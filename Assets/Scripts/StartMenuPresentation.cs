using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>管理开始界面的退出确认、菜单焦点和弹窗内键盘循环导航。</summary>
[DisallowMultipleComponent]
public sealed class StartMenuPresentation : MonoBehaviour
{
    [Header("主菜单")]
    public Button startButton;
    public Button settingsButton;
    public Button exitButton;
    public CanvasGroup menuGroup;
    public StartMenuAudioSettings audioSettings;

    [Header("退出确认")]
    public GameObject exitPanel;
    public Button cancelExitButton;
    public Button confirmExitButton;

    public bool IsExitOpen => exitOpen;

    private readonly List<Selectable> focusTargets = new List<Selectable>(3);
    private bool exitOpen;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (exitPanel != null)
        {
            exitPanel.SetActive(false);
        }
    }

    private void Start()
    {
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(OpenExit);
        }

        if (cancelExitButton != null)
        {
            cancelExitButton.onClick.AddListener(CloseExit);
        }

        if (confirmExitButton != null)
        {
            confirmExitButton.onClick.AddListener(ConfirmExit);
        }

        SetMenuInteraction(true);
        Focus(startButton);
    }

    private void Update()
    {
        if (exitOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseExit();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            bool reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            CycleFocus(reverse);
        }
    }

    /// <summary>打开退出确认，并将焦点放在取消按钮，避免误退出。</summary>
    public void OpenExit()
    {
        if (exitOpen || exitPanel == null)
        {
            return;
        }

        if (audioSettings != null && audioSettings.IsSettingsOpen)
        {
            audioSettings.CloseSettings();
        }

        exitOpen = true;
        exitPanel.SetActive(true);
        SetMenuInteraction(false);
        Focus(cancelExitButton);
    }

    /// <summary>关闭退出确认并恢复退出按钮的键盘焦点。</summary>
    public void CloseExit()
    {
        if (!exitOpen)
        {
            return;
        }

        exitOpen = false;
        if (exitPanel != null)
        {
            exitPanel.SetActive(false);
        }

        SetMenuInteraction(audioSettings == null || !audioSettings.IsSettingsOpen);
        Focus(exitButton);
    }

    /// <summary>保存音量偏好后退出；编辑器只停止运行，打包游戏则关闭程序。</summary>
    public void ConfirmExit()
    {
        if (!exitOpen)
        {
            return;
        }

        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetMenuInteraction(bool enabled)
    {
        if (menuGroup == null)
        {
            return;
        }

        menuGroup.interactable = enabled;
        menuGroup.blocksRaycasts = enabled;
    }

    private void CycleFocus(bool reverse)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return;
        }

        focusTargets.Clear();
        if (exitOpen)
        {
            AddFocusTarget(cancelExitButton);
            AddFocusTarget(confirmExitButton);
        }
        else if (audioSettings != null && audioSettings.IsSettingsOpen)
        {
            AddFocusTarget(audioSettings.musicSlider);
            AddFocusTarget(audioSettings.sfxSlider);
            AddFocusTarget(audioSettings.UsesSeparateCloseButton ? audioSettings.closeButton : audioSettings.settingsButton);
        }
        else
        {
            AddFocusTarget(startButton);
            AddFocusTarget(settingsButton);
            AddFocusTarget(exitButton);
        }

        if (focusTargets.Count == 0)
        {
            return;
        }

        int currentIndex = focusTargets.FindIndex(target => target.gameObject == eventSystem.currentSelectedGameObject);
        int nextIndex = currentIndex < 0
            ? (reverse ? focusTargets.Count - 1 : 0)
            : (currentIndex + (reverse ? -1 : 1) + focusTargets.Count) % focusTargets.Count;
        Focus(focusTargets[nextIndex]);
    }

    private void AddFocusTarget(Selectable target)
    {
        if (target != null && target.IsActive() && target.IsInteractable())
        {
            focusTargets.Add(target);
        }
    }

    private static void Focus(Selectable target)
    {
        if (EventSystem.current != null && target != null && target.IsActive() && target.IsInteractable())
        {
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }

    private void OnDestroy()
    {
        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OpenExit);
        }

        if (cancelExitButton != null)
        {
            cancelExitButton.onClick.RemoveListener(CloseExit);
        }

        if (confirmExitButton != null)
        {
            confirmExitButton.onClick.RemoveListener(ConfirmExit);
        }
    }
}
