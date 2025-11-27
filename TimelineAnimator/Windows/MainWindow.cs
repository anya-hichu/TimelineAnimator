using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using TimelineAnimator.ImSequencer;
using System;
using System.Numerics;

namespace TimelineAnimator.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly TimelineManager timeline;
    private bool inspectorVisible = true;
    private float inspectorWidth = 200f;

    private readonly string[] keyframeShapeNames = Enum.GetNames(typeof(KeyframeShape));

    public MainWindow(Plugin plugin, TimelineManager timelineManager)
        : base("Timeline Animator##SequencerMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        this.plugin = plugin;
        this.timeline = timelineManager;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var style = ImGui.GetStyle();
        float contentWidth = ImGui.GetContentRegionAvail().X;
        float itemHeight = ImGui.GetFrameHeight();
        float itemSpacing = style.ItemSpacing.X;

        if (ImGuiComponents.IconButton(FontAwesomeIcon.PlusCircle))
        {
            timeline.FetchSelectedBones();
        }
        if (plugin.Configuration.ShowTooltips && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Select bones in Ktisis first to add tracks/keyframe.");
        }

        float middleContentWidth = (itemHeight * 2) + itemSpacing;
        float middlePosX = (contentWidth - middleContentWidth) * 0.5f;
        ImGui.SameLine();
        ImGui.SetCursorPosX(middlePosX);

        var icon = timeline.IsPlaying ? FontAwesomeIcon.PauseCircle : FontAwesomeIcon.PlayCircle;
        if (ImGuiComponents.IconButton(icon))
        {
            timeline.TogglePlay();
        }
        if (plugin.Configuration.ShowTooltips && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Start / Pause Playback.");
        }
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.StopCircle))
        {
            timeline.Stop();
        }
        if (plugin.Configuration.ShowTooltips && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Stop Playback.");
        }

        float rightContentWidth = (itemHeight * 1);
        float rightPosX = contentWidth - rightContentWidth;
        ImGui.SameLine();
        ImGui.SetCursorPosX(rightPosX);

        var activeSequencer = timeline.GetActiveSequencer();
        bool hasTrackSelected = activeSequencer != null && timeline.SharedSelectedEntry != -1;
        bool shiftDown = ImGui.GetIO().KeyShift;
        if (!shiftDown || !hasTrackSelected) ImGui.BeginDisabled();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.MinusCircle))
        {
            if (activeSequencer != null && timeline.SharedSelectedEntry != -1)
            {
                activeSequencer.RemoveTrack(timeline.SharedSelectedEntry);
                timeline.SharedSelectedEntry = -1;
                timeline.GetActiveSequencer()?.ClearSelectedKeyframe();
            }
        }
        if (!shiftDown || !hasTrackSelected)
        {
            ImGui.EndDisabled();
            if (plugin.Configuration.ShowTooltips && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Hold SHIFT to remove the selected track");
            }
        }
        ImGui.Separator();

        float toggleButtonWidth = 15f;
        float availableWidth = ImGui.GetContentRegionAvail().X;
        float availableHeight = ImGui.GetContentRegionAvail().Y;

        float mainContentWidth = availableWidth - toggleButtonWidth - style.ItemSpacing.X;
        if (inspectorVisible)
        {
            mainContentWidth -= (inspectorWidth + style.ItemSpacing.X);
        }

        ImGui.BeginChild("SequencerArea", new Vector2(mainContentWidth, availableHeight), false);

        if (ImGui.BeginTabBar("SequencerTabs", ImGuiTabBarFlags.Reorderable))
        {
            int previousActiveTab = timeline.ActiveSequencerIndex;

            for (int i = timeline.Sequencers.Count - 1; i >= 0; i--)
            {
                if (!timeline.Sequencers[i].IsVisible)
                {
                    if (timeline.ActiveSequencerIndex == i)
                    {
                        timeline.ActiveSequencerIndex = -1;
                        timeline.SharedSelectedEntry = -1;
                    }
                    timeline.Sequencers.RemoveAt(i);
                }
            }

            for (int i = 0; i < timeline.Sequencers.Count; i++)
            {
                var sequencer = timeline.Sequencers[i];
                bool isOpen = sequencer.IsVisible;
                if (ImGui.BeginTabItem($"{sequencer.SequencerName}##{sequencer.ActorIndex}", ref isOpen, ImGuiTabItemFlags.None))
                {
                    timeline.ActiveSequencerIndex = i;

                    int currentFrame = timeline.CurrentFrame;
                    int selectedEntry = timeline.SharedSelectedEntry;

                    sequencer.Draw(ref currentFrame, ref selectedEntry);

                    timeline.CurrentFrame = currentFrame;
                    timeline.SharedSelectedEntry = selectedEntry;

                    ImGui.EndTabItem();
                }
                sequencer.IsVisible = isOpen;
            }

            if (timeline.ActiveSequencerIndex != previousActiveTab)
            {
                timeline.SharedSelectedEntry = -1;
            }
            if (timeline.ActiveSequencerIndex >= timeline.Sequencers.Count)
            {
                timeline.ActiveSequencerIndex = timeline.Sequencers.Count - 1;
            }
            if (timeline.ActiveSequencerIndex < 0 && timeline.Sequencers.Count > 0)
            {
                timeline.ActiveSequencerIndex = 0;
            }

            ImGui.EndTabBar();
        }
        if (!timeline.IsPlaying)
        {
            timeline.ApplyCurrentPose();
        }
        ImGui.EndChild();

        ImGui.SameLine();
        var buttonIcon = inspectorVisible ? FontAwesomeIcon.AngleRight : FontAwesomeIcon.AngleLeft;
        var buttonSize = new Vector2(toggleButtonWidth, availableHeight);

        if (ImGuiComponents.IconButton(buttonIcon, buttonSize))
        {
            inspectorVisible = !inspectorVisible;
        }

        if (inspectorVisible)
        {
            ImGui.SameLine();
            ImGui.BeginChild("InspectorPanel", new Vector2(inspectorWidth, availableHeight), true);
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
            DrawInspectorContents();
            ImGui.PopItemWidth();
            ImGui.EndChild();
        }
    }

    private void DrawInspectorContents()
    {
        ImGui.Text("Inspector");
        ImGui.Separator();
        ImGui.Spacing();

        MyEditorWindow? activeSequencer = timeline.GetActiveSequencer();

        if (activeSequencer == null)
        {
            ImGui.Text("No actor selected.");
            ImGui.TextDisabled("Click the 'Get Ktisis Actors' button");
            ImGui.TextDisabled("(the one with the Plus icon)");

            plugin.UpdateEasingUiKeyframe(null);
            return;
        }

        int selectedKeyframeIndex = activeSequencer.GetSelectedKeyframeIndex();
        bool isKeyframeSelected = timeline.SharedSelectedEntry != -1 && selectedKeyframeIndex != -1;

        if (!isKeyframeSelected)
        {
            ImGui.Text("Play Range");
            int frameMax = timeline.GetGlobalMaxFrame();
            int minFrames = timeline.GetGlobalMinFrame() + 1;

            if (ImGui.DragInt("Max Frames##PlayRange", ref frameMax, 1.0f, minFrames, 10000))
            {
                timeline.SetMaxFrameForAll(frameMax);
            }
            plugin.UpdateEasingUiKeyframe(null);
        }
        else
        {
            var keyframe = activeSequencer.GetSelectedKeyframe(timeline.SharedSelectedEntry, selectedKeyframeIndex) as MyKeyframe;
            var anim = activeSequencer.GetAnimation(timeline.SharedSelectedEntry);
            var io = ImGui.GetIO();

            plugin.UpdateEasingUiKeyframe(keyframe);

            ImGui.Text($"Track {timeline.SharedSelectedEntry + 1} Selected");
            if (anim != null)
            {
                ImGui.Text($"Bone: {anim.DisplayName}");
                if (plugin.Configuration.ShowTooltips && ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"Internal Name: {anim.Name}");
                }
            }

            if (keyframe != null)
            {
                ImGui.Text($"Frame: {keyframe.Frame}");
            }
            else
            {
                ImGui.TextDisabled("Keyframe not found.");
            }
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Edit Easing"))
            {
                plugin.OpenEasingUiForKeyframe(keyframe);
            }
            ImGui.Spacing();

            bool isShiftDown = io.KeyShift;
            if (!isShiftDown) ImGui.BeginDisabled();

            if (ImGui.Button("Delete Keyframe"))
            {
                if (anim != null)
                {
                    anim.DeleteKeyframe(selectedKeyframeIndex);
                    activeSequencer.ClearSelectedKeyframe();
                    timeline.SharedSelectedEntry = -1;
                }
            }

            if (!isShiftDown)
            {
                ImGui.EndDisabled();
                if (plugin.Configuration.ShowTooltips && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Hold SHIFT to delete");
                }
            }

            if (keyframe != null)
            {
                ImGui.Text("Keyframe Style");
                int currentShape = (int)keyframe.Shape;
                if (ImGui.Combo("Shape", ref currentShape, keyframeShapeNames, keyframeShapeNames.Length))
                {
                    keyframe.Shape = (KeyframeShape)currentShape;
                }
                bool useCustomColor = keyframe.CustomColor.HasValue;
                if (ImGui.Checkbox("Custom Color", ref useCustomColor))
                {
                    keyframe.CustomColor = useCustomColor ? 0xFFFFFFFF : null;
                }

                ImGui.SameLine();
                if (keyframe.CustomColor.HasValue)
                {
                    uint abgr = keyframe.CustomColor.Value;
                    Vector4 rgba = new(
                        ((abgr >> 0) & 0xFF) / 255.0f,
                        ((abgr >> 8) & 0xFF) / 255.0f,
                        ((abgr >> 16) & 0xFF) / 255.0f,
                        ((abgr >> 24) & 0xFF) / 255.0f
                    );

                    if (ImGui.ColorEdit4("##KeyframeColor", ref rgba, ImGuiColorEditFlags.NoInputs))
                    {
                        keyframe.CustomColor =
                            (((uint)(rgba.X * 255) & 0xFF) << 0) |
                            (((uint)(rgba.Y * 255) & 0xFF) << 8) |
                            (((uint)(rgba.Z * 255) & 0xFF) << 16) |
                            (((uint)(rgba.W * 255) & 0xFF) << 24);
                    }
                }
            }
        }
    }
}