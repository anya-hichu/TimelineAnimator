using Dalamud.Bindings.ImGui;
using TimelineAnimator.Format;
using System.Collections.Generic;

namespace TimelineAnimator.ImSequencer
{
    public interface IKeyframe
    {
        int Frame { get; set; }
    }

    public interface IAnimation
    {
        string Name { get; }
        uint Color { get; }

        IEnumerable<IKeyframe> GetKeyframes();
        IKeyframe AddKeyframe(int frame, BoneDto? transform);
        void DeleteKeyframe(int keyframeIndex);
        IKeyframe GetKeyframe(int keyframeIndex);
        int GetKeyframeCount();
    }

    public interface SequenceInterface
    {
        bool focused { get; set; }
        int FrameMin { get; }
        int FrameMax { get; set; }
        bool IsPaused { get; set; }
        int ItemCount { get; }
        bool ForceLoop { get; set; }

        void BeginEdit(int index);
        void EndEdit();

        int GetItemTypeCount();
        string GetItemTypeName(int typeIndex);

        IAnimation GetAnimation(int index);
        void AddAnimation();
        void RemoveAnimation(int index);
        void DuplicateAnimation(int index);

        bool IsFocus(int index);
        void SetFocus(int index);
        void ResetFocus();
        bool IsVisible(int index);
        void SetVisibility(int index, bool isVisible);

        void Copy();
        void Paste();

        int GetCustomHeight(int index);
        void DoubleClick(int index);
        void CustomDraw(int index, ImDrawListPtr draw_list, ImRect rc, ImRect legendRect, ImRect clippingRect, ImRect legendClippingRect);
        void CustomDrawCompact(int index, ImDrawListPtr draw_list, ImRect rc, ImRect clippingRect);
    }
}
