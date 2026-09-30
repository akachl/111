using UnityEngine;

/// <summary>保存一颗星尘的唯一编号、标题和原始记忆正文。</summary>
[CreateAssetMenu(menuName = "星屿/星尘记忆", fileName = "Memory")]
public sealed class StardustMemoryData : ScriptableObject
{
    [Range(1, 23)] public int id;
    public string title;
    [TextArea(4, 20)] public string content;
}
