using TimelineAnimator.Format;
using TimelineAnimator.ImSequencer;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using TimelineAnimator.Serialization;
using System;

namespace TimelineAnimator;

public static class AnimationHelpers
{
    public static BoneDto? GetInterpolatedBone(MyEditorWindow sequencer, IAnimation animation, int currentFrame)
    {
        var keyframes = animation.GetKeyframes().Cast<MyKeyframe>().ToList();
        if (keyframes.Count == 0)
        {
            return null;
        }
        MyKeyframe? kfB = keyframes.FirstOrDefault(k => k.Frame >= currentFrame);
        int kfB_Index = (kfB == null) ? -1 : keyframes.IndexOf(kfB);

        if (kfB == null)
        {
            return keyframes.Last().Transform;
        }

        if (kfB.Frame == currentFrame)
        {
            return kfB.Transform;
        }

        Vector3 startPos;
        Quaternion startRot;
        Vector3 startScale;
        int startFrame;

        if (kfB_Index == 0)
        {
            BoneDto? defaultBone = GetDefaultBone(sequencer, animation.Name);
            if (defaultBone == null) return null;

            startPos = new Vector3(defaultBone.Position.X, defaultBone.Position.Y, defaultBone.Position.Z);
            startRot = new Quaternion(defaultBone.Rotation.X, defaultBone.Rotation.Y, defaultBone.Rotation.Z, defaultBone.Rotation.W);
            startScale = new Vector3(defaultBone.Scale.X, defaultBone.Scale.Y, defaultBone.Scale.Z);
            startFrame = 0;
        }
        else
        {
            MyKeyframe kfA = keyframes[kfB_Index - 1];
            startPos = kfA.Position;
            startRot = kfA.Rotation;
            startScale = kfA.Scale;
            startFrame = kfA.Frame;
        }

        Vector3 endPos = kfB.Position;
        Quaternion endRot = kfB.Rotation;
        Vector3 endScale = kfB.Scale;
        int endFrame = kfB.Frame;

        float t = (float)(currentFrame - startFrame) / (float)(endFrame - startFrame);
        if (float.IsNaN(t) || float.IsInfinity(t)) t = 0;

        float easedT = GetEasedT(t, kfB);

        Vector3 resPos = Vector3.Lerp(startPos, endPos, easedT);
        Quaternion resRot = Quaternion.Slerp(startRot, endRot, easedT);
        Vector3 resScale = Vector3.Lerp(startScale, endScale, easedT);

        // Construct DTO
        return new BoneDto
        {
            Position = new Vector3Dto { X = resPos.X, Y = resPos.Y, Z = resPos.Z },
            Rotation = new QuaternionDto { X = resRot.X, Y = resRot.Y, Z = resRot.Z, W = resRot.W },
            Scale = new Vector3Dto { X = resScale.X, Y = resScale.Y, Z = resScale.Z }
        };
    }

    private static BoneDto? GetDefaultBone(MyEditorWindow sequencer, string boneName)
    {
        if (string.IsNullOrEmpty(sequencer.DefaultPoseJson)) return null;
        try
        {
            var poseFile = JsonSerializer.Deserialize(sequencer.DefaultPoseJson, KtisisJsonContext.Default.KtisisPoseFile);
            BoneDto? boneDto = null;
            poseFile?.Bones.TryGetValue(boneName, out boneDto);
            return boneDto;
        }
        catch (Exception e)
        {
            Services.Log.Error(e, "Failed to get default bone pose from JSON.");
            return null;
        }
    }

    private static float GetEasedT(float t, MyKeyframe kf)
    {
        t = Math.Clamp(t, 0.0f, 1.0f);
        float u = 1.0f - t;
        float tt = t * t;
        float uu = u * u;

        float y = (3 * uu * t * kf.P1.Y) + (3 * u * tt * kf.P2.Y) + (tt * t);
        return y;
    }
}