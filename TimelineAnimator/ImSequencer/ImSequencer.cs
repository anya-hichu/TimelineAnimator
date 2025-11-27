// The MIT License(MIT)
// 
// Copyright(c) 2016 Cedric Guillemet
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

// This is a porting and modified version of ImSequencer from ImGuizmo repository.
// Please refer to https://github.com/CedricGuillemet/ImGuizmo for the original
// source code.

using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace TimelineAnimator.ImSequencer
{
    public class ImSequencerState
    {
        public float framePixelWidth = 10f;
        public float framePixelWidthTarget = 10f;
        public int movingEntry = -1;
        public int movingPos = -1;
        public int movingKeyframeIndex = -1;
        public bool MovingCurrentFrame = false;


        public ZoomScrollbar.State ZoomState = new();
    }

    public class ImSequencer
    {
        public ImGuiCol Color_Widget_Background { get; set; } = ImGuiCol.WindowBg;
        public ImGuiCol Color_Content_Background { get; set; } = ImGuiCol.ChildBg;
        public ImGuiCol Color_Header_Background { get; set; } = ImGuiCol.TitleBg;
        public ImGuiCol Color_Header_Text { get; set; } = ImGuiCol.Text;
        public ImGuiCol Color_Header_Lines { get; set; } = ImGuiCol.Border;
        public ImGuiCol Color_Legend_Text { get; set; } = ImGuiCol.Text;
        public ImGuiCol Color_Stripe_1 { get; set; } = ImGuiCol.FrameBg;
        public ImGuiCol Color_Stripe_2 { get; set; } = ImGuiCol.FrameBgHovered;
        public ImGuiCol Color_Content_Lines { get; set; } = ImGuiCol.Border;
        public float Color_Content_Lines_Alpha { get; set; } = 0.188f;
        public ImGuiCol Color_Selection { get; set; } = ImGuiCol.HeaderActive;
        public float Color_Selection_Alpha { get; set; } = 0.25f;
        public ImGuiCol Color_Keyframe_Hover { get; set; } = ImGuiCol.TitleBgActive;
        public ImGuiCol Color_Playhead { get; set; } = ImGuiCol.TitleBgActive;
        public float Color_Playhead_Glow_Alpha { get; set; } = 0.314f;
        public ImGuiCol Color_Playhead_Text { get; set; } = ImGuiCol.Text;

        private class CustomDraw
        {
            public int index { get; set; }
            public ImRect customRect { get; set; }
            public ImRect legendRect { get; set; }
            public ImRect clippingRect { get; set; }
            public ImRect legendClippingRect { get; set; }
        };

        public bool Sequencer(string SequenceName,
                                ImSequencerState state,
                                SequenceInterface sequence,
                                ref int currentFrame,
                                ref int selectedEntry,
                                ref int firstFrame)
        {
            var ret = false;
            var io = ImGui.GetIO();
            var style = ImGui.GetStyle();
            var cx = (int)io.MousePos.X;
            var cy = (int)io.MousePos.Y;

            var ItemHeight = 20;

            var popupOpened = false;
            var sequenceCount = sequence.ItemCount;
            ImGui.BeginGroup();

            var draw_list = ImGui.GetWindowDrawList();
            var canvas_pos = ImGui.GetCursorScreenPos();
            var canvas_size = ImGui.GetContentRegionAvail();

            var legendWidth = 120;
            var viewWidthPixels = canvas_size.X - legendWidth;

            state.ZoomState.ContentMin = sequence.FrameMin;
            state.ZoomState.ContentMax = sequence.FrameMax;

            if (state.ZoomState.MinViewSpan <= 0)
                state.ZoomState.MinViewSpan = 1;

            if (state.ZoomState.ViewMax <= state.ZoomState.ViewMin || state.ZoomState.ViewMax > state.ZoomState.ContentMax || double.IsNaN(state.ZoomState.ViewMax))
            {
                state.ZoomState.ViewMin = sequence.FrameMin;
                double initialSpan = viewWidthPixels / state.framePixelWidth;
                if (initialSpan <= 0 || double.IsNaN(initialSpan) || double.IsInfinity(initialSpan))
                    initialSpan = 100;
                state.ZoomState.ViewMax = state.ZoomState.ViewMin + initialSpan;
            }

            state.ZoomState.ViewMin = Math.Clamp(state.ZoomState.ViewMin, state.ZoomState.ContentMin, state.ZoomState.ContentMax - state.ZoomState.MinViewSpan);
            state.ZoomState.ViewMax = Math.Clamp(state.ZoomState.ViewMax, state.ZoomState.ViewMin + state.ZoomState.MinViewSpan, state.ZoomState.ContentMax);

            firstFrame = (int)Math.Round(state.ZoomState.ViewMin);
            double viewSpan = state.ZoomState.ViewMax - state.ZoomState.ViewMin;
            if (viewSpan <= 0) viewSpan = 1;

            state.framePixelWidth = (float)(viewWidthPixels / viewSpan);
            state.framePixelWidthTarget = state.framePixelWidth;
            var firstFrameUsed = firstFrame;

            var controlHeight = sequenceCount * ItemHeight;
            for (var i = 0; i < sequenceCount; i++)
                controlHeight += sequence.GetCustomHeight(i);
            var frameCount = Math.Max(sequence.FrameMax - sequence.FrameMin, 1);

            var customDraws = new List<CustomDraw>();
            var compactCustomDraws = new List<CustomDraw>();

            var visibleFrameCount = (int)Math.Floor(viewSpan);
            var barWidthRatio = Math.Min(visibleFrameCount / (float)frameCount, 1f);
            var barWidthInPixels = barWidthRatio * (canvas_size.X - legendWidth);

            var regionRect = new ImRect(canvas_pos, canvas_pos + canvas_size);

            frameCount = sequence.FrameMax - sequence.FrameMin;
            if (visibleFrameCount >= frameCount)
            {
                state.ZoomState.ViewMin = sequence.FrameMin;
                state.ZoomState.ViewMax = sequence.FrameMax;
                firstFrame = sequence.FrameMin;
            }

            var hasScrollBar = true;

            var headerSize = new Vector2(canvas_size.X, ItemHeight);
            ImGui.InvisibleButton("topBar", headerSize);
            draw_list.AddRectFilled(canvas_pos, canvas_pos + headerSize, ImGui.GetColorU32(Color_Header_Background), 0f);

            var childFramePos = ImGui.GetCursorScreenPos();
            var childFrameSize = new Vector2(canvas_size.X, controlHeight);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, ImGui.GetColorU32(Color_Content_Background));
            ImGui.BeginChild(SequenceName, childFrameSize, false, ImGuiWindowFlags.NoScrollbar);

            sequence.focused = ImGui.IsWindowFocused();
            ImGui.InvisibleButton("contentBar", new Vector2(canvas_size.X, controlHeight));
            var contentMin = ImGui.GetItemRectMin();
            var contentMax = ImGui.GetItemRectMax();
            var contentRect = new ImRect(contentMin, contentMax);
            var contentHeight = contentMax.Y - contentMin.Y;

            bool clickedOnContent = ImGui.IsMouseClicked(0) && contentRect.Contains(io.MousePos) && ImGui.IsWindowFocused();
            bool clickedOnTrackArea = clickedOnContent && io.MousePos.X > contentMin.X + legendWidth;

            draw_list.AddRectFilled(canvas_pos, canvas_pos + canvas_size, ImGui.GetColorU32(Color_Widget_Background), 0f);

            var topRect = new ImRect(new Vector2(canvas_pos.X + legendWidth, canvas_pos.Y), new Vector2(canvas_pos.X + canvas_size.X, canvas_pos.Y + ItemHeight));
            if (state.movingEntry == -1 && currentFrame >= 0 && topRect.Contains(io.MousePos) && io.MouseDown[0])
            {
                state.MovingCurrentFrame = true;
            }
            if (state.MovingCurrentFrame)
            {
                currentFrame = (int)((io.MousePos.X - topRect.Min.X) / state.framePixelWidth) + firstFrameUsed;
                currentFrame = Math.Clamp(currentFrame, sequence.FrameMin, sequence.FrameMax);
                if (!io.MouseDown[0])
                    state.MovingCurrentFrame = false;
            }

            const int MinimumDistanceBetweenElements = 100;
            var modFrameCount = 5;
            var frameStep = 1;
            while (modFrameCount * state.framePixelWidth < MinimumDistanceBetweenElements)
            {
                modFrameCount *= 2;
                frameStep *= 2;
            }
            var halfModFrameCount = modFrameCount / 2;

            Action<int, int> drawLine = (i, regionHeight) =>
            {
                uint TextColor = ImGui.GetColorU32(Color_Header_Text);
                var baseIndex = i % modFrameCount == 0 || i == sequence.FrameMax || i == sequence.FrameMin;
                var halfIndex = i % halfModFrameCount == 0;
                var px = (float)(contentMin.X + (i - state.ZoomState.ViewMin) * state.framePixelWidth + legendWidth);
                var tiretStart = baseIndex ? 4 : halfIndex ? 10 : 14;
                var tiretEnd = baseIndex ? regionHeight : ItemHeight;

                if (px <= contentMax.X && px >= contentMin.X + legendWidth)
                {
                    draw_list.AddLine(new Vector2(px, canvas_pos.Y + tiretStart), new Vector2(px, canvas_pos.Y + tiretEnd - 1), ImGui.GetColorU32(Color_Header_Lines), 1);
                }
                if (baseIndex && px >= contentMin.X + legendWidth && px <= contentMax.X)
                    draw_list.AddText(new Vector2(px + 3f, canvas_pos.Y), TextColor, $"{i}");
            };

            for (var i = sequence.FrameMin; i <= sequence.FrameMax; i += frameStep)
            {
                drawLine(i, ItemHeight);
            }
            drawLine(sequence.FrameMin, ItemHeight);
            drawLine(sequence.FrameMax, ItemHeight);

            var legendClipRectMin = contentMin;
            var legendClipRectMax = new Vector2(contentMin.X + legendWidth, contentMax.Y);
            draw_list.PushClipRect(legendClipRectMin, legendClipRectMax, true);

            for (int i = 0, customHeight = 0; i < sequenceCount; i++)
            {
                var animation = sequence.GetAnimation(i);
                var tPos = new Vector2(contentMin.X + 3, contentMin.Y + i * ItemHeight + 2 + customHeight);
                var tEndPos = new Vector2(contentMin.X + 3 + legendWidth, contentMin.Y + (i + 1) * ItemHeight + 2 + customHeight);
                var canMouseClickOnRow = new ImRect(tPos, tEndPos).Contains(io.MousePos) &&
                                            io.MousePos.Y > contentMin.Y && io.MousePos.Y <= contentMax.Y &&
                                            ImGui.IsWindowFocused();

                if (canMouseClickOnRow && io.MouseDown[0] && ImGui.IsWindowHovered())
                    selectedEntry = i;

                draw_list.AddText(tPos, ImGui.GetColorU32(Color_Legend_Text), animation.DisplayName ?? animation.Name ?? $"#{i + 1}");

                customHeight += sequence.GetCustomHeight(i);
            }
            draw_list.PopClipRect();

            var contentClipRectMin = new Vector2(contentMin.X + legendWidth, contentMin.Y);
            var contentClipRectMax = contentMax;

            if (currentFrame >= (int)Math.Round(state.ZoomState.ViewMin) && currentFrame <= sequence.FrameMax)
            {
                var cursorOffset = (float)(contentMin.X + legendWidth + (currentFrame - state.ZoomState.ViewMin) * state.framePixelWidth);
                if (cursorOffset >= contentClipRectMin.X && cursorOffset <= contentClipRectMax.X)
                {
                    uint playheadColor = ImGui.GetColorU32(Color_Playhead);
                    uint textColor = ImGui.GetColorU32(Color_Playhead_Text);
                    float headerBottom = canvas_pos.Y + ItemHeight;
                    float rounding = 3f;
                    float padding = 4f;
                    string frameText = $"{currentFrame}";
                    var textSize = ImGui.CalcTextSize(frameText);
                    float triHeight = 5f;
                    float boxHeight = textSize.Y + (padding / 2);
                    float boxWidthHalf = (textSize.X / 2) + padding;
                    float triBaseY = headerBottom - triHeight - 1f;
                    float boxBottom = triBaseY;
                    float boxTop = boxBottom - boxHeight;
                    var boxMin = new Vector2(cursorOffset - boxWidthHalf, boxTop);
                    var boxMax = new Vector2(cursorOffset + boxWidthHalf, boxBottom);
                    draw_list.AddRectFilled(boxMin, boxMax, playheadColor, rounding, ImDrawFlags.RoundCornersTopLeft | ImDrawFlags.RoundCornersTopRight);
                    var triP1 = new Vector2(cursorOffset - boxWidthHalf, boxBottom);
                    var triP2 = new Vector2(cursorOffset + boxWidthHalf, boxBottom);
                    var triP3 = new Vector2(cursorOffset, headerBottom - 1f);
                    draw_list.AddTriangleFilled(triP1, triP2, triP3, playheadColor);
                    float textY = boxTop + (padding / 4);
                    draw_list.AddText(new Vector2(cursorOffset - (textSize.X / 2), textY), textColor, frameText);
                }
            }

            draw_list.PushClipRect(contentClipRectMin, contentClipRectMax, true);

            int hoveredEntry = -1;
            for (int i = 0, customHeight = 0; i < sequenceCount; i++)
            {
                var col = (i & 1) != 0 ? ImGui.GetColorU32(Color_Stripe_1) : ImGui.GetColorU32(Color_Stripe_2);
                var localCustomHeight = sequence.GetCustomHeight(i);
                var pos = new Vector2(contentMin.X + legendWidth, contentMin.Y + ItemHeight * i + 1 + customHeight);
                var sz = new Vector2(contentMax.X, pos.Y + ItemHeight - 1 + localCustomHeight);
                draw_list.AddRectFilled(pos, sz, col, 0f);

                if (new ImRect(pos, sz).Contains(io.MousePos))
                {
                    hoveredEntry = i;
                }

                customHeight += localCustomHeight;
            }

            Action<int, int> drawLineContent = (i, regionHeight) =>
            {
                var px = (float)(contentMin.X + (i - state.ZoomState.ViewMin) * state.framePixelWidth + legendWidth);
                var tiretStart = (int)contentMin.Y;
                var tiretEnd = (int)contentMax.Y;
                if (px <= contentClipRectMax.X && px >= contentClipRectMin.X)
                {
                    var contentLineColorVec = style.Colors[(int)Color_Content_Lines];
                    contentLineColorVec.W *= Color_Content_Lines_Alpha;
                    uint contentLineColor = ImGui.GetColorU32(contentLineColorVec);
                    draw_list.AddLine(new Vector2(px, tiretStart), new Vector2(px, tiretEnd), contentLineColor, 1);
                }
            };
            for (var i = sequence.FrameMin; i <= sequence.FrameMax; i += frameStep)
            {
                drawLineContent(i, (int)contentHeight);
            }
            drawLineContent(sequence.FrameMin, (int)contentHeight);
            drawLineContent(sequence.FrameMax, (int)contentHeight);

            var selected = selectedEntry >= 0;
            if (selected)
            {
                var customHeight = 0;
                for (var i = 0; i < selectedEntry; i++)
                    customHeight += sequence.GetCustomHeight(i);
                var selectionColorVec = style.Colors[(int)Color_Selection];
                selectionColorVec.W *= Color_Selection_Alpha;
                uint selectionColor = ImGui.GetColorU32(selectionColorVec);
                draw_list.AddRectFilled(
                    new Vector2(contentClipRectMin.X, contentMin.Y + ItemHeight * selectedEntry + customHeight),
                    new Vector2(contentClipRectMax.X, contentMin.Y + ItemHeight * (selectedEntry + 1) + customHeight),
                    selectionColor, 1f);
            }

            bool clickedOnKeyframe = false;
            for (int i = 0, customHeight = 0; i < sequenceCount; i++)
            {
                var animation = sequence.GetAnimation(i);
                var localCustomHeight = sequence.GetCustomHeight(i);
                var pos = new Vector2(0, contentMin.Y + ItemHeight * i + 1 + customHeight);

                for (int k = 0; k < animation.GetKeyframeCount(); k++)
                {
                    var keyframe = animation.GetKeyframe(k) as MyKeyframe;
                    if (keyframe == null) continue;

                    float keyframeX = (float)(contentMin.X + legendWidth + (keyframe.Frame - state.ZoomState.ViewMin) * state.framePixelWidth);
                    float y = pos.Y + (ItemHeight / 2);
                    float size = 6f;
                    var keyframeRect = new ImRect(new Vector2(keyframeX - size, y - size), new Vector2(keyframeX + size, y + size));
                    bool isHovered = keyframeRect.Contains(io.MousePos);

                    uint baseColor = keyframe.CustomColor ?? animation.Color;
                    uint drawColor = isHovered ? ImGui.GetColorU32(Color_Keyframe_Hover) : baseColor;

                    if (keyframeX >= contentClipRectMin.X && keyframeX <= contentClipRectMax.X)
                    {
                        switch (keyframe.Shape)
                        {
                            case KeyframeShape.Circle:
                                draw_list.AddCircleFilled(new Vector2(keyframeX, y), size, drawColor);
                                break;

                            case KeyframeShape.Square:
                                var squareMin = new Vector2(keyframeX - size, y - size);
                                var squareMax = new Vector2(keyframeX + size, y + size);
                                draw_list.AddRectFilled(squareMin, squareMax, drawColor);
                                break;

                            case KeyframeShape.Diamond:
                            default:
                                var points = new Vector2[] { new(keyframeX, y - size), new(keyframeX + size, y), new(keyframeX, y + size), new(keyframeX - size, y), new(keyframeX, y - size) };
                                draw_list.AddConvexPolyFilled(ref points[0], points.Length, drawColor);
                                break;
                        }
                    }

                    if (state.movingEntry == -1 && ImGui.IsMouseClicked(0) && isHovered)
                    {
                        if (!new ImRect(contentClipRectMin, contentClipRectMax).Contains(io.MousePos))
                            continue;
                        if (!state.MovingCurrentFrame)
                        {
                            state.movingEntry = i;
                            state.movingKeyframeIndex = k;
                            state.movingPos = cx;
                            sequence.BeginEdit(state.movingEntry);
                            clickedOnKeyframe = true;
                        }
                    }
                }

                if (localCustomHeight > 0)
                {
                    var rp = new Vector2(contentMin.X, contentMin.Y + ItemHeight * i + 1 + customHeight);
                    var customRect = new ImRect(rp, rp + new Vector2(canvas_size.X - legendWidth, localCustomHeight));
                    var clippingRect = new ImRect(contentClipRectMin, contentClipRectMax);
                    var legendRect = new ImRect(contentMin + new Vector2(0, ItemHeight * i + 1 + customHeight), contentMin + new Vector2(legendWidth, ItemHeight * i + 1 + customHeight + localCustomHeight));
                    var legendClippingRect = new ImRect(legendClipRectMin, legendClipRectMax);
                    customDraws.Add(new CustomDraw { index = i, customRect = customRect, legendRect = legendRect, clippingRect = clippingRect, legendClippingRect = legendClippingRect });
                }
                else
                {
                    var rp = new Vector2(contentMin.X, contentMin.Y + ItemHeight * i + customHeight);
                    var customRect = new ImRect(rp + new Vector2(legendWidth, 0), rp + new Vector2(canvas_size.X, ItemHeight));
                    var clippingRect = new ImRect(contentClipRectMin, contentClipRectMax);
                    compactCustomDraws.Add(new CustomDraw { index = i, customRect = customRect, clippingRect = clippingRect });
                }
                customHeight += localCustomHeight;
            }

            if (clickedOnTrackArea && !clickedOnKeyframe && state.movingEntry == -1 && !state.MovingCurrentFrame)
            {
                if (hoveredEntry >= 0)
                {
                    selectedEntry = hoveredEntry;
                    state.movingKeyframeIndex = -1;
                }
                else
                {
                    selectedEntry = -1;
                    state.movingKeyframeIndex = -1;
                }
            }

            if (state.movingEntry >= 0)
            {
                ImGui.SetNextFrameWantCaptureMouse(true);
                var diffX = cx - state.movingPos;
                if (state.movingKeyframeIndex != -1)
                {
                    var animation = sequence.GetAnimation(state.movingEntry);
                    var keyframe = animation.GetKeyframe(state.movingKeyframeIndex);
                    var potentialDiffFrame = (int)Math.Round((double)diffX / state.framePixelWidth);

                    if (Math.Abs(potentialDiffFrame) > 0)
                    {
                        int originalFrame = keyframe.Frame;
                        int newFrameUnclamped = originalFrame + potentialDiffFrame;
                        int newFrameClamped = Math.Clamp(newFrameUnclamped, sequence.FrameMin, sequence.FrameMax);

                        if (originalFrame != newFrameClamped)
                        {
                            keyframe.Frame = newFrameClamped;
                            selectedEntry = state.movingEntry;
                            ret = true;
                        }
                        int actualDiffFrame = newFrameClamped - originalFrame;
                        state.movingPos += (int)Math.Round(actualDiffFrame * state.framePixelWidth);
                    }
                }
                if (!io.MouseDown[0])
                {
                    if (state.movingKeyframeIndex != -1)
                    {
                        var animation = sequence.GetAnimation(state.movingEntry);
                        if (animation is MyAnimation myAnim) { myAnim.Keyframes.Sort((a, b) => a.Frame.CompareTo(b.Frame)); }
                        selectedEntry = state.movingEntry;
                        ret = true;
                    }
                    state.movingEntry = -1;
                    sequence.EndEdit();
                }
            }

            if (currentFrame >= (int)Math.Round(state.ZoomState.ViewMin) && currentFrame <= sequence.FrameMax)
            {
                var cursorOffset = (float)(contentMin.X + legendWidth + (currentFrame - state.ZoomState.ViewMin) * state.framePixelWidth);
                if (cursorOffset >= contentClipRectMin.X && cursorOffset <= contentClipRectMax.X)
                {
                    uint playheadColor = ImGui.GetColorU32(Color_Playhead);
                    var playheadGlowVec = style.Colors[(int)Color_Playhead];
                    playheadGlowVec.W *= Color_Playhead_Glow_Alpha;
                    uint playheadGlow = ImGui.GetColorU32(playheadGlowVec);
                    float lineTop = contentClipRectMin.Y;
                    float lineBottom = contentClipRectMax.Y;
                    draw_list.AddLine(new Vector2(cursorOffset, lineTop), new Vector2(cursorOffset, lineBottom), playheadGlow, 3f);
                    draw_list.AddLine(new Vector2(cursorOffset, lineTop), new Vector2(cursorOffset, lineBottom), playheadColor, 1f);
                }
            }

            foreach (var customDraw in customDraws)
                sequence.CustomDraw(customDraw.index, draw_list, customDraw.customRect, customDraw.legendRect, customDraw.clippingRect, customDraw.legendClippingRect);
            foreach (var customDraw in compactCustomDraws)
                sequence.CustomDrawCompact(customDraw.index, draw_list, customDraw.customRect, customDraw.clippingRect);

            draw_list.PopClipRect();

            ImGui.EndChild();
            ImGui.PopStyleColor();

            var scrollbarCursorPos = ImGui.GetCursorPos();
            ImGui.SetCursorPosX(scrollbarCursorPos.X + legendWidth);
            ImGui.SetItemAllowOverlap();
            bool scrollbarChanged = ZoomScrollbar.Draw("sequencer_zoom", ref state.ZoomState, 14.0f);
            if (scrollbarChanged)
            {
                firstFrame = (int)Math.Round(state.ZoomState.ViewMin);
                viewSpan = state.ZoomState.ViewMax - state.ZoomState.ViewMin;
                if (viewSpan <= 0) viewSpan = 1;
                state.framePixelWidth = (float)(viewWidthPixels / viewSpan);
                state.framePixelWidthTarget = state.framePixelWidth;
            }
            ImGui.SetCursorPos(scrollbarCursorPos + new Vector2(0, 14.0f));

            ImGui.EndGroup();

            return ret;
        }
    }
}
