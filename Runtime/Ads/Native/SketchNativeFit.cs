using UnityEngine;

namespace Sketch.Sdk
{
    public static class SketchNativeFit
    {
        public const float MinClosePixels = 88f;
        public const float MinPopupScale = 0.6f;
        public const float GoodPopupScale = 0.8f;

        public enum Side { Left = 0, Right = 1 }
        public enum FitReason { Fits = 0, InvalidInput, ContentOutsideSafe, NoRoomHorizontal, NoRoomVertical }

        public struct Input
        {
            public Side Side;
            public Rect Safe;
            public Rect Content;
            public float ScreenHeight;
            public float BannerHeightPixels;
            public float MinWidth, PreferredWidth, MaxWidth;
            public float Gap, Margin, BottomReserve;
            public float HeightRatio, MinHeight, CloseSize;
            public Rect[] KeepClear;
        }

        public struct Result
        {
            public bool Fits;
            public FitReason Reason;
            public Rect Native;
            public Rect Close;
            public float PopupScale;
            public float PopupShiftX;
            public Rect ShiftedContent;
        }

        public static Input Defaults(Rect safe, Rect content, float screenHeight, float bannerHeightPixels = 0f)
        {
            float k = Mathf.Max(0.5f, screenHeight / 1080f);
            return new Input
            {
                Safe = safe,
                Content = content,
                ScreenHeight = screenHeight,
                BannerHeightPixels = bannerHeightPixels,
                MinWidth = Mathf.Max(safe.width * 0.26f, 264f),
                PreferredWidth = safe.width * 0.38f,
                MaxWidth = safe.width * 0.5f,
                Gap = 24f * k,
                Margin = 24f * k,
                BottomReserve = Mathf.Max(bannerHeightPixels, 200f * k),
                HeightRatio = 1.25f,
                MinHeight = 300f * k,
                CloseSize = Mathf.Max(88f, 88f * k),
            };
        }

        public static Result Compute(Input input)
        {
            if (input.Side != Side.Right) return ComputeLeft(input);
            float axis = input.Safe.xMin + input.Safe.xMax;
            Input mirrored = input;
            mirrored.Side = Side.Left;
            mirrored.Content = MirrorX(input.Content, axis);
            if (input.KeepClear != null)
            {
                mirrored.KeepClear = new Rect[input.KeepClear.Length];
                for (int i = 0; i < input.KeepClear.Length; i++) mirrored.KeepClear[i] = MirrorX(input.KeepClear[i], axis);
            }
            Result result = ComputeLeft(mirrored);
            result.Native = MirrorX(result.Native, axis);
            result.Close = MirrorX(result.Close, axis);
            result.ShiftedContent = MirrorX(result.ShiftedContent, axis);
            result.PopupShiftX = result.Fits ? result.ShiftedContent.xMin - input.Content.xMin : 0f;
            return result;
        }

        public static Rect FixedSquare(int screenWidth, int screenHeight, float bannerHeightPixels)
        {
            float shortSide = Mathf.Min(screenWidth, screenHeight);
            float longSide = Mathf.Max(screenWidth, screenHeight);
            bool squareScreen = shortSide > 0f && longSide / shortSide < 1.3f;
            float available = Mathf.Max(0f, screenHeight - Mathf.Max(0f, bannerHeightPixels));
            float side = Mathf.Round(Mathf.Min(squareScreen ? shortSide * 0.7f : shortSide, available));
            return new Rect(0f, screenHeight - side, side, side);
        }

        public static Rect SideColumn(Rect safe, float screenHeight, Side side, float bannerHeightPixels, out Rect closeStrip)
        {
            float k = Mathf.Max(0.5f, screenHeight / 1080f);
            float margin = 24f * k;
            float gap = 24f * k;
            float close = Mathf.Max(88f, 88f * k);
            float width = Mathf.Min(safe.width * 0.34f, 640f * k);
            float bottom = safe.yMin + Mathf.Max(bannerHeightPixels, 200f * k);
            float top = safe.yMax - margin - close - gap;
            float height = Mathf.Max(0f, Mathf.Min(width * 1.25f, top - bottom));
            float x = side == Side.Right ? safe.xMax - margin - width : safe.xMin + margin;
            float y = bottom + Mathf.Max(0f, top - bottom - height) * 0.5f;
            Rect native = new Rect(x, y, width, height);
            float closeX = side == Side.Right ? native.xMax - close : native.xMin;
            closeStrip = new Rect(closeX, native.yMax + gap, close, close);
            return native;
        }

        public static Rect PixelsToDp(Rect pixels, float screenHeight, float deviceScale)
        {
            if (float.IsNaN(deviceScale) || float.IsInfinity(deviceScale) || deviceScale <= 0f)
                throw new System.ArgumentOutOfRangeException(nameof(deviceScale));
            return new Rect(pixels.xMin / deviceScale, (screenHeight - pixels.yMax) / deviceScale,
                pixels.width / deviceScale, pixels.height / deviceScale);
        }

