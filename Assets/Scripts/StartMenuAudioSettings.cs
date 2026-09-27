using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Controls the start scene's audio settings panel. UI references are serialized
/// so the panel and its behavior remain editable in the Unity Inspector.
/// </summary>
public sealed class StartMenuAudioSettings : MonoBehaviour
{
    [Header("Menu")]
    public Button startButton;
    public Button settingsButton;
    public TMP_Text settingsButtonLabel;
    public GameObject settingsPanel;
    public Vector2 menuSettingsPosition;
    public Vector2 panelReturnPosition;
    public int menuSettingsSiblingIndex;

    [Header("Audio controls")]
    public Slider musicSlider;
    public TMP_Text musicPercentLabel;
    public Slider sfxSlider;
    public TMP_Text sfxPercentLabel;
    public AudioMixer audioMixer;

    private RectTransform settingsButtonRect;
    private bool settingsOpen;

    private void Start()
    {
        StartAudioMixerService.UseMixer(audioMixer);

        if (settingsButton != null)
        {
            settingsButtonRect = settingsButton.GetComponent<RectTransform>();
            settingsButton.onClick.AddListener(ToggleSettings);
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
            SetSettingsOpen(false);
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

        slider.onValueChanged.AddListener(value =>
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
        });
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

    private void SetSettingsOpen(bool open)
    {
        bool wasOpen = settingsOpen;
        settingsOpen = open;

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(open);
        }

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
                else
                {
                    int siblingIndex = Mathf.Clamp(menuSettingsSiblingIndex, 0, settingsButtonRect.parent.childCount - 1);
                    settingsButtonRect.SetSiblingIndex(siblingIndex);
                }
            }
        }

        if (wasOpen && !open)
        {
            PlayerPrefs.Save();
        }
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}

/// <summary>
/// Shared volume service for every scene. It restores the saved mixer levels
/// before the first scene loads and provides a small routing API for later audio.
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
            Debug.LogError("AudioMixer could not read or set the exposed parameter '" + parameter + "'. Check the StarIsleAudio mixer asset.");
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
