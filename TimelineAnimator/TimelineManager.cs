using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TimelineAnimator.Format;
using TimelineAnimator.ImSequencer;
using TimelineAnimator.Interop;
using TimelineAnimator.Serialization;

namespace TimelineAnimator;

public class TimelineManager : IDisposable
{
    private readonly KtisisIpc ipc;

    public List<MyEditorWindow> Sequencers { get; private set; } = new();

    public int CurrentFrame { get; set; } = 0;
    public bool IsPlaying { get; private set; } = false;

    public int SharedSelectedEntry { get; set; } = -1;
    public int ActiveSequencerIndex { get; set; } = -1;

    private int lastAppliedFrame = -1;
    private const double PlaybackFramesPerSecond = 30.0;
    private double timeAccumulator = 0.0;

    public TimelineManager(KtisisIpc ipc)
    {
        this.ipc = ipc;
    }

    public void Dispose() { }

    public int GetGlobalMinFrame()
    {
        if (Sequencers.Count == 0) return 0;
        return Sequencers.Min(s => s.GetMinFrame());
    }

    public int GetGlobalMaxFrame()
    {
        if (Sequencers.Count == 0) return 100;
        return Sequencers.Max(s => s.GetMaxFrame());
    }

    public void SetMaxFrameForAll(int max)
    {
        foreach (var s in Sequencers) s.SetMaxFrame(max);
    }

    public MyEditorWindow? GetActiveSequencer()
    {
        if (Sequencers.Count == 0 || ActiveSequencerIndex < 0 || ActiveSequencerIndex >= Sequencers.Count)
            return null;
        return Sequencers[ActiveSequencerIndex];
    }

    public void TogglePlay()
    {
        IsPlaying = !IsPlaying;
        if (!IsPlaying) timeAccumulator = 0.0;
    }

    public void Stop()
    {
        IsPlaying = false;
        CurrentFrame = GetGlobalMinFrame();
        timeAccumulator = 0.0;
        lastAppliedFrame = -1;
        ApplyPoseToAllSequencers(CurrentFrame);
    }

    public void Update(float deltaSeconds)
    {
        if (!IsPlaying || PlaybackFramesPerSecond <= 0) { timeAccumulator = 0.0; return; }
        if (Sequencers.Count == 0) return;

        timeAccumulator += deltaSeconds;
        double frameDuration = 1.0 / PlaybackFramesPerSecond;
        if (frameDuration <= 0) { timeAccumulator = 0.0; return; }

        int maxFrame = GetGlobalMaxFrame();
        int minFrame = GetGlobalMinFrame();

        bool frameAdvanced = false;
        while (timeAccumulator >= frameDuration)
        {
            CurrentFrame++;
            timeAccumulator -= frameDuration;
            if (CurrentFrame > maxFrame) { CurrentFrame = minFrame; }
            frameAdvanced = true;
        }

        if (frameAdvanced)
        {
            ApplyPoseToAllSequencers(CurrentFrame);
        }
    }

    public void ApplyCurrentPose()
    {
        ApplyPoseToAllSequencers(CurrentFrame);
    }

    private void ApplyPoseToAllSequencers(int frame)
    {
        if (lastAppliedFrame == frame) return;

        if (Sequencers.Count == 0)
        {
            lastAppliedFrame = frame;
            return;
        }

        foreach (var sequencer in Sequencers)
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
                _ = ipc.LoadPoseAsync(sequencer.ActorIndex, newPoseJson);
            }
            else
            {
                if (sequencer.DefaultPoseJson != null)
                {
                    _ = ipc.LoadPoseAsync(sequencer.ActorIndex, sequencer.DefaultPoseJson);
                }
            }
        }

        lastAppliedFrame = frame;
    }

    public async void FetchSelectedBones()
    {
        if (!ipc.IsAvailable) { Services.Log.Error("Cannot get bones: Ktisis IPC is not available."); return; }

        try
        {
            var selectedBonesByActor = await ipc.GetSelectedBonesAsync();

            if (selectedBonesByActor == null || selectedBonesByActor.Count == 0) { return; }

            foreach (var (actorIndex, boneNames) in selectedBonesByActor)
            {
                if (boneNames == null || boneNames.Count == 0) { continue; }

                var existingSequencer = Sequencers.FirstOrDefault(s => s.ActorIndex == actorIndex);

                if (existingSequencer == null)
                {
                    int currentGlobalMax = GetGlobalMaxFrame();

                    string? defaultPoseJson = await ipc.SavePoseAsync((uint)actorIndex);
                    string actorName = $"Actor {actorIndex}";
                    var newSequencer = new MyEditorWindow(actorName, (uint)actorIndex, defaultPoseJson);

                    newSequencer.SetMaxFrame(currentGlobalMax);

                    foreach (var boneName in boneNames.OrderBy(b => b))
                    {
                        newSequencer.AddTrack(boneName);
                    }
                    Sequencers.Add(newSequencer);
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
                                                             .FirstOrDefault(k => k.Frame == CurrentFrame);

                                string? fullPoseJson = await ipc.SavePoseAsync(existingSequencer.ActorIndex);
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
                                        Services.Log.Error(e, $"Failed to deserialize pose or find bone {boneName}");
                                    }
                                }

                                if (existingKeyframe == null)
                                {
                                    animation.AddKeyframe(CurrentFrame, boneTransform);
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

            if (Sequencers.Count > 0 && ActiveSequencerIndex < 0)
            {
                ActiveSequencerIndex = Sequencers.Count - 1;
            }
            else if (Sequencers.Count > 0 && ActiveSequencerIndex >= Sequencers.Count)
            {
                ActiveSequencerIndex = 0;
            }
            else if (Sequencers.Count == 0)
            {
                ActiveSequencerIndex = -1;
            }
        }
        catch (Exception e)
        {
            Services.Log.Error(e, "FetchSelectedBones encountered an Exception.");
        }
    }
}