using Godot;

namespace SpeedControl;

// styles the footer buttons and provides a way to add new ones.
static class Footer
{
    public static readonly Color Gold = new Color("ffc100");

    const int popupLayer = 110;
    const float popupMargin = 6;
    const float hoverBrightness = 1.25f;
    static StyleBox normal, hover, pressed, hoverPressed, disabled, dark;
    static StyleBox LoadStyleBox(string fileName) => GD.Load<StyleBox>("res://Assets/UI/StyleBoxes/" + fileName);
    
    // loads the style buttons and does nothing if theyre loaded already.
    static void LoadStyles()
    {
        if (normal != null) return;
        normal = LoadStyleBox("btn_textured_normal_8x.tres");
        hover = LoadStyleBox("btn_textured_hover_8x.tres");
        pressed = LoadStyleBox("btn_textured_pressed_8x.tres");
        hoverPressed = LoadStyleBox("btn_textured_hover_pressed_8x.tres");
        disabled = LoadStyleBox("btn_textured_disabled_8x.tres");
        dark = LoadStyleBox("btn_textured_normal_dark_8x.tres");
    }

    public static void Style(Button button)
    {
        LoadStyles();
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("hover_pressed", hoverPressed);
        button.AddThemeStyleboxOverride("disabled", disabled);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_pressed_color", Gold);
        button.AddThemeColorOverride("font_hover_pressed_color", Gold);
        button.AddThemeColorOverride("icon_pressed_color", Gold);
        button.AddThemeColorOverride("icon_hover_pressed_color", Gold);
        button.FocusMode = Control.FocusModeEnum.None;
    }

    public static Node Find(Node node, string typeName)
    {
        if (node.GetType().Name == typeName) return node;
        foreach (var child in node.GetChildren())
        {
            var match = Find(child, typeName);
            if (match != null) return match;
        }
        return null;
    }

    static Texture2D GoldTexture(string path)
    {
        var img = GD.Load<Texture2D>(path).GetImage();
        img.Decompress();
        img.Convert(Image.Format.Rgba8);
        for (int y = 0; y < img.GetHeight(); y++)
            for (int x = 0; x < img.GetWidth(); x++)
            {
                float alpha = img.GetPixel(x, y).A;
                img.SetPixel(x, y, new Color(Gold, alpha));
            }
        return ImageTexture.CreateFromImage(img);
    }

    // Puts an icon button left of the music button, styled like it.
    // Returns null until the footer exists.
    public static Button AddIcon(SceneTree tree, string name, string iconPath, string beforeIconName)
    {
        var musicButton = Find(tree.Root, "MusicButton") as Control;
        if (musicButton == null) return null;
        var footerBar = musicButton.GetParent();

        var icon = new Button();
        icon.Name = name;
        icon.Icon = GoldTexture(iconPath);
        icon.ExpandIcon = true;
        icon.IconAlignment = HorizontalAlignment.Center;
        float size = musicButton.Size.Y;
        icon.CustomMinimumSize = new Vector2(size, size);
        icon.SizeFlagsVertical = musicButton.SizeFlagsVertical;
        icon.FocusMode = Control.FocusModeEnum.None;
        icon.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        var musicStyle = musicButton.GetThemeStylebox("panel");
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
            icon.AddThemeStyleboxOverride(state, musicStyle);
        icon.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        icon.AddThemeColorOverride("icon_hover_color", new Color(hoverBrightness, hoverBrightness, hoverBrightness));

        footerBar.AddChild(icon);
        var placeBefore = beforeIconName != null ? footerBar.GetNodeOrNull<Control>(beforeIconName) : null;
        footerBar.MoveChild(icon, (placeBefore ?? musicButton).GetIndex());
        return icon;
    }

    // A panel shown above the icon. Clicking anywhere outside it closes it.
    public static Control MakePopup(SceneTree tree, Control content)
    {
        var layer = new CanvasLayer();
        layer.Layer = popupLayer;

        var overlay = new Control();
        overlay.Visible = false;
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.GuiInput += inputEvent =>
        {
            if (inputEvent is InputEventMouseButton mb && mb.Pressed) overlay.Visible = false;
        };

        LoadStyles();
        var panel = new PanelContainer();
        panel.Name = "Panel";
        panel.AddThemeStyleboxOverride("panel", dark);
        panel.AddChild(content);

        overlay.AddChild(panel);
        layer.AddChild(overlay);
        tree.Root.CallDeferred(Node.MethodName.AddChild, layer);
        return overlay;
    }

    public static void Toggle(Control popup, Control icon)
    {
        popup.Visible = !popup.Visible;
        if (!popup.Visible) return;

        var panel = popup.GetNode<PanelContainer>("Panel");
        panel.Size = panel.GetCombinedMinimumSize();
        var iconRect = icon.GetGlobalRect();
        float x = iconRect.End.X - panel.Size.X;
        float y = iconRect.Position.Y - panel.Size.Y - popupMargin;
        panel.GlobalPosition = new Vector2(x, y);
    }
}
