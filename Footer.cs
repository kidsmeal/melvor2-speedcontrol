using Godot;

namespace SpeedControl;

static class Footer
{
    public static readonly Color Gold = new Color("ffc100");

    static StyleBox normal, hover, pressed, hoverPressed, disabled, dark;

    static StyleBox Load(string name) => GD.Load<StyleBox>("res://Assets/UI/StyleBoxes/" + name);

    static void LoadStyles()
    {
        if (normal != null) return;
        normal = Load("btn_textured_normal_8x.tres");
        hover = Load("btn_textured_hover_8x.tres");
        pressed = Load("btn_textured_pressed_8x.tres");
        hoverPressed = Load("btn_textured_hover_pressed_8x.tres");
        disabled = Load("btn_textured_disabled_8x.tres");
        dark = Load("btn_textured_normal_dark_8x.tres");
    }

    public static void Style(Button b)
    {
        LoadStyles();
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeStyleboxOverride("hover_pressed", hoverPressed);
        b.AddThemeStyleboxOverride("disabled", disabled);
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        b.AddThemeColorOverride("font_pressed_color", Gold);
        b.AddThemeColorOverride("font_hover_pressed_color", Gold);
        b.AddThemeColorOverride("icon_pressed_color", Gold);
        b.AddThemeColorOverride("icon_hover_pressed_color", Gold);
        b.FocusMode = Control.FocusModeEnum.None;
    }

    public static Node Find(Node node, string typeName)
    {
        if (node.GetType().Name == typeName) return node;
        foreach (var child in node.GetChildren())
        {
            var hit = Find(child, typeName);
            if (hit != null) return hit;
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
                img.SetPixel(x, y, new Color(Gold, img.GetPixel(x, y).A));
        return ImageTexture.CreateFromImage(img);
    }

    // Puts an icon button left of the music button, styled like it.
    // Returns null until the footer exists.
    public static Button AddIcon(SceneTree tree, string name, string iconPath, string before)
    {
        var music = Find(tree.Root, "MusicButton") as Control;
        if (music == null) return null;
        var bar = music.GetParent();

        var icon = new Button();
        icon.Name = name;
        icon.Icon = GoldTexture(iconPath);
        icon.ExpandIcon = true;
        icon.IconAlignment = HorizontalAlignment.Center;
        icon.CustomMinimumSize = new Vector2(music.Size.Y, music.Size.Y);
        icon.SizeFlagsVertical = music.SizeFlagsVertical;
        icon.FocusMode = Control.FocusModeEnum.None;
        icon.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        var box = music.GetThemeStylebox("panel");
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
            icon.AddThemeStyleboxOverride(state, box);
        icon.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        icon.AddThemeColorOverride("icon_hover_color", new Color(1.25f, 1.25f, 1.25f));

        bar.AddChild(icon);
        var next = before != null ? bar.GetNodeOrNull<Control>(before) : null;
        bar.MoveChild(icon, (next ?? music).GetIndex());
        return icon;
    }

    // A panel shown above the icon. Clicking anywhere outside it closes it.
    public static Control MakePopup(SceneTree tree, Control content)
    {
        var layer = new CanvasLayer();
        layer.Layer = 110;

        var screen = new Control();
        screen.Visible = false;
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.GuiInput += e =>
        {
            if (e is InputEventMouseButton mb && mb.Pressed) screen.Visible = false;
        };

        LoadStyles();
        var panel = new PanelContainer();
        panel.Name = "Panel";
        panel.AddThemeStyleboxOverride("panel", dark);
        panel.AddChild(content);

        screen.AddChild(panel);
        layer.AddChild(screen);
        tree.Root.CallDeferred(Node.MethodName.AddChild, layer);
        return screen;
    }

    public static void Toggle(Control popup, Control icon)
    {
        popup.Visible = !popup.Visible;
        if (!popup.Visible) return;

        var panel = popup.GetNode<PanelContainer>("Panel");
        panel.Size = panel.GetCombinedMinimumSize();
        var r = icon.GetGlobalRect();
        panel.Position = new Vector2(r.End.X - panel.Size.X, r.Position.Y - panel.Size.Y - 6);
    }
}
