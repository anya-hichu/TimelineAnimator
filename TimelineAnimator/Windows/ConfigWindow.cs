using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Windowing;

namespace TimelineAnimator.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;
    private string? bindingActionName = null;
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
        ImGui.BeginTabBar("ConfigTabs");
        if (ImGui.BeginTabItem("General Settings"))
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
            
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Keybindings"))
        {
            ImGui.TextDisabled("Click a button to rebind. PressESC to clear");
            ImGui.Spacing();
            ImGui.TextWrapped("Assign a Modifier Key (ALT, SHIFT or CTRL). While holding it, game input will be blocked and you can use the other hotkeys.");
            ImGui.Spacing();
            DrawKeybind("Modifier", configuration.ModifierKey, k => configuration.ModifierKey = k);
            DrawKeybind("Toggle Playback", configuration.TogglePlaybackKey, k => configuration.TogglePlaybackKey = k);
            DrawKeybind("Add Item", configuration.AddItemKey, k => configuration.AddItemKey = k);

            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawKeybind(string label, VirtualKey currentKey, Action<VirtualKey> setter)
    {
        ImGui.Text(label);
        ImGui.SameLine();

        if (bindingActionName == label)
        {
            ImGui.Button("Press any key...");

            var pressedKey = Services.KeyState.GetValidVirtualKeys().FirstOrDefault(k => Services.KeyState[k]);
            if (pressedKey != VirtualKey.NO_KEY)
            {
                if (pressedKey == VirtualKey.ESCAPE)
                {
                    setter(VirtualKey.NO_KEY);
                }
                else
                {
                    setter(pressedKey);
                }
                configuration.Save();
                bindingActionName = null;
            }
        }
        else
        {
            string keyName = currentKey == VirtualKey.NO_KEY ? "None" : currentKey.ToString();
            if (ImGui.Button($"{keyName}###{label}"))
            {
                bindingActionName = label;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Click to rebind");
            }
        }
    }
}
