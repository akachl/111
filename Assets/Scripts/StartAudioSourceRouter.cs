using UnityEngine;

/// <summary>Attach to a future AudioSource to route it through Music or SFX.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class StartAudioSourceRouter : MonoBehaviour
{
    [SerializeField] private bool music;

    private void Awake()
    {
        StartAudioMixerService.RouteAudioSource(GetComponent<AudioSource>(), music);
    }
}
