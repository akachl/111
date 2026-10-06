using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>通过柔和背景和金色下划线呈现按钮悬停与键盘焦点，不改变布局。</summary>
[DisallowMultipleComponent]
public sealed class StartMenuButtonFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public Button button;
    public Image highlight;
    public Image underline;
    public TMP_Text label;
    public bool selectedByDefault;
    [Range(0f, 1f)] public float highlightAlpha = 1f;
    [Range(0f, 1f)] public float underlineAlpha = 1f;

    private const float TransitionDuration = 0.18f;
    private bool pointerInside;
    private bool keyboardSelected;
    private float visibility;
    private float highlightOpacity;
    private float underlineOpacity;
    private FontStyles normalFontStyle;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        highlightOpacity = highlightAlpha;
        underlineOpacity = underlineAlpha;
        normalFontStyle = label != null ? label.fontStyle : FontStyles.Normal;
    }

    private void OnEnable()
    {
        GameObject currentSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        keyboardSelected = currentSelection == gameObject || (selectedByDefault && currentSelection == null);
        pointerInside = false;
        visibility = keyboardSelected ? 1f : 0f;
        ApplyFeedback();
    }

    private void Update()
    {
        bool pointerOwnsFocus = pointerInside && (EventSystem.current == null
            || (button != null && EventSystem.current.currentSelectedGameObject == button.gameObject));
        bool active = button != null && button.IsActive() && button.IsInteractable() && (pointerOwnsFocus || keyboardSelected);
        float target = active ? 1f : 0f;
        float nextVisibility = Mathf.MoveTowards(visibility, target, Time.unscaledDeltaTime / TransitionDuration);
        if (!Mathf.Approximately(visibility, nextVisibility))
        {
            visibility = nextVisibility;
            ApplyFeedback();
        }
    }

    private void ApplyFeedback()
    {
        ApplyOpacity(highlight, highlightOpacity);
        ApplyOpacity(underline, underlineOpacity);
        if (label != null)
        {
            label.fontStyle = visibility > 0.5f ? normalFontStyle | FontStyles.Bold : normalFontStyle;
        }
    }

    private void ApplyOpacity(Image graphic, float opacity)
    {
        if (graphic == null)
        {
            return;
        }

        Color color = graphic.color;
        color.a = opacity * visibility;
        graphic.color = color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        // 悬停同时更新菜单焦点，使原选中项取消高亮并保持单项反馈。
        if (button != null && button.IsActive() && button.IsInteractable() && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        keyboardSelected = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        keyboardSelected = false;
    }

    private void OnDisable()
    {
        pointerInside = false;
        keyboardSelected = false;
        visibility = 0f;
        ApplyFeedback();
    }
}
