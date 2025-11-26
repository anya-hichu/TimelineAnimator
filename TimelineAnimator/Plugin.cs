using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TimelineAnimator.ImSequencer;
using TimelineAnimator.Interop;
using TimelineAnimator.Windows;

namespace TimelineAnimator;

public sealed class Plugin : IDalamudPlugin
{
    private bool wasInGpose = false;

    private const string CommandName = "/animator";
#if DEBUG
    private const string DebugCommandName = "/animatordebug";
#endif
    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("TimelineAnimator");
    public KtisisIpc KtisisIpc { get; init; }
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private DebugWindow DebugWindow { get; init; }
    private TutorialWindow TutorialWindow { get; init; }
    private EasingWindow EasingWindow { get; init; }
    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        KtisisIpc = new KtisisIpc();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, Services.Framework);
        TutorialWindow = new TutorialWindow(this);
        EasingWindow = new EasingWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(TutorialWindow);
        WindowSystem.AddWindow(EasingWindow);

#if DEBUG
        DebugWindow = new DebugWindow(this);
        WindowSystem.AddWindow(DebugWindow);
#endif
        pluginInterface.UiBuilder.DisableGposeUiHide = true;

        Services.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggles the Main Window."
        });


#if DEBUG
        Services.CommandManager.AddHandler(DebugCommandName, new CommandInfo(OnDebugCommand)
        {
            HelpMessage = "Toggles the Ktisis IPC Debug Window"
        });
#endif
        pluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        pluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Services.Framework.Update += OnFrameworkUpdate;

        Services.Log.Information($"{pluginInterface.Manifest.Name} Started up successfully!");
    }

    public void Dispose()
    {
        Services.Framework.Update -= OnFrameworkUpdate;

        Services.PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        Services.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        Services.PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

#if DEBUG
        DebugWindow.Dispose();
#endif
        EasingWindow.Dispose();
        TutorialWindow.Dispose();
        Services.CommandManager.RemoveHandler(CommandName);
#if DEBUG
        Services.CommandManager.RemoveHandler(DebugCommandName);
#endif
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        bool currentlyInGpose = Services.ClientState.IsGPosing;
        if (currentlyInGpose != wasInGpose)
        {
            if (currentlyInGpose)
                OnEnterGpose();
            else
                OnLeaveGpose();

            wasInGpose = currentlyInGpose;
        }

        MainWindow.UpdateAnimation(framework);
    }
    private void OnEnterGpose()
    {
        if (Configuration.OpenInGpose)
        {
            MainWindow.IsOpen = true;
        }
        if (Configuration.ShowTutorial)
        {
            TutorialWindow.IsOpen = true;
        }
    }

    private void OnLeaveGpose()
    {
        MainWindow.IsOpen = false;
        TutorialWindow.IsOpen = false;
        EasingWindow.IsOpen = false;
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

#if DEBUG
    private void OnDebugCommand(string command, string args) => DebugWindow.Toggle();
#endif

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
    public void ToggleTutorialWindow() => TutorialWindow.Toggle();
    public void OpenEasingUiForKeyframe(MyKeyframe? keyframe)
    {
        EasingWindow.SetKeyframe(keyframe);
        EasingWindow.IsOpen = true;
    }

    public void UpdateEasingUiKeyframe(MyKeyframe? keyframe)
    {
        EasingWindow.SetKeyframe(keyframe);
    }
}