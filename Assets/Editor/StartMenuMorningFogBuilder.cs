using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>将已确认的花谷设计接入真实开始场景，保留原界面与既有游戏、音频绑定。</summary>
[InitializeOnLoad]
public static class StartMenuMorningFogBuilder
{
    private const string ScenePath = "Assets/Scenes/start.unity";
    private const string ResourceFolder = "Assets/UI/StartMenu";
    private const string BackgroundPath = "Assets/Images/Xingyu_MainMenu_MorningFog_Background.png";
    private const string CommandPath = "Library/StartMenuBuild.command";
    private const string Output = "Temp/EditorTools/开始界面UI实施_20261006";
    private static double nextPoll;
    private static readonly Color Ink = Hex("354C43");
    private static readonly Color Muted = Hex("455D54");
    private static readonly Color Gold = Hex("D6A345");
    private static readonly Color Paper = Hex("F8F5E9");
    private static TMP_FontAsset menuFont;
    private static Sprite highlightSprite;
    private static Sprite roundedSprite;

    static StartMenuMorningFogBuilder() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (!File.Exists(CommandPath)) return;
        string command = File.ReadAllText(CommandPath).Trim();
        if (command.Length == 0) return;
        File.Delete(CommandPath);
        Directory.CreateDirectory(Output);
        try
        {
            if (command == "build") Build();
            else throw new InvalidOperationException("未知开始界面命令：" + command);
            File.WriteAllText(Output + "/build-result.txt", command + " OK " + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception error)
        {
            File.WriteAllText(Output + "/build-result.txt", command + " FAILED " + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("工具/星屿/开始界面/应用晨雾花谷设计")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || SceneManager.sceneCount != 1)
            throw new InvalidOperationException("请在编辑状态中仅打开 start 场景，再应用开始界面设计。");
        if (!File.Exists(BackgroundPath)) throw new FileNotFoundException("缺少交接指定的无标题背景。", BackgroundPath);
        Canvas canvas = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).Single();
        StartMenuAudioSettings audio = canvas.GetComponent<StartMenuAudioSettings>();
        if (audio == null || audio.startButton == null || audio.settingsButton == null || audio.audioMixer == null)
            throw new InvalidOperationException("开始界面原有绑定不完整，不能直接改造。");
        Directory.CreateDirectory(Output + "/备份");
        if (!File.Exists(Output + "/备份/start_内存场景.unity") && !EditorSceneManager.SaveScene(scene, Output + "/备份/start_内存场景.unity", true))
            throw new InvalidOperationException("开始界面内存备份失败。");
        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "接入花谷开始界面");
        Directory.CreateDirectory(ResourceFolder);
        AssetDatabase.Refresh();
        menuFont = CreateMenuFont();
        highlightSprite = CreateShape("菜单柔光", 600, 96, true, false);
        roundedSprite = CreateShape("圆角底板", 64, 64, false, false);
        Sprite orbit = CreateShape("标题微光", 560, 260, false, true);
        Sprite background = ImportSprite(BackgroundPath, 2048, Vector4.zero);
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 使用线性色彩顶点，避免旧 TMP 着色器将网页墨绿显示成浅灰绿。
        canvas.vertexColorAlwaysGammaSpace = false;

        RectTransform canvasRect = (RectTransform)canvas.transform;
        RectTransform main = FindRect(canvasRect, "花谷主菜单");
        if (main == null)
        {
            Transform[] oldChildren = canvasRect.Cast<Transform>().ToArray();
            RectTransform legacy = Rect(canvasRect, "旧版开始界面_保留");
            Stretch(legacy);
            foreach (Transform child in oldChildren) child.SetParent(legacy, false);
            legacy.gameObject.SetActive(false);
            main = Rect(canvasRect, "花谷主菜单");
        }
        Stretch(main);
        CanvasGroup menuGroup = Get<CanvasGroup>(main.gameObject);
        menuGroup.alpha = 1;
        menuGroup.interactable = true;
        menuGroup.blocksRaycasts = true;
        Image bg = Picture(canvasRect, "花谷背景_无标题原图", background, Color.white);
        Stretch(bg.rectTransform);
        AspectRatioFitter fitter = Get<AspectRatioFitter>(bg.gameObject);
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 1672f / 941f;
        bg.transform.SetAsFirstSibling();

