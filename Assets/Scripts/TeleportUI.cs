using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>靠近灯塔按 F 打开目的地，设置菜单优先，E 保留给记忆阅读。</summary>
[DefaultExecutionOrder(-150)]
public sealed class TeleportUI : MonoBehaviour
{
    public static TeleportUI Instance { get; private set; }
    public IslandTravel travel;
    public GameObject panel;
    public TMP_Text title;
    public TMP_Text hint;
    public TMP_Text notification;
    public Button[] destinations;
    public TMP_Text[] labels;
    public Button closeButton;
    private LighthouseController source;
    private float notificationTime;
    private int closedFrame = -1;
    private bool open;
    public bool IsOpen => open;

    private void Awake() { Instance = this; panel.SetActive(false); }
    private void Start()
    {
        destinations[0].onClick.AddListener(() => Select(0));
        destinations[1].onClick.AddListener(() => Select(1));
        closeButton.onClick.AddListener(Close);
    }
    private void Update()
    {
        // 灯塔销毁后 Unity 引用会表现为 null，仍须释放面板持有的暂停。
        if (IsOpen && source == null) Close();
        notificationTime -= Time.deltaTime;
        notification.gameObject.SetActive(notificationTime > 0f && !GameplayPause.IsPaused);
        if (SettingsMenu.IsPaused) { hint.text = ""; return; }
        if (IsOpen)
        {
            hint.text = "";
            if (Input.GetKeyDown(KeyCode.F)) Close();
            return;
        }
        LighthouseController nearby = FindNearby();
        hint.text = nearby != null ? nearby.ProgressHint + "\nF：选择目的地" : "";
        if (nearby != null && !GameplayPause.IsBlocked && Time.frameCount != closedFrame && Input.GetKeyDown(KeyCode.F)) Open(nearby);
    }

    public LighthouseController FindNearby()
    {
        foreach (LighthouseController lighthouse in travel.lighthouses)
            if (lighthouse != null && lighthouse.island == travel.CurrentIsland && lighthouse.IsNear(travel.player.transform.position)) return lighthouse;
        return null;
    }
    public bool Open(LighthouseController lighthouse)
    {
        if (lighthouse == null || SettingsMenu.IsPaused || IsOpen || lighthouse.island != travel.CurrentIsland || !lighthouse.IsNear(travel.player.transform.position)) return false;
        source = lighthouse;
        open = true;
        title.text = $"{source.island} 岛灯塔 · {source.ProgressHint}";
        for (int index = 0; index < destinations.Length; index++)
        {
            int target = Destination(index);
            destinations[index].gameObject.SetActive(target > 0);
            if (target == 0) continue;
            bool allowed = travel.CanTravel(source.island, target);
            destinations[index].interactable = allowed;
            labels[index].text = allowed ? $"前往第 {target} 岛" : target == 3 ? "第三岛未解锁：收齐前两岛 01～13" : "第二岛未解锁：收齐第一岛 01～05";
        }
        panel.SetActive(true);
        hint.text = "";
        GameplayPause.Set(this, true);
        return true;
    }
    private int Destination(int index) => source.island == 2 ? (index == 0 ? 1 : 3) : index == 0 ? 2 : 0;
    private void Select(int index) { if (source != null) travel.TryTravel(source.island, Destination(index)); }
    public void Close()
    {
        if (!IsOpen) return;
        open = false;
        source = null;
        if (panel != null) panel.SetActive(false);
        closedFrame = Time.frameCount;
        GameplayPause.Set(this, false);
    }
    public void Notify(string text, float seconds = 5f) { notification.text = text; notificationTime = seconds; }
    private void OnDisable() { Close(); }
    private void OnDestroy() { Close(); if (Instance == this) Instance = null; }
}
