using Dalamud.Bindings.ImGui;
using TimelineAnimator.Format;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

// Some things in this file are unused or set only but may be useful for future features, leave them for now.
namespace TimelineAnimator.ImSequencer
{
    public enum KeyframeShape
    {
        Diamond,
        Circle,
        Square
    }

    public class MyKeyframe : IKeyframe
    {
        public int Frame { get; set; }
        public BoneDto? Transform { get; set; }
        public Vector2 P1 { get; set; }
        public Vector2 P2 { get; set; }
        public KeyframeShape Shape { get; set; }
        public uint? CustomColor { get; set; }
        public MyKeyframe(int frame, BoneDto? transform = null)
        {
            Frame = frame;
            P1 = new Vector2(0.25f, 0.25f);
            P2 = new Vector2(0.75f, 0.75f);
            Transform = transform;
            Shape = KeyframeShape.Diamond;
            CustomColor = null;
        }
    }

    internal class MyAnimation : IAnimation
    {
        public string Name { get; private set; }
        public uint Color { get; private set; }

        public List<MyKeyframe> Keyframes { get; private set; } = new List<MyKeyframe>();

        public MyAnimation(string name, uint color)
        {
            Name = name;
            Color = color;
        }

        public IEnumerable<IKeyframe> GetKeyframes() => Keyframes;

        public IKeyframe AddKeyframe(int frame, BoneDto? transform)
        {
            var newKeyframe = new MyKeyframe(frame, transform);
            Keyframes.Add(newKeyframe);
            Keyframes.Sort((a, b) => a.Frame.CompareTo(b.Frame));
            return newKeyframe;
        }

        public void DeleteKeyframe(int keyframeIndex)
        {
            if (keyframeIndex >= 0 && keyframeIndex < Keyframes.Count)
                Keyframes.RemoveAt(keyframeIndex);
        }

        public IKeyframe GetKeyframe(int keyframeIndex)
        {
            return Keyframes[keyframeIndex];
        }

        public int GetKeyframeCount() => Keyframes.Count;
    }

    internal class MySequencer : SequenceInterface
    {
        public List<MyAnimation> Animations { get; private set; }

        public MySequencer()
        {
            Animations = new List<MyAnimation>();
        }

        public bool focused { get; set; }
        public int FrameMin => 0;
        public int FrameMax { get; set; } = 100;
        public bool IsPaused { get; set; } = true;
        public int ItemCount => Animations.Count;
        public bool ForceLoop { get; set; } = true;

        public void BeginEdit(int index) { }
        public void EndEdit() { }

        public IAnimation GetAnimation(int index)
        {
            return Animations[index];
        }

        public void AddAnimation(string animationName)
        {
            var newAnim = new MyAnimation(animationName, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            Animations.Add(newAnim);
        }

        public void AddAnimation()
        {
            AddAnimation("New Animation");
        }

        public void RemoveAnimation(int index)
        {
            Animations.RemoveAt(index);
        }

        public void DuplicateAnimation(int index)
        {
            var anim = Animations[index];
            var newAnim = new MyAnimation(
                anim.Name + " (Copy)",
                anim.Color
            );

            foreach (var keyframe in anim.Keyframes.Cast<MyKeyframe>())
            {
                var newKeyframe = newAnim.AddKeyframe(keyframe.Frame, keyframe.Transform) as MyKeyframe;
                if (newKeyframe != null)
                {
                    newKeyframe.P1 = keyframe.P1;
                    newKeyframe.P2 = keyframe.P2;

                    newKeyframe.Shape = keyframe.Shape;
                    newKeyframe.CustomColor = keyframe.CustomColor;
                }
            }
            Animations.Insert(index + 1, newAnim);
        }

        public int GetItemTypeCount() => 0;
        public string GetItemTypeName(int typeIndex) => "";
        public bool IsFocus(int index) => false;
        public void SetFocus(int index) { }
        public void ResetFocus() { }
        public bool IsVisible(int index) => true;
        public void SetVisibility(int index, bool isVisible) { }
        public void Copy() { }
        public void Paste() { }
        public int GetCustomHeight(int index) => 0;
        public void DoubleClick(int index) { }
        public void CustomDraw(int index, ImDrawListPtr draw_list, ImRect rc, ImRect legendRect, ImRect clippingRect, ImRect legendClippingRect) { }

        public void CustomDrawCompact(int index, ImDrawListPtr draw_list, ImRect rc, ImRect clippingRect) { }
    }

    public class MyEditorWindow
    {
        private MySequencer mySequencer;
        private int firstFrame = 0;

        private ImSequencer imSequencer;
        private ImSequencerState imSequencerState;

        public string SequencerName { get; private set; }
        public uint ActorIndex { get; private set; }
        public bool IsVisible = true;
        public string? DefaultPoseJson { get; private set; }

        public MyEditorWindow(string name, uint actorIndex, string? defaultPose)
        {
            mySequencer = new MySequencer();
            imSequencer = new ImSequencer();
            imSequencerState = new ImSequencerState();
            SequencerName = name;
            ActorIndex = actorIndex;
            DefaultPoseJson = defaultPose;
        }
        public IEnumerable<IAnimation> GetAnimations()
        {
            return mySequencer.Animations;
        }

        public void AddTrack(string trackName)
        {
            mySequencer.AddAnimation(trackName);
        }

        public bool HasTrack(string trackName)
        {
            return mySequencer.Animations.Any(anim => anim.Name == trackName);
        }

        public void Draw(ref int sharedCurrentFrame, ref int sharedSelectedEntry)
        {
            imSequencer.Sequencer(
                            SequencerName,
                            imSequencerState,
                            mySequencer,
                            ref sharedCurrentFrame,
                            ref sharedSelectedEntry,
                            ref firstFrame
                        );
        }

        public int GetMinFrame() => mySequencer?.FrameMin ?? 0;
        public int GetMaxFrame() => mySequencer?.FrameMax ?? 100;
        public void SetMaxFrame(int max)
        {
            if (mySequencer != null)
                mySequencer.FrameMax = max;
        }

        public IAnimation GetAnimation(int index)
        {
            return mySequencer.GetAnimation(index);
        }

        public IKeyframe? GetSelectedKeyframe(int selectedEntryIndex, int keyframeIndex)
        {
            if (selectedEntryIndex < 0 || selectedEntryIndex >= mySequencer.ItemCount) return null;
            var anim = mySequencer.GetAnimation(selectedEntryIndex);
            if (keyframeIndex < 0 || keyframeIndex >= anim.GetKeyframeCount()) return null;
            return anim.GetKeyframe(keyframeIndex);
        }
        public int GetSelectedKeyframeIndex() => imSequencerState.movingKeyframeIndex;
        public IAnimation? GetAnimationByName(string trackName)
        {
            return mySequencer.Animations.FirstOrDefault(anim => anim.Name == trackName);
        }
        public void ClearSelectedKeyframe()
        {
            imSequencerState.movingKeyframeIndex = -1;
        }
    }
}