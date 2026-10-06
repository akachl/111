using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 管理开始界面的音频设置，支持独立返回按钮，并兼容原菜单按钮返回方式。
/// </summary>
public sealed class StartMenuAudioSettings : MonoBehaviour
{
    [Header("菜单")]
    public Button startButton;
    public Button settingsButton;
    public TMP_Text settingsButtonLabel;
    public GameObject settingsPanel;
    public Vector2 menuSettingsPosition;
    public Vector2 panelReturnPosition;
    public int menuSettingsSiblingIndex;
    public Button closeButton;
    public CanvasGroup menuGroup;

    [Header("音频控制")]
    public Slider musicSlider;
    public TMP_Text musicPercentLabel;
    public Slider sfxSlider;
    public TMP_Text sfxPercentLabel;
    public AudioMixer audioMixer;

    public bool IsSettingsOpen => settingsOpen;
    public bool UsesSeparateCloseButton => closeButton != null;

    private RectTransform settingsButtonRect;
    private bool settingsOpen;
    private UnityAction<float> musicValueChanged;
    private UnityAction<float> sfxValueChanged;

    private void Start()
    {
        StartAudioMixerService.UseMixer(audioMixer);

        if (settingsButton != null)
        {
            settingsButtonRect = settingsButton.GetComponent<RectTransform>();
            settingsButton.onClick.AddListener(ToggleSettings);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseSettings);
        }

        ConfigureSlider(musicSlider, StartAudioMixerService.MusicPreferenceKey, musicPercentLabel, true);
        ConfigureSlider(sfxSlider, StartAudioMixerService.SfxPreferenceKey, sfxPercentLabel, false);

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        SetSettingsOpen(false);
    }

    private void Update()
    {
        if (settingsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseSettings();
        }
    }

    private void ConfigureSlider(Slider slider, string preferenceKey, TMP_Text percentLabel, bool music)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true;

        int savedPercent = PlayerPrefs.GetInt(preferenceKey, 100);
        float initialValue = Mathf.Clamp(savedPercent, 0, 100);
        slider.SetValueWithoutNotify(initialValue);
        UpdatePercentLabel(percentLabel, initialValue);
        if (music)
        {
            StartAudioMixerService.SetMusicPercent(initialValue, false);
        }
        else
        {
            StartAudioMixerService.SetSfxPercent(initialValue, false);
        }

        UnityAction<float> valueChanged = value =>
        {
            int percent = Mathf.Clamp(Mathf.RoundToInt(value), 0, 100);
            UpdatePercentLabel(percentLabel, percent);
            PlayerPrefs.SetInt(preferenceKey, percent);
            if (music)
            {
                StartAudioMixerService.SetMusicPercent(percent, false);
            }
            else
            {
                StartAudioMixerService.SetSfxPercent(percent, false);
            }
        };
        slider.onValueChanged.AddListener(valueChanged);
        if (music)
        {
            musicValueChanged = valueChanged;
        }
        else
        {
            sfxValueChanged = valueChanged;
        }
    }

    private static void UpdatePercentLabel(TMP_Text label, float value)
    {
        if (label != null)
        {
            label.text = Mathf.RoundToInt(value) + "%";
        }
    }

    private void ToggleSettings()
    {
        SetSettingsOpen(!settingsOpen);
    }

    /// <summary>关闭音频设置并将键盘焦点恢复到设置按钮。</summary>
    public void CloseSettings()
    {
        SetSettingsOpen(false);
    }

    private void SetSettingsOpen(bool open)
    {
        bool wasOpen = settingsOpen;
        settingsOpen = open;

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(open);
        }

        if (UsesSeparateCloseButton)
        {
            // 独立返回模式保持菜单可见，仅禁止被弹窗覆盖的菜单接收输入。
            if (menuGroup != null)
            {
                menuGroup.interactable = !open;
                menuGroup.blocksRaycasts = !open;
            }
        }
        else
        {
            if (startButton != null)
            {
                startButton.gameObject.SetActive(!open);
            }

            if (settingsButton != null)
            {
                if (settingsButtonLabel != null)
                {
                    settingsButtonLabel.text = open ? "返回" : "设置";
                }

                if (settingsButtonRect == null)
                {
                    settingsButtonRect = settingsButton.GetComponent<RectTransform>();
                }

                if (settingsButtonRect != null)
                {
                    settingsButtonRect.anchoredPosition = open ? panelReturnPosition : menuSettingsPosition;
                    if (open)
                    {
                        settingsButtonRect.SetAsLastSibling();
                    }
                    else if (settingsButtonRect.parent != null)
                    {
                        int siblingIndex = Mathf.Clamp(menuSettingsSiblingIndex, 0, settingsButtonRect.parent.childCount - 1);
                        settingsButtonRect.SetSiblingIndex(siblingIndex);
                    }
                }
            }
        }

        if (wasOpen && !open)
        {
            PlayerPrefs.Save();
        }

        if (open != wasOpen && EventSystem.current != null)
        {
            Selectable focus = open ? (Selectable)musicSlider : settingsButton;
            if (focus != null && focus.IsActive() && focus.IsInteractable())
            {
                EventSystem.current.SetSelectedGameObject(focus.gameObject);
            }
        }
    }

    private void OnDestroy()
    {
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(ToggleSettings);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseSettings);
        }

        if (musicSlider != null && musicValueChanged != null)
        {
            musicSlider.onValueChanged.RemoveListener(musicValueChanged);
        }

        if (sfxSlider != null && sfxValueChanged != null)
        {
            sfxSlider.onValueChanged.RemoveListener(sfxValueChanged);
        }
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}

