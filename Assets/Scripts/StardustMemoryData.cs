using UnityEngine;

[CreateAssetMenu(menuName = "星屿/星尘记忆", fileName = "Memory")]
public sealed class StardustMemoryData : ScriptableObject
{
    [Range(1, 23)] public int id;
    public string title;
    [TextArea(4, 20)] public string content;
}