        RectTransform brand = Rect(main, "标题区域");
        Place(brand, new Vector2(0.087f, 0.89f), Vector2.zero, new Vector2(680, 358));
        Text(brand, "拼音", "X I N G Y U", new Vector2(0, 0), new Vector2(430, 40), 25, Muted, 16);
        TMP_Text title = Text(brand, "标题_星屿", "星屿", new Vector2(-9, -38), new Vector2(480, 282), 184, Ink, 4);
        title.fontStyle = FontStyles.Normal;
        Picture(brand, "标题微光轨迹", orbit, Color.white).rectTransform.SetAsLastSibling();
        Place((RectTransform)brand.Find("标题微光轨迹"), new Vector2(0, 1), new Vector2(-48, -68), new Vector2(560, 260));
        Text(brand, "副标题", "循着微光，找回自己。", new Vector2(0, -306), new Vector2(680, 58), 34, Muted, 3);
        RectTransform menu = Rect(main, "文字菜单");
        Place(menu, new Vector2(0.061f, 0.51f), Vector2.zero, new Vector2(600, 336));
        StyleMenuButton(audio.startButton, menu, 0, "开始游戏", true);
        StyleMenuButton(audio.settingsButton, menu, 1, "设置", false);
        Button exit = ChildButton(menu, "退出游戏按钮");
        StyleMenuButton(exit, menu, 2, "退出游戏", false);
        TMP_Text footer = Text(main, "底部文案", "探索 · 收集 · 重拾光芒", Vector2.zero, new Vector2(640, 48), 26, Muted, 4);
        Place(footer.rectTransform, new Vector2(0.087f, 0.08f), new Vector2(0, 0), new Vector2(640, 48));
        footer.rectTransform.pivot = new Vector2(0, 0);

        RectTransform settingsOverlay = Overlay(canvasRect, "设置弹窗");
        RectTransform settingsCard = Card(settingsOverlay, "音频设置面板", new Vector2(624, 520));
        Text(settingsCard, "弹窗标题", "设置", new Vector2(48, -36), new Vector2(528, 66), 44, Ink, 4);
        SetupSlider(settingsCard, audio.musicSlider, audio.musicPercentLabel, "音乐", -134);
        SetupSlider(settingsCard, audio.sfxSlider, audio.sfxPercentLabel, "音效", -264);
        Button close = DialogButton(settingsCard, "返回按钮", "返回", new Vector2(408, -416), false);
        audio.closeButton = close;
        audio.menuGroup = menuGroup;
        audio.settingsPanel = settingsOverlay.gameObject;
        audio.settingsButtonLabel = audio.settingsButton.GetComponentInChildren<TMP_Text>(true);
        settingsOverlay.gameObject.SetActive(false);

