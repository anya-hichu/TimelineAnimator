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
    // will move to global later
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

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
    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        KtisisIpc = new KtisisIpc();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, Framework);
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
        PluginInterface.UiBuilder.DisableGposeUiHide = true;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });


#if DEBUG
        CommandManager.AddHandler(DebugCommandName, new CommandInfo(OnDebugCommand)
        {
            HelpMessage = "Toggles the Ktisis IPC Debug Window"
        });
#endif
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Framework.Update += OnFrameworkUpdate;

        Log.Information($"{PluginInterface.Manifest.Name} Started up successfully!");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

#if DEBUG
        DebugWindow.Dispose();
#endif
        EasingWindow.Dispose();
        TutorialWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
#if DEBUG
        CommandManager.RemoveHandler(DebugCommandName);
#endif
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        bool currentlyInGpose = ClientState.IsGPosing;
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