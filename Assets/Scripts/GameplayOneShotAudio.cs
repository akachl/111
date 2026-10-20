using UnityEngine;

public sealed class GameplayOneShotAudio : MonoBehaviour
{
    private static AudioClip stardustClip;
    private static AudioClip lighthouseClip;
    private AudioSource source;
    private bool paused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetClips()
    {
        stardustClip = null;
        lighthouseClip = null;
    }

    public static void PlayStardust()
    {
        if (stardustClip == null) stardustClip = Resources.Load<AudioClip>("Audio/SFX/StardustPickup_A");
        Play(stardustClip, 0.65f);
    }

    public static void PlayLighthouse()
    {
        if (lighthouseClip == null) lighthouseClip = Resources.Load<AudioClip>("Audio/SFX/LighthouseIgnition_B");
        Play(lighthouseClip, 0.85f);
    }

    private static void Play(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            Debug.LogError("无法加载星尘或灯塔音效，请检查 Resources/Audio/SFX。");
            return;
        }

        // 独立对象保留拾取物销毁后的尾音，切换场景时自动清理。
        GameObject voice = new GameObject("音效_" + clip.name);
        AudioSource audioSource = voice.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        // 收集和点亮属于全局进度反馈，使用二维声音保证远处点亮也能听见。
        audioSource.spatialBlend = 0f;
        audioSource.clip = clip;
        audioSource.volume = volume;
        if (!StartAudioMixerService.RouteAudioSource(audioSource, false))
        {
            Debug.LogError("星尘或灯塔音效未能接入 SFX 混音通道。");
            Destroy(voice);
            return;
        }
        GameplayOneShotAudio lifetime = voice.AddComponent<GameplayOneShotAudio>();
        lifetime.source = audioSource;
        audioSource.Play();
        if (GameplayPause.IsPaused)
        {
            audioSource.Pause();
            lifetime.paused = true;
        }
    }

    private void Update()
    {
        if (source == null) return;
        if (GameplayPause.IsPaused)
        {
            if (!paused)
            {
                source.Pause();
                paused = true;
            }
            return;
        }
        if (paused)
        {
            source.UnPause();
            paused = false;
        }
        if (!source.isPlaying) Destroy(gameObject);
    }
}