        RectTransform exitOverlay = Overlay(canvasRect, "退出确认弹窗");
        RectTransform exitCard = Card(exitOverlay, "退出确认面板", new Vector2(624, 370));
        Text(exitCard, "弹窗标题", "退出游戏", new Vector2(48, -38), new Vector2(528, 64), 44, Ink, 4);
        Text(exitCard, "确认文案", "要暂时离开这段旅程吗？", new Vector2(48, -132), new Vector2(528, 64), 27, Muted);
        Button cancel = DialogButton(exitCard, "取消按钮", "取消", new Vector2(214, -258), false);
        Button confirm = DialogButton(exitCard, "确认退出按钮", "退出", new Vector2(408, -258), true);
        StartMenuPresentation presentation = Get<StartMenuPresentation>(canvas.gameObject);
        presentation.startButton = audio.startButton;
        presentation.settingsButton = audio.settingsButton;
        presentation.exitButton = exit;
        presentation.menuGroup = menuGroup;
        presentation.audioSettings = audio;
        presentation.exitPanel = exitOverlay.gameObject;
        presentation.cancelExitButton = cancel;
        presentation.confirmExitButton = confirm;
        exitOverlay.gameObject.SetActive(false);
        SetNavigation(new Selectable[] { audio.startButton, audio.settingsButton, exit }, false);
        SetNavigation(new Selectable[] { audio.musicSlider, audio.sfxSlider, close }, true);
        SetNavigation(new Selectable[] { cancel, confirm }, true);
        var eventSystem = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).Single();
        eventSystem.firstSelectedGameObject = audio.startButton.gameObject;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("start 场景保存失败。");
        File.WriteAllText(Output + "/场景接入记录.json", JsonUtility.ToJson(new BuildRecord
        {
            scene = scene.path, background = BackgroundPath, font = AssetDatabase.GetAssetPath(menuFont),
            startHandler = audio.startButton.GetComponent<UIControl>().gameSceneName,
            startPersistentHandlers = audio.startButton.onClick.GetPersistentEventCount(),
            mixer = AssetDatabase.GetAssetPath(audio.audioMixer), legacyPreserved = main.parent.Find("旧版开始界面_保留") != null
        }, true));
        Debug.Log("花谷开始界面已保存：独立标题、无图标文字菜单、音频设置和退出确认已接入。");
    }

    [Serializable] private class BuildRecord { public string scene, background, font, startHandler, mixer; public int startPersistentHandlers; public bool legacyPreserved; }
    private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color color); return color; }
    private static T Get<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component == null ? go.AddComponent<T>() : component;
    }
    private static RectTransform FindRect(Transform parent, string name) { return parent.Find(name) as RectTransform; }
    private static RectTransform Rect(Transform parent, string name)
    {
        RectTransform rect = FindRect(parent, name);
        if (rect == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "创建花谷界面元素");
            rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
        }
        rect.gameObject.layer = 5;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return rect;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }
    private static Image Picture(Transform parent, string name, Sprite sprite, Color color)
    {
        Image image = Get<Image>(Rect(parent, name).gameObject);
        image.sprite = sprite; image.color = color; image.raycastTarget = false; image.type = Image.Type.Simple;
        return image;
    }
    private static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color, float spacing = 0)
    {
        TextMeshProUGUI text = Get<TextMeshProUGUI>(Rect(parent, name).gameObject);
        Place(text.rectTransform, new Vector2(0, 1), position, size);
        text.font = menuFont; text.fontSharedMaterial = menuFont.material; text.text = value; text.fontSize = fontSize;
        text.fontStyle = FontStyles.Normal; text.color = color; text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = false; text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Overflow;
        text.characterSpacing = spacing; text.raycastTarget = false; text.margin = Vector4.zero;
        return text;
    }
    private static Button ChildButton(Transform parent, string name) { return Get<Button>(Rect(parent, name).gameObject); }
    private static void StyleMenuButton(Button button, Transform parent, int index, string caption, bool initial)
    {
        button.transform.SetParent(parent, false);
        button.gameObject.SetActive(true);
        Place((RectTransform)button.transform, new Vector2(0, 1), new Vector2(0, -index * 120), new Vector2(600, 96));
        Image hit = Get<Image>(button.gameObject); hit.sprite = null; hit.color = Color.clear; hit.raycastTarget = true;
        button.targetGraphic = hit; button.transition = Selectable.Transition.None;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        string labelName = label != null ? label.name : "菜单文字";
        label = Text(button.transform, labelName, caption, new Vector2(130, 0), new Vector2(440, 90), 42, Ink, 2);
        Image highlight = Picture(button.transform, "柔光高亮", highlightSprite, Color.white);
        Stretch(highlight.rectTransform); highlight.transform.SetAsFirstSibling();
        Image underline = Picture(button.transform, "金色下划线", null, Gold);
        Place(underline.rectTransform, new Vector2(0, 1), new Vector2(134, -79), new Vector2(caption.Length * 27, 3));
        BindFeedback(button, highlight, underline, label, initial);
    }
    private static void BindFeedback(Button button, Image highlight, Image underline, TMP_Text label, bool initial)
    {
        StartMenuButtonFeedback feedback = Get<StartMenuButtonFeedback>(button.gameObject);
        feedback.button = button; feedback.highlight = highlight; feedback.underline = underline; feedback.label = label; feedback.selectedByDefault = initial;
        feedback.highlightAlpha = highlight.color.a;
        feedback.underlineAlpha = underline.color.a;
        Color highlightColor = highlight.color; highlightColor.a = initial ? feedback.highlightAlpha : 0; highlight.color = highlightColor;
        Color underlineColor = underline.color; underlineColor.a = initial ? feedback.underlineAlpha : 0; underline.color = underlineColor;
    }
    private static RectTransform Overlay(Transform parent, string name)
    {
        Image overlay = Picture(parent, name, null, new Color(0.149f, 0.243f, 0.271f, 0.50f));
        Stretch(overlay.rectTransform); overlay.raycastTarget = true; overlay.transform.SetAsLastSibling();
        return overlay.rectTransform;
    }
    private static RectTransform Card(Transform parent, string name, Vector2 size)
    {
        Image card = Picture(parent, name, roundedSprite, Paper);
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        card.rectTransform.pivot = new Vector2(0.5f, 0.5f); card.type = Image.Type.Sliced; card.raycastTarget = true;
        Shadow shadow = Get<Shadow>(card.gameObject); shadow.effectColor = new Color(0.16f, 0.25f, 0.22f, 0.16f); shadow.effectDistance = new Vector2(0, -10);
        return card.rectTransform;
    }
    private static Button DialogButton(Transform parent, string name, string caption, Vector2 position, bool primary)
    {
        Button button = ChildButton(parent, name);
        Place((RectTransform)button.transform, new Vector2(0, 1), position, new Vector2(166, 66));
        Image face = Get<Image>(button.gameObject); face.sprite = roundedSprite; face.type = Image.Type.Sliced; face.color = primary ? Ink : new Color(0.21f, 0.30f, 0.26f, 0.07f); face.raycastTarget = true;
        button.targetGraphic = face; button.transition = Selectable.Transition.None;
        TMP_Text label = Text(button.transform, "文字", caption, Vector2.zero, new Vector2(166, 62), 28, primary ? Paper : Ink);
        label.alignment = TextAlignmentOptions.Center;
        Image focus = Picture(button.transform, "焦点柔光", roundedSprite, new Color(0.84f, 0.64f, 0.27f, primary ? 0.20f : 0.18f));
        Stretch(focus.rectTransform); focus.type = Image.Type.Sliced; focus.transform.SetSiblingIndex(1);
        Image line = Picture(button.transform, "焦点下划线", null, Gold);
        Place(line.rectTransform, new Vector2(0, 1), new Vector2(24, -56), new Vector2(118, 3));
        BindFeedback(button, focus, line, label, false);
        return button;
    }
    private static void SetupSlider(RectTransform card, Slider slider, TMP_Text percent, string caption, float top)
    {
        slider.transform.SetParent(card, false);
        slider.gameObject.SetActive(true);
        Place((RectTransform)slider.transform, new Vector2(0, 1), new Vector2(48, top - 46), new Vector2(528, 48));
        Image hit = Get<Image>(slider.gameObject); hit.sprite = null; hit.color = Color.clear; hit.raycastTarget = true;
        slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
        Text(card, caption + "标签", caption, new Vector2(48, top), new Vector2(250, 46), 29, Ink);
        percent.transform.SetParent(card, false);
        TMP_Text value = Text(card, percent.name, "100%", new Vector2(408, top), new Vector2(168, 46), 27, Muted);
        value.alignment = TextAlignmentOptions.MidlineRight;
        Image track = slider.transform.Find("背景")?.GetComponent<Image>();
        if (track != null) { track.sprite = roundedSprite; track.type = Image.Type.Sliced; track.color = new Color(0.21f, 0.30f, 0.26f, 0.20f); track.raycastTarget = false; Stretch(track.rectTransform); track.rectTransform.offsetMin = new Vector2(12, 20); track.rectTransform.offsetMax = new Vector2(-12, -20); }
        if (slider.fillRect != null)
        {
            RectTransform area = (RectTransform)slider.fillRect.parent;
            area.anchorMin = new Vector2(0, 0.5f); area.anchorMax = new Vector2(1, 0.5f);
            area.pivot = new Vector2(0.5f, 0.5f); area.anchoredPosition = Vector2.zero; area.sizeDelta = new Vector2(-26, 8);
            slider.fillRect.offsetMin = slider.fillRect.offsetMax = Vector2.zero;
            Image fill = slider.fillRect.GetComponent<Image>(); fill.sprite = roundedSprite; fill.type = Image.Type.Sliced; fill.color = Gold; fill.raycastTarget = false;
        }
        if (slider.handleRect != null)
        {
            RectTransform area = (RectTransform)slider.handleRect.parent;
            area.anchorMin = new Vector2(0, 0.5f); area.anchorMax = new Vector2(1, 0.5f);
            area.pivot = new Vector2(0.5f, 0.5f); area.anchoredPosition = Vector2.zero; area.sizeDelta = new Vector2(-26, 0);
            slider.handleRect.anchorMin = slider.handleRect.anchorMax = new Vector2(slider.normalizedValue, 0.5f);
            slider.handleRect.pivot = new Vector2(0.5f, 0.5f); slider.handleRect.anchoredPosition = Vector2.zero;
            Image handle = slider.handleRect.GetComponent<Image>(); handle.sprite = roundedSprite; handle.type = Image.Type.Simple; handle.color = Paper; handle.raycastTarget = true;
            slider.handleRect.sizeDelta = new Vector2(26, 26); Outline outline = Get<Outline>(handle.gameObject); outline.effectColor = Gold; outline.effectDistance = new Vector2(2, 2);
        }
        slider.colors = new ColorBlock { normalColor = Paper, highlightedColor = Hex("FFF8E8"), pressedColor = Gold, selectedColor = Hex("FFE7B4"), disabledColor = Muted, colorMultiplier = 1, fadeDuration = 0.18f };
    }
    private static void SetNavigation(Selectable[] controls, bool horizontal)
    {
        for (int i = 0; i < controls.Length; i++)
        {
            Navigation navigation = new Navigation { mode = Navigation.Mode.Explicit };
            navigation.selectOnUp = controls[(i + controls.Length - 1) % controls.Length];
            navigation.selectOnDown = controls[(i + 1) % controls.Length];
            if (horizontal && !(controls[i] is Slider)) { navigation.selectOnLeft = navigation.selectOnUp; navigation.selectOnRight = navigation.selectOnDown; }
            controls[i].navigation = navigation;
        }
    }
    private static TMP_FontAsset CreateMenuFont()
    {
        string path = ResourceFolder + "/花谷菜单字体.asset";
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font != null && font.material != null && font.atlasTextures.Length > 0 && font.atlasTextures[0] != null) return font;
        TMP_FontAsset source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/SourceHanSansSC-Bold SDF.asset");
        TMP_FontAsset generated = TMP_FontAsset.CreateFontAsset(source.sourceFontFile, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        if (font != null)
        {
            EditorUtility.CopySerialized(generated, font);
            UnityEngine.Object.DestroyImmediate(generated);
        }
        else font = generated;
        font.name = "花谷菜单字体";
        font.material.SetFloat(ShaderUtilities.ID_FaceDilate, -0.06f);
        if (!AssetDatabase.Contains(font)) AssetDatabase.CreateAsset(font, path);
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (Texture2D atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
        font.TryAddCharacters("星屿循着微光，找回自己。探索 · 收集 · 重拾光芒开始游戏设置退出音乐效返回要暂时离这段旅程吗？取消XINGYU0123456789%", out _);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        return font;
    }
    private static Sprite ImportSprite(string path, int maxSize, Vector4 border)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100; importer.spriteBorder = border; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = maxSize;
        importer.alphaIsTransparency = true; importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    /// <summary>程序绘制柔光与装饰形状，背景始终使用交接指定的原始 PNG。</summary>
    private static Sprite CreateShape(string name, int width, int height, bool gradient, bool decoration)
    {
        string path = ResourceFolder + "/" + name + ".png";
        if (!File.Exists(path))
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float alpha;
                Color color = gradient ? Hex("FFF8E8") : Color.white;
                if (decoration)
                {
                    float nx = (x - 258f) / 244f, ny = (y - 120f - (x - 258f) * 0.12f) / 93f;
                    float radial = Mathf.Sqrt(nx * nx + ny * ny);
                    float arc = Mathf.Clamp01(1.2f - Mathf.Abs(radial - 1) * 100f) * (ny < 0.12f ? 1f : 0f);
                    float glint1 = Mathf.Clamp01(1 - (Mathf.Abs(x - 490f) / 12 + Mathf.Abs(y - 220f) / 22));
                    float glint2 = Mathf.Clamp01(1 - (Mathf.Abs(x - 20f) / 8 + Mathf.Abs(y - 65f) / 14));
                    alpha = Mathf.Max(arc * 0.85f, Mathf.Max(glint1, glint2)); color = Hex("E8B660");
                }
                else
                {
                    float radius = gradient ? 48 : 8;
                    float dx = Mathf.Max(radius - x, x - (width - radius));
                    float dy = Mathf.Max(radius - y, y - (height - radius));
                    alpha = Mathf.Clamp01(radius + 0.5f - new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude);
                    if (gradient) alpha *= 0.91f * (1 - Mathf.SmoothStep(0, 1, x / (float)(width - 1)));
                }
                color.a = alpha; pixels[y * width + x] = color;
            }
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
        return ImportSprite(path, 1024, decoration || gradient ? Vector4.zero : new Vector4(10, 10, 10, 10));
    }
}
