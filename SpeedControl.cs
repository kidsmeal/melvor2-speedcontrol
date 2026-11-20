using System;
using System.Diagnostics;
using System.IO;
using Godot;
using MelvorModLoader;

namespace SpeedControl;

public class SpeedControlMod : IMod
{
    int[] speeds = { 1, 2, 4, 8, 10 };
    const int gameTicksPerSecond = 60;
    const double maxFrameTime = 0.25;

    ModContext ctx;
    int speed = 1;
    bool paused;
    double extraTicksOwed;
    Stopwatch clock = new Stopwatch();

    Button icon;
    Control popup;
    Button pauseButton;
    Texture2D pauseTex, playTex;
    Node driver;

    public void OnLoad(ModContext ctx)
    {
        this.ctx = ctx;
        string savedSpeedFile = Path.Combine(ctx.Directory, "speed.txt");
        if (File.Exists(savedSpeedFile))
        {
            string fileText = File.ReadAllText(savedSpeedFile);
            bool isNumber = int.TryParse(fileText, out int savedSpeed);
            bool isValidSpeed = Array.IndexOf(speeds, savedSpeed) >= 0;
            if (isNumber && isValidSpeed) speed = savedSpeed;
        }
        ctx.Frame += Update;
    }

    void Update()
    {
        double dt = clock.Elapsed.TotalSeconds;
        clock.Restart();

        if (popup == null) BuildPopup();

        var game = GameAccess.CurrentGame;
        if (game == null || !game.IsSaveLoaded)
        {
            popup.Visible = false;
            return;
        }

        // The footer and GameDriver get rebuilt when a save loads, so look again when they go away.
        if (Engine.GetProcessFrames() % 30 == 0)
        {
            if (!GodotObject.IsInstanceValid(icon))
            {
                icon = Footer.AddIcon(ctx.Tree, "SpeedControlIcon", "res://Assets/Icons/TimeInterval_48.png", null);
                if (icon != null) icon.Pressed += () => Footer.Toggle(popup, icon);
            }
            if (!GodotObject.IsInstanceValid(driver))
                driver = Footer.Find(ctx.Tree.Root, "GameDriver");
        }

        // GameDriver._PhysicsProcess is what calls PropagateTick, so switching it off pauses the game.
        if (paused)
        {
            if (driver != null && driver.IsPhysicsProcessing()) driver.SetPhysicsProcess(false);
            return;
        }

        if (speed == 1 || GameAccess.IsComputingOffline || ctx.Tree.Paused)
        {
            extraTicksOwed = 0;
            return;
        }

        int speedMultiplier = speed - 1;
        double frameTime = Math.Min(dt, maxFrameTime);
        extraTicksOwed += frameTime * gameTicksPerSecond * speedMultiplier;
        while (extraTicksOwed >= 1)
        {
            game.PropagateTick();
            extraTicksOwed--;
        }
    }

    void BuildPopup()
    {
        pauseTex = GD.Load<Texture2D>("res://Assets/Icons/Pause.png");
        playTex = GD.Load<Texture2D>("res://Assets/Icons/Play.png");

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 3);

        pauseButton = new Button();
        pauseButton.Icon = pauseTex;
        pauseButton.ExpandIcon = true;
        pauseButton.ToggleMode = true;
        pauseButton.CustomMinimumSize = new Vector2(34, 30);
        Footer.Style(pauseButton);
        pauseButton.Toggled += SetPaused;
        row.AddChild(pauseButton);

        var group = new ButtonGroup();
        foreach (int s in speeds)
        {
            var b = new Button();
            b.Text = s + "x";
            b.ToggleMode = true;
            b.ButtonGroup = group;
            b.ButtonPressed = s == speed;
            b.CustomMinimumSize = new Vector2(40, 30);
            Footer.Style(b);
            b.Pressed += () => SetSpeed(s);
            row.AddChild(b);
        }

        popup = Footer.MakePopup(ctx.Tree, row);
    }

    void SetPaused(bool on)
    {
        paused = on;
        extraTicksOwed = 0;
        pauseButton.Icon = on ? playTex : pauseTex;
        if (driver != null) driver.SetPhysicsProcess(!on);
        ctx.Log(on ? "paused" : "resumed");
    }

    void SetSpeed(int s)
    {
        speed = s;
        extraTicksOwed = 0;
        File.WriteAllText(Path.Combine(ctx.Directory, "speed.txt"), s.ToString());
        ctx.Log("speed " + s + "x");
    }
}
