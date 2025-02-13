using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SpikeFinder.Controls
{
    public class RegionHighlighter : Control
    {
        public RegionHighlighter()
        {
            Focusable = false;
            FocusVisualStyle = null;
            Cursor = Cursors.Hand;
        }

        public double? StartPosition
        {
            get => GetValue(StartPositionProperty) as double?;
            set => SetValue(StartPositionProperty, value);
        }
        public static readonly DependencyProperty StartPositionProperty = DependencyProperty.Register(nameof(StartPosition), typeof(double?), typeof(RegionHighlighter), new FrameworkPropertyMetadata(default(double?), FrameworkPropertyMetadataOptions.AffectsRender));

        public double? TentativeEndPosition
        {
            get => GetValue(TentativeEndPositionProperty) as double?;
            set => SetValue(TentativeEndPositionProperty, value);
        }
        public static readonly DependencyProperty TentativeEndPositionProperty = DependencyProperty.Register(nameof(TentativeEndPosition), typeof(double?), typeof(RegionHighlighter), new FrameworkPropertyMetadata(default(double?), FrameworkPropertyMetadataOptions.AffectsRender));

        public double? EndPosition
        {
            get => GetValue(EndPositionProperty) as double?;
            set => SetValue(EndPositionProperty, value);
        }
        public static readonly DependencyProperty EndPositionProperty = DependencyProperty.Register(nameof(EndPosition), typeof(double?), typeof(RegionHighlighter), new FrameworkPropertyMetadata(default(double?), FrameworkPropertyMetadataOptions.AffectsRender));

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            if (Mouse.Captured == this)
            {
                ReleaseMouseCapture();
                return;
            }

            if (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers == ModifierKeys.None && e.RightButton == MouseButtonState.Released && e.MiddleButton == MouseButtonState.Released && e.XButton1 == MouseButtonState.Released && e.XButton2 == MouseButtonState.Released)
            {
                TentativeEndPosition = StartPosition = e.GetPosition(this).X / RenderSize.Width;
                EndPosition = null;
                Mouse.Capture(this);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (Mouse.Captured != this)
                return;

            TentativeEndPosition = e.GetPosition(this).X / RenderSize.Width;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (Mouse.Captured != this)
                return;

            if (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers == ModifierKeys.None && e.RightButton == MouseButtonState.Released && e.MiddleButton == MouseButtonState.Released && e.XButton1 == MouseButtonState.Released && e.XButton2 == MouseButtonState.Released)
            {
                TentativeEndPosition = null;
                EndPosition = e.GetPosition(this).X / RenderSize.Width;
                ReleaseMouseCapture();
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

            var val1 = StartPosition;
            var val2 = TentativeEndPosition ?? EndPosition;

            if (val1 is { } x && val2 is { } y)
            {
                var brush = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));

                var min = Math.Min(x, y);
                var max = Math.Max(x, y);

                static Pen CreateDashedPen() => new(Brushes.Gray, 1) { DashStyle = new DashStyle([5.0, 5.0], 0) };
                Pen? dashedPen = null;

                if (min > 0 && min < 1)
                {
                    drawingContext.DrawRectangle(brush, null, new Rect(0, 0, min * RenderSize.Width, RenderSize.Height));
                    drawingContext.DrawLine(dashedPen ??= CreateDashedPen(), new Point(min * RenderSize.Width, 0), new Point(min * RenderSize.Width, RenderSize.Height));
                }

                if (max > 0 && max < 1)
                {
                    drawingContext.DrawRectangle(brush, null, new Rect(max * RenderSize.Width, 0, (1 - max) * RenderSize.Width, RenderSize.Height));
                    drawingContext.DrawLine(dashedPen ??= CreateDashedPen(), new Point(max * RenderSize.Width, 0), new Point(max * RenderSize.Width, RenderSize.Height));
                }
            }
        }
    }
}
