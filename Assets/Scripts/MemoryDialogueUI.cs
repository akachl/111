using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>按拾取顺序显示完整记忆，用 E 翻到下一条，不暂停玩家。</summary>
public sealed class MemoryDialogueUI : MonoBehaviour
{
    public CanvasGroup panel;
    public TMP_Text titleText;
    public TMP_Text contentText;
    [Min(0.01f)] public float fadeDuration = 0.2f;
    [SerializeField, Min(200f)] private float panelWidth = 900f;
    [SerializeField, Min(0f)] private float screenMargin = 24f;
    private readonly Queue<StardustMemoryData> pending = new Queue<StardustMemoryData>();
    private bool showing;
    private bool closing;
    private bool coveredBySettings;
    public int PendingCount => pending.Count;
    public bool IsShowing => showing;

    private void Awake()
    {
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        panel.gameObject.SetActive(false);
    }

    public void Enqueue(StardustMemoryData memory)
    {
        if (memory == null) return;
        pending.Enqueue(memory);
        if (!showing) ShowNext();
    }

    private void ShowNext()
    {
        if (pending.Count == 0)
        {
            showing = false;
            panel.gameObject.SetActive(false);
            return;
        }
        StardustMemoryData memory = pending.Dequeue();
        titleText.text = $"{memory.id:00}/23  {memory.title}";
        contentText.text = memory.content;
        showing = true;
        closing = false;
        panel.alpha = 0f;
        panel.gameObject.SetActive(!coveredBySettings);
        RefreshLayout();
    }

    /// <summary>按可用屏幕宽度排版，背景高度由布局组件根据文本自动计算。</summary>
    public void RefreshLayout()
    {
        RectTransform rect = panel.GetComponent<RectTransform>();
        RectTransform parent = rect.parent as RectTransform;
        float width = parent != null ? Mathf.Min(panelWidth, Mathf.Max(200f, parent.rect.width - screenMargin * 2f)) : panelWidth;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        if (panel.gameObject.activeInHierarchy) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private void LateUpdate()
    {
        if (!showing || coveredBySettings) return;
        RectTransform rect = panel.GetComponent<RectTransform>();
        RectTransform parent = rect.parent as RectTransform;
        if (parent != null && !Mathf.Approximately(rect.rect.width, Mathf.Min(panelWidth, Mathf.Max(200f, parent.rect.width - screenMargin * 2f))))
            RefreshLayout();
    }

    private void Update()
    {
        if (!showing || SettingsMenu.IsGameplayBlocked) return;
        if (Input.GetKeyDown(KeyCode.E)) CloseCurrent();
        panel.alpha = Mathf.MoveTowards(panel.alpha, closing ? 0f : 1f, Time.deltaTime / fadeDuration);
        if (closing && panel.alpha <= 0f) ShowNext();
    }

    public void CloseCurrent()
    {
        if (showing && !SettingsMenu.IsGameplayBlocked) closing = true;
    }

    /// <summary>设置打开时暂时隐藏记忆，保留当前文字、队列和淡入进度。</summary>
    public void SetSettingsCovered(bool covered)
    {
        coveredBySettings = covered;
        panel.gameObject.SetActive(showing && !coveredBySettings);
        if (showing && !coveredBySettings) RefreshLayout();
    }
}
