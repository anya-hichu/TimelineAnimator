using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using TimelineAnimator.Format;
using TimelineAnimator.ImSequencer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using YourPlugin.Serialization;

namespace TimelineAnimator.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly IFramework framework;

    private readonly List<MyEditorWindow> sequencers = new();

    private int sharedCurrentFrame = 0;
    private bool isPlaying = false;
    private int sharedSelectedEntry = -1;
    private int activeSequencerTab = -1;
    private int lastAppliedFrame = -1;
    

    private const double PlaybackFramesPerSecond = 30.0;
    private double timeAccumulator = 0.0;

    private bool inspectorVisible = true;
    private float inspectorWidth = 200f;

    private readonly string[] keyframeShapeNames = Enum.GetNames(typeof(KeyframeShape));

    public MainWindow(Plugin plugin, IFramework framework)
        : base("Timeline Animator##SequencerMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        this.plugin = plugin;
        this.framework = framework;
    }

    public void Dispose() { }
    private int GetGlobalMinFrame()
    {
        if (sequencers.Count == 0) return 0;
        return sequencers.Min(s => s.GetMinFrame());
    }

    private int GetGlobalMaxFrame()
    {
        if (sequencers.Count == 0) return 100;
        return sequencers.Max(s => s.GetMaxFrame());
    }
    public void UpdateAnimation(IFramework fw)
    {
        if (!isPlaying || PlaybackFramesPerSecond <= 0) { timeAccumulator = 0.0; return; }
        if (sequencers.Count == 0) return;

        timeAccumulator += fw.UpdateDelta.TotalSeconds;
        double frameDuration = 1.0 / PlaybackFramesPerSecond;
        if (frameDuration <= 0) { timeAccumulator = 0.0; return; }

        int maxFrame = GetGlobalMaxFrame();
        int minFrame = GetGlobalMinFrame();

        bool frameAdvanced = false;
        while (timeAccumulator >= frameDuration)
        {
            sharedCurrentFrame++;
            timeAccumulator -= frameDuration;
            if (sharedCurrentFrame > maxFrame) { sharedCurrentFrame = minFrame; }
            frameAdvanced = true;
        }

        if (frameAdvanced)
        {
            ApplyPoseToAllSequencers(sharedCurrentFrame);
        }
    }

    private void ApplyPoseToAllSequencers(int frame)
    {
        if (lastAppliedFrame == frame) return;

        if (sequencers.Count == 0)
        {
            lastAppliedFrame = frame;
            return;
        }

        foreach (var sequencer in sequencers)
        {
            var poseFile = new KtisisPoseFile
            {
                Bones = new Dictionary<string, BoneDto>()
            };

            foreach (var animation in sequencer.GetAnimations())
            {
                var boneDto = AnimationHelpers.GetInterpolatedBone(sequencer, animation, frame);
                if (boneDto != null)
                {
                    poseFile.Bones[animation.Name] = boneDto;
                }
            }

            if (poseFile.Bones.Count > 0)
            {
                string newPoseJson = JsonSerializer.Serialize(poseFile, KtisisJsonContext.Default.KtisisPoseFile);
                _ = plugin.KtisisIpc.LoadPoseAsync(sequencer.ActorIndex, newPoseJson);
            }
            else
            {
                if (sequencer.DefaultPoseJson != null)
                {
                    _ = plugin.KtisisIpc.LoadPoseAsync(sequencer.ActorIndex, sequencer.DefaultPoseJson);
                }
            }
        }

        lastAppliedFrame = frame;
    }
    private async void OnGetSelectedBones()
    {
        if (!plugin.KtisisIpc.IsAvailable) { Plugin.Log.Error("Cannot get bones: Ktisis IPC is not available."); return; }

        try
        {
            var selectedBonesByActor = await plugin.KtisisIpc.GetSelectedBonesAsync();

            if (selectedBonesByActor == null || selectedBonesByActor.Count == 0) { return; }

            foreach (var (actorIndex, boneNames) in selectedBonesByActor)
            {
                if (boneNames == null || boneNames.Count == 0) { continue; }

                var existingSequencer = sequencers.FirstOrDefault(s => s.ActorIndex == actorIndex);

                if (existingSequencer == null)
                {
                    int currentGlobalMax = GetGlobalMaxFrame();

                    string? defaultPoseJson = await plugin.KtisisIpc.SavePoseAsync((uint)actorIndex);
                    string actorName = $"Actor {actorIndex}";
                    var newSequencer = new MyEditorWindow(actorName, (uint)actorIndex, defaultPoseJson);

                    newSequencer.SetMaxFrame(currentGlobalMax);

                    foreach (var boneName in boneNames.OrderBy(b => b))
                    {
                        newSequencer.AddTrack(boneName);
                    }
                    sequencers.Add(newSequencer);
                }
                else
                {
                    foreach (var boneName in boneNames.OrderBy(b => b))
                    {
                        if (!existingSequencer.HasTrack(boneName))
                        {
                            existingSequencer.AddTrack(boneName);
                        }
                        else
                        {
                            var animation = existingSequencer.GetAnimationByName(boneName);
                            if (animation != null)
                            {
                                var existingKeyframe = animation.GetKeyframes().Cast<MyKeyframe>()
                                                             .FirstOrDefault(k => k.Frame == sharedCurrentFrame);

                                string? fullPoseJson = await plugin.KtisisIpc.SavePoseAsync(existingSequencer.ActorIndex);
                                BoneDto? boneTransform = null;

                                if (fullPoseJson != null)
                                {
                                    try
                                    {
                                        var poseFile = JsonSerializer.Deserialize(fullPoseJson, KtisisJsonContext.Default.KtisisPoseFile);
                                        poseFile?.Bones.TryGetValue(boneName, out boneTransform);
                                    }
                                    catch (Exception e)
                                    {
                                        Plugin.Log.Error(e, $"Failed to deserialize pose or find bone {boneName}");
                                    }
                                }

                                if (existingKeyframe == null)
                                {
                                    animation.AddKeyframe(sharedCurrentFrame, boneTransform);
                                }
                                else
                                {
                                    existingKeyframe.Transform = boneTransform;
                                }
                            }
                        }
                    }
                }
            }

            if (sequencers.Count > 0 && activeSequencerTab < 0)
            {
                activeSequencerTab = sequencers.Count - 1;
            }
            else if (sequencers.Count > 0 && activeSequencerTab >= sequencers.Count)
            {
                activeSequencerTab = 0;
            }
            else if (sequencers.Count == 0)
            {
                activeSequencerTab = -1;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Exception occurred within OnGetSelectedBones:");
        }
    }

    public override void Draw()
    {
        var style = ImGui.GetStyle();
        float contentWidth = ImGui.GetContentRegionAvail().X;
        float itemHeight = ImGui.GetFrameHeight();
        float itemSpacing = style.ItemSpacing.X;

        if (ImGuiComponents.IconButton(FontAwesomeIcon.PlusCircle))
        {
            OnGetSelectedBones();
        }
        if (plugin.Configuration.ShowTooltips && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Select bones in Ktisis first to add tracks/keyframe.");
        }

        float middleContentWidth = (itemHeight * 2) + itemSpacing;
        float middlePosX = (contentWidth - middleContentWidth) * 0.5f;
        ImGui.SameLine();
        ImGui.SetCursorPosX(middlePosX);

        if (ImGuiComponents.IconButton(isPlaying ? FontAwesomeIcon.PauseCircle : FontAwesomeIcon.PlayCircle)) { isPlaying = !isPlaying; if (!isPlaying) timeAccumulator = 0.0; }
        if (plugin.Configuration.ShowTooltips && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Start / Pause Playback.");
        }
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.StopCircle))
        {
            isPlaying = false;
            sharedCurrentFrame = GetGlobalMinFrame();
            timeAccumulator = 0.0;
            lastAppliedFrame = -1;
            ApplyPoseToAllSequencers(sharedCurrentFrame);
        }
        if (plugin.Configuration.ShowTooltips && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Stop Playback.");
        }
        float rightContentWidth = (itemHeight * 1); //+ (itemSpacing * 2)
        float rightPosX = contentWidth - rightContentWidth;
        ImGui.SameLine();
        ImGui.SetCursorPosX(rightPosX);

        if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
        {
            plugin.ToggleConfigUi();
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
            int previousActiveTab = activeSequencerTab;

            for (int i = sequencers.Count - 1; i >= 0; i--)
            {
                if (!sequencers[i].IsVisible)
                {
                    if (activeSequencerTab == i)
                    {
                        activeSequencerTab = -1;
                        sharedSelectedEntry = -1;
                    }
                    sequencers.RemoveAt(i);
                }
            }

            for (int i = 0; i < sequencers.Count; i++)
            {
                var sequencer = sequencers[i];
                if (ImGui.BeginTabItem($"{sequencer.SequencerName}##{sequencer.ActorIndex}", ref sequencer.IsVisible, ImGuiTabItemFlags.None))
                {
                    activeSequencerTab = i;
                    sequencer.Draw(ref sharedCurrentFrame, ref sharedSelectedEntry);
                    ImGui.EndTabItem();
                }
            }

            if (activeSequencerTab != previousActiveTab)
            {
                sharedSelectedEntry = -1;
                lastAppliedFrame = -1;
            }
            if (activeSequencerTab >= sequencers.Count)
            {
                activeSequencerTab = sequencers.Count - 1;
            }
            if (activeSequencerTab < 0 && sequencers.Count > 0)
            {
                activeSequencerTab = 0;
            }

            ImGui.EndTabBar();
        }
        if (!isPlaying)
        {
            ApplyPoseToAllSequencers(sharedCurrentFrame);
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

        if (sequencers.Count == 0 || activeSequencerTab < 0 || activeSequencerTab >= sequencers.Count)
        {
            ImGui.Text("No actor selected.");
            ImGui.TextDisabled("Click the 'Get Ktisis Actors' button");
            ImGui.TextDisabled("(the one with the Plus icon)");

            plugin.UpdateEasingUiKeyframe(null);
            return;
        }

        MyEditorWindow activeSequencer = sequencers[activeSequencerTab];
        int selectedKeyframeIndex = activeSequencer.GetSelectedKeyframeIndex();
        bool isKeyframeSelected = sharedSelectedEntry != -1 && selectedKeyframeIndex != -1;

        if (!isKeyframeSelected)
        {
            ImGui.Text("Play Range");
            int frameMax = GetGlobalMaxFrame();

            int minFrames = GetGlobalMinFrame() + 1;
            if (ImGui.DragInt("Max Frames##PlayRange", ref frameMax, 1.0f, minFrames, 10000))
            {
                foreach (var sequencer in sequencers)
                {
                    sequencer.SetMaxFrame(frameMax);
                }
            }
            plugin.UpdateEasingUiKeyframe(null);
        }
        else
        {
            var keyframe = activeSequencer.GetSelectedKeyframe(sharedSelectedEntry, selectedKeyframeIndex) as MyKeyframe;
            var anim = activeSequencer.GetAnimation(sharedSelectedEntry);
            var io = ImGui.GetIO();
            plugin.UpdateEasingUiKeyframe(keyframe);

            ImGui.Text($"Track {sharedSelectedEntry + 1} Selected");
            if (anim != null)
            {
                ImGui.Text($"Bone: {anim.Name}");
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
            if (!isShiftDown)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button("Delete Keyframe"))
            {
                if (anim != null)
                {
                    anim.DeleteKeyframe(selectedKeyframeIndex);
                    activeSequencer.ClearSelectedKeyframe();
                    sharedSelectedEntry = -1;
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
                    if (useCustomColor)
                    {
                        keyframe.CustomColor = 0xFFFFFFFF;
                    }
                    else
                    {
                        keyframe.CustomColor = null;
                    }
                }

                ImGui.SameLine();
                if (useCustomColor)
                {
                    uint abgr = keyframe.CustomColor ?? 0xFFFFFFFF;
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