        static Result ComputeLeft(Input input)
        {
            Result result = new Result { Reason = FitReason.InvalidInput, PopupScale = 1f };
            Rect safe = input.Safe;
            Rect content = input.Content;
            if (safe.width <= 0f || safe.height <= 0f || content.width <= 0f || content.height <= 0f) return result;
            float close = Mathf.Max(MinClosePixels, input.CloseSize);
            float margin = Mathf.Max(0f, input.Margin);
            float gap = Mathf.Max(0f, input.Gap);
            float minWidth = Mathf.Max(input.MinWidth, close * 3f);
            float maxWidth = Mathf.Max(minWidth, input.MaxWidth);
            float preferred = Mathf.Clamp(input.PreferredWidth, minWidth, maxWidth);
            if (content.width >= safe.width * 0.9f || content.yMin < safe.yMin - 0.5f || content.yMax > safe.yMax + 0.5f)
                return With(result, FitReason.ContentOutsideSafe);
            if (content.width > safe.width - 2f * margin || content.xMin < safe.xMin - 0.5f || content.xMax > safe.xMax + 0.5f)
                return TryScale(input, safe, content, margin, gap, close, minWidth, preferred, out Result outside) ? outside : With(result, FitReason.ContentOutsideSafe);
            float rightLimit = safe.xMax - margin;
            float nativeLeft = safe.xMin + margin;
            float maxShift = Mathf.Max(0f, rightLimit - content.xMax);
            float shift = Mathf.Clamp(nativeLeft + preferred + gap - content.xMin, 0f, maxShift);
            float nativeRight = Mathf.Min(content.xMin + shift - gap, nativeLeft + maxWidth);
            float width = nativeRight - nativeLeft;
            if (width < minWidth - 0.01f)
                return TryScale(input, safe, content, margin, gap, close, minWidth, preferred, out Result scaled) ? scaled : With(result, FitReason.NoRoomHorizontal);
            float bottom = safe.yMin + Mathf.Max(input.BottomReserve, input.BannerHeightPixels);
            float top = safe.yMax - margin;
            if (!PickVertical(bottom, top, nativeLeft, nativeLeft + width, width * Mathf.Max(0.1f, input.HeightRatio), Mathf.Max(1f, input.MinHeight), content.center.y, input.KeepClear, out float y, out float height))
                return With(result, FitReason.NoRoomVertical);
            result.Native = new Rect(nativeLeft, y, width, height);
            result.Close = CloseRect(result.Native, close);
            result.PopupShiftX = shift;
            result.ShiftedContent = new Rect(content.x + shift, content.y, content.width, content.height);
            result.Fits = true;
            result.Reason = FitReason.Fits;
            return result;
        }

        static bool TryScale(Input input, Rect safe, Rect content, float margin, float gap, float close, float minWidth, float preferred, out Result result)
        {
            result = new Result { Reason = FitReason.NoRoomHorizontal, PopupScale = 1f };
            float rightLimit = safe.xMax - margin;
            float nativeLeft = safe.xMin + margin;
            float bottom = safe.yMin + Mathf.Max(input.BottomReserve, input.BannerHeightPixels);
            float top = safe.yMax - margin;
            for (int pass = 0; pass < 3; pass++)
            {
                float requiredScale = pass == 0 ? GoodPopupScale : MinPopupScale;
                for (int step = 0; step < 4; step++)
                {
                    float width = pass < 2 ? Mathf.Lerp(preferred, minWidth, step / 3f) : Mathf.Lerp(minWidth, close * 3f, step / 3f);
                    if (!PickVertical(bottom, top, nativeLeft, nativeLeft + width, width * Mathf.Max(0.1f, input.HeightRatio), Mathf.Max(1f, input.MinHeight), content.center.y, input.KeepClear, out float y, out float height))
                    {
                        result.Reason = FitReason.NoRoomVertical;
                        continue;
                    }
                    float regionLeft = nativeLeft + width + gap;
                    float scale = Mathf.Min(1f, (rightLimit - regionLeft) / content.width);
                    scale = Mathf.Min(scale, (safe.height - 2f * margin) / content.height);
                    if (scale < requiredScale) continue;
                    result.Native = new Rect(nativeLeft, y, width, height);
                    result.Close = CloseRect(result.Native, close);
                    result.PopupScale = scale;
                    result.PopupShiftX = regionLeft - content.xMin;
                    result.ShiftedContent = new Rect(regionLeft, content.center.y - content.height * scale * 0.5f, content.width * scale, content.height * scale);
                    result.Fits = true;
                    result.Reason = FitReason.Fits;
                    return true;
                }
            }
            return false;
        }

        static bool PickVertical(float bottom, float top, float xMin, float xMax, float desired, float minimum, float center, Rect[] blocks, out float y, out float height)
        {
            y = 0f;
            height = 0f;
            if (top - bottom < minimum) return false;
            float best = float.MaxValue;
            int count = blocks == null ? 0 : blocks.Length;
            for (int candidate = -1; candidate < count; candidate++)
            {
                float bandBottom = candidate < 0 ? bottom : Mathf.Max(bottom, blocks[candidate].yMax);
                float bandTop = top;
                for (int i = 0; i < count; i++)
                {
                    Rect block = blocks[i];
                    if (block.width <= 0f || block.height <= 0f || block.xMax <= xMin || block.xMin >= xMax || block.yMax <= bandBottom) continue;
                    bandTop = Mathf.Min(bandTop, block.yMin);
                }
                float bandHeight = bandTop - bandBottom;
                if (bandHeight < minimum) continue;
                float h = Mathf.Min(desired, bandHeight);
                float cy = Mathf.Clamp(center - h * 0.5f, bandBottom, bandTop - h);
                float score = (desired - h) * 4f + Mathf.Abs(cy + h * 0.5f - center);
                if (score >= best) continue;
                best = score;
                y = cy;
                height = h;
            }
            return best < float.MaxValue;
        }

        static Rect CloseRect(Rect native, float size) => new Rect(native.xMax - size, native.yMax - size, size, size);
        static Rect MirrorX(Rect rect, float axis) => new Rect(axis - rect.xMax, rect.y, rect.width, rect.height);
        static Result With(Result result, FitReason reason) { result.Reason = reason; return result; }
    }
}
