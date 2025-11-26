using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace TimelineAnimator.Windows;

public class TutorialWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;

    public TutorialWindow(Plugin plugin) : base("Welcome to Timeline Animator!")
    {
        this.configuration = plugin.Configuration;
        this.plugin = plugin;

        Size = new Vector2(400, 220);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextWrapped("This is a quick start guide to show you the functionality of the plugin!");
        ImGui.Spacing();
        ImGui.TextWrapped("1. Select the bones you want to animate in Ktisis.");
        ImGui.TextWrapped("2. Click the 'Add' button to create tracks for them, while they are selected.");
        ImGui.TextWrapped("   (This will also create new timelines for actors with their respective bones).");
        ImGui.TextWrapped("3. Move the playhead (the vertical red line) to a frame.");
        ImGui.TextWrapped("4. Pose the bones and reselect all of them when you are ready to finish up the animation.");
        ImGui.TextWrapped("5. Click 'Add Selected Bones' again to create a new keyframe.");
        ImGui.TextWrapped("   (This will update an existing keyframe if you're on one).");
        ImGui.Spacing();
        ImGui.TextWrapped("You can edit easing, delete keyframes and more in the inspector on the right. This will show up once you have clicked on a keyframe.");
        ImGui.Spacing();

        if (ImGui.Button("Got it! Don't show this again."))
        {
            configuration.ShowTutorial = false;
            configuration.Save();
            IsOpen = false;
        }

        ImGui.SameLine();
        if (ImGui.Button("Close"))
        {
            IsOpen = false;
        }
    }
}