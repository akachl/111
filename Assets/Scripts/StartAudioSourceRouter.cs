using UnityEngine;

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
