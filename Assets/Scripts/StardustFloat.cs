using UnityEngine;

public sealed class StardustFloat : MonoBehaviour
{
    [SerializeField] private Transform model;
    [SerializeField] private Transform particle;
    [SerializeField, Min(0f)] private float floatHeight = 0.15f;
    [SerializeField, Min(0f)] private float floatSpeed = 1.4f;
    [SerializeField] private float rotateSpeed = 30f;

    private Vector3 modelPosition;
    private Vector3 particlePosition;

    private void Awake()
    {
        if (model != null) modelPosition = model.localPosition;
        if (particle != null) particlePosition = particle.localPosition;
    }

    private void Update()
    {
        float offset = Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        if (model != null)
        {
            model.localPosition = modelPosition + Vector3.up * offset;
            model.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);
        }

        if (particle != null)
        {
            particle.localPosition = particlePosition + Vector3.up * offset;
        }
    }
}
