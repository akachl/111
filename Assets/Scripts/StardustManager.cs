using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>仅记录本次游戏的记忆，按编号去重，并通知计数和记忆界面。</summary>
[DisallowMultipleComponent]
public sealed class StardustManager : MonoBehaviour
{
    public const int Total = 23;
    public static StardustManager Instance { get; private set; }
    public StardustMemoryData[] memories;
    public TMP_Text counterText;
    public MemoryDialogueUI dialogue;
    private readonly HashSet<int> collected = new HashSet<int>();
    private readonly Dictionary<int, StardustMemoryData> catalog = new Dictionary<int, StardustMemoryData>();
    public int Count => collected.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("场景中出现重复的星尘管理器，已禁用重复组件。", this);
            enabled = false;
            return;
        }
        Instance = this;
        foreach (StardustMemoryData memory in memories ?? new StardustMemoryData[0])
        {
            if (memory == null || memory.id < 1 || memory.id > Total || catalog.ContainsKey(memory.id))
            {
                Debug.LogError("星尘记忆数据为空、编号越界或重复。", this);
                continue;
            }
            catalog.Add(memory.id, memory);
        }
        if (catalog.Count != Total) Debug.LogError("必须配置完整的 23 段星尘记忆。", this);
        UpdateCounter();
    }

    public bool TryCollect(StardustMemoryData memory)
    {
        if (Instance != this || memory == null || !catalog.TryGetValue(memory.id, out StardustMemoryData entry)) return false;
        if (Count >= Total || !collected.Add(memory.id)) return false;
        UpdateCounter();
        if (dialogue != null) dialogue.Enqueue(entry);
        return true;
    }

    private void UpdateCounter()
    {
        if (counterText != null) counterText.text = $"记忆 {Count}/{Total}";
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
