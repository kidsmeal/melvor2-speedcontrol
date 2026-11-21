using System;
using System.Diagnostics;
using System.IO;
using Godot;
using MelvorModLoader;

namespace SpeedControl;

public class SpeedControlMod : IMod
{
    static readonly int[] speeds = { 1, 2, 4, 8, 10 };
    const int gameTicksPerSecond = 60;
    const double maxFrameTime = 0.25;
    const int nodeCheckInterval = 30; // frames between checks for the footer and GameDriver nodes.
    const int missedChecksBeforeLog = 10; // failed node checks in a row before logging, about 5s at 60fps.
    const int buttonSpacing = 3; // pixels between the speed buttons in the popup.
    static readonly Vector2 pauseButtonSize = new Vector2(34, 30);
    static readonly Vector2 speedButtonSize = new Vector2(40, 30);
    ModContext ctx;
    int speed = 1;
    bool paused;
    double extraTicksOwed;
    Stopwatch frameTimer = new Stopwatch();

    Button footerIcon;
    Control popup;
    Button pauseButton;
    Texture2D pauseIcon, playIcon;
    Node gameDriver;
    int missedNodeChecks;

    string savedSpeedFile => Path.Combine(ctx.Directory, "speed.txt");

    public void OnLoad(ModContext ctx)
    {
        this.ctx = ctx;
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
        double timeSinceLastFrame = frameTimer.Elapsed.TotalSeconds;
        frameTimer.Restart();

        if (popup == null) BuildPopup();

        var game = GameAccess.CurrentGame;
        if (game == null || !game.IsSaveLoaded)
        {
            popup.Visible = false;
            return;
        }

        // The footer and GameDriver get rebuilt when a save loads, so look again when they go away.
        if (Engine.GetProcessFrames() % nodeCheckInterval == 0)
        {
            if (!GodotObject.IsInstanceValid(footerIcon))
            {
                footerIcon = Footer.AddIcon(ctx.Tree, "SpeedControlIcon", "res://Assets/Icons/TimeInterval_48.png", null);
                if (footerIcon != null) footerIcon.Pressed += () => Footer.Toggle(popup, footerIcon);
            }
            if (!GodotObject.IsInstanceValid(gameDriver))
                gameDriver = Footer.Find(ctx.Tree.Root, "GameDriver");
            LogMissingNodes();
        }

        // GameDriver._PhysicsProcess is what calls PropagateTick, so switching it off pauses the game.
        if (paused)
        {
            if (gameDriver != null && gameDriver.IsPhysicsProcessing()) gameDriver.SetPhysicsProcess(false);
            return;
        }

        bool runningAtNormalSpeed = speed == 1 || GameAccess.IsComputingOffline || ctx.Tree.Paused;
        if (runningAtNormalSpeed)
        {
            extraTicksOwed = 0;
            return;
        }

        int speedMultiplier = speed - 1;
        double frameTime = Math.Min(timeSinceLastFrame, maxFrameTime);
        extraTicksOwed += frameTime * gameTicksPerSecond * speedMultiplier;
        while (extraTicksOwed >= 1)
        {
            game.PropagateTick();
            extraTicksOwed--;
        }
    }

    // Logs once when a game update renames the nodes this mod hooks into. Resets once both are found again.
    void LogMissingNodes()
    {
        bool hasIcon = footerIcon != null;
        bool hasDriver = gameDriver != null;
        if (hasIcon && hasDriver)
        {
            missedNodeChecks = 0;
            return;
        }
        missedNodeChecks++;
        if (missedNodeChecks != missedChecksBeforeLog) return;
        if (!hasIcon) ctx.Log("MusicButton not found, speed icon not added");
        if (!hasDriver) ctx.Log("GameDriver not found, pause will not work");
    }

    void BuildPopup()
    {
        pauseIcon = GD.Load<Texture2D>("res://Assets/Icons/Pause.png");
        playIcon = GD.Load<Texture2D>("res://Assets/Icons/Play.png");

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", buttonSpacing);

        pauseButton = new Button();
        pauseButton.Icon = pauseIcon;
        pauseButton.ExpandIcon = true;
        pauseButton.ToggleMode = true;
        pauseButton.CustomMinimumSize = pauseButtonSize;
        Footer.Style(pauseButton);
        pauseButton.Toggled += SetPaused;
        row.AddChild(pauseButton);

        var group = new ButtonGroup();
        foreach (int option in speeds)
        {
            var button = new Button();
            button.Text = option + "x";
            button.ToggleMode = true;
            button.ButtonGroup = group;
            button.ButtonPressed = option == speed;
            button.CustomMinimumSize = speedButtonSize;
            Footer.Style(button);
            button.Pressed += () => SetSpeed(option);
            row.AddChild(button);
        }

        popup = Footer.MakePopup(ctx.Tree, row);
    }

    void SetPaused(bool isPaused)
    {
        paused = isPaused;
        extraTicksOwed = 0;
        pauseButton.Icon = isPaused ? playIcon : pauseIcon;
        if (gameDriver != null) gameDriver.SetPhysicsProcess(!isPaused);
        ctx.Log(isPaused ? "paused" : "resumed");
    }

    void SetSpeed(int newSpeed)
    {
        speed = newSpeed;
        extraTicksOwed = 0;
        File.WriteAllText(savedSpeedFile, newSpeed.ToString());
        ctx.Log($"speed {newSpeed}x");
    }
}
