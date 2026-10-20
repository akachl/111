using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AudioSettings : MonoBehaviour
{
    public Slider musicSlider;
    public Slider sfxSlider;
    public TMP_Text musicPercent;
    public TMP_Text sfxPercent;

    private void Start()
    {
        musicSlider.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt(StartAudioMixerService.MusicPreferenceKey, 100), 0, 100));
        sfxSlider.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt(StartAudioMixerService.SfxPreferenceKey, 100), 0, 100));
        ApplyMusic(musicSlider.value);
        ApplySfx(sfxSlider.value);
        musicSlider.onValueChanged.AddListener(ApplyMusic);
        sfxSlider.onValueChanged.AddListener(ApplySfx);
    }

    private void ApplyMusic(float value)
    {
        StartAudioMixerService.SetMusicPercent(value);
        musicPercent.text = Mathf.RoundToInt(value) + "%";
    }

    private void ApplySfx(float value)
    {
        StartAudioMixerService.SetSfxPercent(value);
        sfxPercent.text = Mathf.RoundToInt(value) + "%";
    }

    private void OnDestroy()
    {
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(ApplyMusic);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(ApplySfx);
    }
}
