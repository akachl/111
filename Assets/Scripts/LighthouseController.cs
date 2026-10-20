using UnityEngine;

public sealed class LighthouseController : MonoBehaviour
{
    [Range(1, 3)] public int island = 1;
    [Min(0.1f)] public float interactionRadius = 4.5f;
    public Renderer core;
    public Light warmLight;
    public ParticleSystem steadyParticles;
    public ParticleSystem ignitionParticles;
    public ParticleSystem halo;
    public LineRenderer ring;
    public TeleportUI ui;
    public float lightDuration = 2f;
    private StardustManager progress;
    private Material coreMaterial;
    private bool lit;
    private float elapsed;
    public bool IsLit => lit;
    public int IgnitionCount { get; private set; }
    public string ProgressHint
    {
        get
        {
            int count = StardustManager.Instance != null ? StardustManager.Instance.GetIslandCount(island) : 0;
            string message = $"本岛记忆 {count}/{StardustManager.IslandTotal(island)}" + (lit ? " · 灯塔已点亮" : " · 集齐后点亮灯塔");
            return island > 1 ? message + $" · 可返回第 {island - 1} 岛" : message;
        }
    }
    private void Start()
    {
        coreMaterial = core.material;
        progress = StardustManager.Instance;
        if (progress != null) progress.ProgressChanged += OnProgress;
        steadyParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ignitionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ring.enabled = false;
        halo.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        lit = progress != null && progress.IsIslandComplete(island);
        elapsed = lit ? lightDuration : 0f;
        if (lit) steadyParticles.Play();
        if (lit) halo.Play();
        ApplyLight(lit ? 1f : 0f);
    }
    private void OnProgress(int id)
    {
        if (lit || progress == null || !progress.IsIslandComplete(island)) return;
        lit = true;
        elapsed = 0f;
        IgnitionCount++;
        GameplayOneShotAudio.PlayLighthouse();
        steadyParticles.Play();
        ignitionParticles.Play();
        halo.Play();
        ring.enabled = true;
        if (ui != null) ui.Notify($"第 {island} 岛记忆已集齐，灯塔正在点亮。靠近后按 F 选择目的地。");
    }
    private void Update()
    {
        if (!lit || elapsed >= lightDuration || GameplayPause.IsPaused) return;
        elapsed = Mathf.Min(lightDuration, elapsed + Time.deltaTime);
        float amount = Mathf.SmoothStep(0f, 1f, elapsed / lightDuration);
        ApplyLight(amount);
        float radius = Mathf.Lerp(1f, 4f, amount);
        for (int index = 0; index < ring.positionCount; index++)
        {
            float angle = index * Mathf.PI * 2f / ring.positionCount;
            ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
        Color gold = new Color(1f, 0.824f, 0.478f, 1f - amount);
        ring.startColor = gold; ring.endColor = gold;
        if (elapsed >= lightDuration) ring.enabled = false;
    }
    private void ApplyLight(float amount)
    {
        Color warm = new Color(1f, 0.949f, 0.824f);
        coreMaterial.SetColor("_BaseColor", Color.Lerp(new Color(0.13f, 0.11f, 0.08f), warm, amount));
        coreMaterial.SetColor("_EmissionColor", warm * amount * 2f);
        warmLight.intensity = amount * 2.2f;
    }
    public bool IsNear(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        return new Vector2(offset.x, offset.z).sqrMagnitude <= interactionRadius * interactionRadius && Mathf.Abs(offset.y) < 6f;
    }
    private void OnDestroy()
    {
        if (progress != null) progress.ProgressChanged -= OnProgress;
        if (coreMaterial != null) Destroy(coreMaterial);
    }
}