/// <summary>
/// 为各场景共享音量偏好与混音器设置，并为音源提供音乐或音效分组路由。
/// </summary>
public static class StartAudioMixerService
{
    public const string MusicPreferenceKey = "StarIsle.Audio.Music";
    public const string SfxPreferenceKey = "StarIsle.Audio.SFX";
    private const string MixerResourcePath = "Audio/StarIsleAudio";
    private const float MinimumDecibels = -80f;

    private static AudioMixer mixer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeAfterFirstScene()
    {
        mixer = Resources.Load<AudioMixer>(MixerResourcePath);
        if (mixer != null)
        {
            ApplyPercent("MusicVolume", PlayerPrefs.GetInt(MusicPreferenceKey, 100));
            ApplyPercent("SFXVolume", PlayerPrefs.GetInt(SfxPreferenceKey, 100));
        }
    }

    public static void UseMixer(AudioMixer audioMixer)
    {
        if (audioMixer == null)
        {
            return;
        }

        mixer = audioMixer;
    }

    public static void SetMusicPercent(float percent, bool saveImmediately = false)
    {
        SetPercent("MusicVolume", MusicPreferenceKey, percent, saveImmediately);
    }

    public static void SetSfxPercent(float percent, bool saveImmediately = false)
    {
        SetPercent("SFXVolume", SfxPreferenceKey, percent, saveImmediately);
    }

    public static bool RouteAudioSource(AudioSource source, bool music)
    {
        EnsureMixerLoaded();
        if (source == null || mixer == null)
        {
            return false;
        }

        AudioMixerGroup[] groups = mixer.FindMatchingGroups(music ? "Music" : "SFX");
        if (groups == null || groups.Length == 0)
        {
            return false;
        }

        source.outputAudioMixerGroup = groups[0];
        return true;
    }

    private static void SetPercent(string parameter, string preferenceKey, float percent, bool saveImmediately)
    {
        int value = Mathf.Clamp(Mathf.RoundToInt(percent), 0, 100);
        PlayerPrefs.SetInt(preferenceKey, value);
        ApplyPercent(parameter, value);
        if (saveImmediately)
        {
            PlayerPrefs.Save();
        }
    }

    private static void ApplyPercent(string parameter, float percent)
    {
        EnsureMixerLoaded();
        if (mixer == null)
        {
            return;
        }

        float normalized = Mathf.Clamp01(percent / 100f);
        float decibels = normalized <= 0f
            ? MinimumDecibels
            : Mathf.Max(MinimumDecibels, 20f * Mathf.Log10(normalized));
        if (!mixer.SetFloat(parameter, decibels) || !mixer.GetFloat(parameter, out _))
        {
            Debug.LogError("无法读取或设置混音器公开参数“" + parameter + "”，请检查 StarIsleAudio 混音器资源。");
        }
    }

    private static void EnsureMixerLoaded()
    {
        if (mixer == null)
        {
            mixer = Resources.Load<AudioMixer>(MixerResourcePath);
        }
    }
}
