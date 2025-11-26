using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace TimelineAnimator.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("Timeline Animator Settings")
    {
        Size = new Vector2(232, 160);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.configuration = plugin.Configuration;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var openInGpose = configuration.OpenInGpose;
        if (ImGui.Checkbox("Open automatically in GPose", ref openInGpose))
        {
            configuration.OpenInGpose = openInGpose;
            configuration.Save();
        }

        var showTooltips = configuration.ShowTooltips;
        if (ImGui.Checkbox("Show tooltips", ref showTooltips))
        {
            configuration.ShowTooltips = showTooltips;
            configuration.Save();
        }

        ImGui.Spacing();
        var showTutorial = configuration.ShowTutorial;
        if (ImGui.Checkbox("Show tutorial on entering GPose", ref showTutorial))
        {
            configuration.ShowTutorial = showTutorial;
            configuration.Save();
        }
        if (ImGui.Button("Show Tutorial"))
        {
            plugin.ToggleTutorialWindow();
        }
    }
}
