using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class WindField : MonoBehaviour
{
    private static readonly List<WindField> fields = new List<WindField>();
    [Min(0.1f)] public float liftSpeed = 8f;
    [Min(0.1f)] public float acceleration = 24f;
    public AudioSource windAudio;
    private BoxCollider volume;
    private bool audioPaused;
    private void OnEnable() { volume = GetComponent<BoxCollider>(); if (!fields.Contains(this)) fields.Add(this); }
    private void OnDisable()
    {
        fields.Remove(this);
        if (audioPaused && windAudio != null) windAudio.UnPause();
        audioPaused = false;
    }

    private void Update()
    {
        if (windAudio == null) return;
        if (GameplayPause.IsPaused && windAudio.isPlaying) { windAudio.Pause(); audioPaused = true; }
        else if (!GameplayPause.IsPaused && audioPaused) { windAudio.UnPause(); audioPaused = false; }
    }

    public bool Contains(CharacterController player)
    {
        if (!isActiveAndEnabled || volume == null || !volume.enabled || player == null) return false;
        // 以胶囊中心进入范围为准，避免仅擦过底边就反复托举。
        Vector3 center = transform.InverseTransformPoint(player.transform.TransformPoint(player.center)) - volume.center;
        Vector3 half = volume.size * 0.5f;
        return Mathf.Abs(center.x) <= half.x && Mathf.Abs(center.y) <= half.y && Mathf.Abs(center.z) <= half.z;
    }

    public static WindField FindStrongest(CharacterController player)
    {
        WindField strongest = null;
        foreach (WindField field in fields)
            if (field != null && field.Contains(player) && (strongest == null || field.liftSpeed > strongest.liftSpeed)) strongest = field;
        return strongest;
    }
}
