using System.Collections;
using UnityEngine;

/// <summary>
/// 玩家碰到星尘时只拾取一次，播放粒子反馈后移除实例。
/// </summary>
public sealed class StardustPickup : MonoBehaviour
{
    [SerializeField] private GameObject model;
    [SerializeField] private ParticleSystem ambientParticles;
    [SerializeField] private ParticleSystem pickupParticles;
    [SerializeField] private Collider pickupTrigger;
    [SerializeField] private StardustMemoryData memory;

    private bool isPickedUp;

    private void OnTriggerEnter(Collider other)
    {
        if (isPickedUp || !other.CompareTag("Player")) return;

        isPickedUp = true;
        if (StardustManager.Instance != null) StardustManager.Instance.TryCollect(memory);
        if (pickupTrigger != null) pickupTrigger.enabled = false;
        if (model != null) model.SetActive(false);
        if (ambientParticles != null)
        {
            ambientParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ambientParticles.gameObject.SetActive(false);
        }

        if (pickupParticles != null)
        {
            pickupParticles.Play(true);
            StartCoroutine(WaitForParticles());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator WaitForParticles()
    {
        yield return new WaitWhile(() => pickupParticles != null && pickupParticles.IsAlive(true));
        Destroy(gameObject);
    }
}
