using TimelineAnimator.Format;
using TimelineAnimator.ImSequencer;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using YourPlugin.Serialization;
using System;

namespace TimelineAnimator;

public static class AnimationHelpers
{
    public static BoneDto? GetInterpolatedBone(MyEditorWindow sequencer, IAnimation animation, int currentFrame)
    {
        var keyframes = animation.GetKeyframes().Cast<MyKeyframe>().ToList();
        if (keyframes.Count == 0)
        {
            // Return null preserve pose data
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

        BoneDto? startPoseDto;
        int startFrame;

        if (kfB_Index == 0)
        {
            startPoseDto = GetDefaultBone(sequencer, animation.Name);
            startFrame = 0;
        }
        else
        {
            MyKeyframe kfA = keyframes[kfB_Index - 1];
            startPoseDto = kfA.Transform;
            startFrame = kfA.Frame;
        }

        BoneDto? endPoseDto = kfB.Transform;
        int endFrame = kfB.Frame;

        if (startPoseDto == null || endPoseDto == null) return null;

        float t = (float)(currentFrame - startFrame) / (float)(endFrame - startFrame);
        if (float.IsNaN(t) || float.IsInfinity(t)) t = 0;

        float easedT = GetEasedT(t, kfB);

        return new BoneDto
        {
            Position = Lerp(startPoseDto.Position, endPoseDto.Position, easedT),
            Rotation = Slerp(startPoseDto.Rotation, endPoseDto.Rotation, easedT),
            Scale = Lerp(startPoseDto.Scale, endPoseDto.Scale, easedT)
        };
    }

    // default for single bone from actor base JSON
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

    private static Vector3Dto Lerp(Vector3Dto a, Vector3Dto b, float t)
    {
        return new Vector3Dto
        {
            X = a.X + (b.X - a.X) * t,
            Y = a.Y + (b.Y - a.Y) * t,
            Z = a.Z + (b.Z - a.Z) * t
        };
    }

    private static QuaternionDto Slerp(QuaternionDto a, QuaternionDto b, float t)
    {
        var qA = new Quaternion(a.X, a.Y, a.Z, a.W);
        var qB = new Quaternion(b.X, b.Y, b.Z, b.W);

        var resultQ = Quaternion.Slerp(qA, qB, t);

        return new QuaternionDto { X = resultQ.X, Y = resultQ.Y, Z = resultQ.Z, W = resultQ.W };
    }
}