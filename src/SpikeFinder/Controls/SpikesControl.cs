using ReactiveUI;
using SpikeFinder.Attributes;
using SpikeFinder.Extensions;
using SpikeFinder.Models;
using SpikeFinder.RefractiveIndices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using static System.Windows.Input.Cursors;

namespace SpikeFinder.Controls
{
    class SpikesControl : Control, IActivatableView
    {
        public SpikesControl()
        {
            AllowDrop = true;
            IsManipulationEnabled = true;

            ContextMenu = (ContextMenu)FindResource("SpikesControlContextMenu");
            ContextMenu.DataContext = this;

            this.WhenActivated(d =>
            {
                DeleteCursorCommand = ReactiveCommand.Create<CursorPosition, CursorPosition>(x => x);
                AddMissingCursorCommand = ReactiveCommand.Create<CursorPosition, CursorPosition>(x => x);

                if (PresentationSource.FromVisual(this) is HwndSource source)
                {
                    HwndSourceHook hook = WindowProc;
                    source.AddHook(hook);
                    d(Disposable.Create(() => source.RemoveHook(hook)));
                }

                // TODO: Get finger-pinch working.
                //Point? touchManipulationOrigin = null;

                //// Wire up touch input
                //d(this.Events().ManipulationStarted
                //    .Do(_ => Debug.WriteLine("Manipulation Started!"))
                //    .Do(x => touchManipulationOrigin = (x.ManipulationContainer as Visual)?.PointToScreen(x.ManipulationOrigin))
                //    .Do(_ => Manipulation.SetManipulationMode(this, ManipulationModes.Scale | ManipulationModes.Translate))
                    
                //    .Subscribe());

                //d(this.Events().ManipulationDelta
                //    .Do(x =>
                //    {
                //        var delta = x.DeltaManipulation;

                //        if (touchManipulationOrigin.HasValue && (delta.Scale.X is not 1 || delta.Scale.Y is not 1))
                //        {
                //            Debug.WriteLine($"Zooming relative to position {touchManipulationOrigin.Value + x.CumulativeManipulation.Translation}");

                //            var screenPoint = PointFromScreen(touchManipulationOrigin.Value + x.CumulativeManipulation.Translation);

                //            //var scaleBy = delta.Scale.Length / Math.Sqrt(2);
                //            //scaleBy = scaleBy > 1 ? 1.05 : 1 / 1.05;
                //            ChangeZoom(screenPoint, delta.Scale.X);

                //            //Manipulation.SetManipulationMode(this, ManipulationModes.None);
                //        }
                //    })
                //    .Subscribe());

                d(DeleteCursorCommand.WhereNotNull().Do(x => x.X = null).Subscribe(_ => InvalidateVisual()));
                d(DeleteCursorCommand);
                d(AddMissingCursorCommand.WhereNotNull().Do(x => x.X = PixelToDragTo(_contextMenuRightClickLocation)).Subscribe(_ => InvalidateVisual()));
                d(AddMissingCursorCommand);

                d(this.WhenAnyValue(x => x.Cursors)
                    .SelectMany(x => x)
                    .Select(x => x.WhenAnyValue(y => y.X)
                                .Select(y => y.HasValue)
                                .DistinctUntilChanged()
                                .CombineLatest(Observable.Return(x), (y, z) => z))
                    .Merge() // Switch() does not work. We need to listen to _all_ cursors, not just the last cursor.
                    .Throttle(TimeSpan.FromMilliseconds(5), RxApp.MainThreadScheduler)
                    .Select(_ => (Cursors as IEnumerable<CursorPosition> ?? new CursorPosition[0]).Where(x => !x.X.HasValue).OrderBy(x => CursorElementsInOrder[x.CursorElement]).ToList())
                    .BindTo(this, x => x.UnmarkedCursors));
            });
        }

        // WPF really does a really bad job of handling input from precision touchpads. Here, we hook and intercept WM_MOUSEWHEEL and WM_MOUSEHWHEEL messages to handle all scrolling/zooming.
        private IntPtr WindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_MOUSEHWHEEL = 0x020E;
            const int WM_MOUSEWHEEL = 0x020A;
            const int WHEEL_DELTA = 120;
            const int MK_SHIFT = 0x0004;
            const int MK_CONTROL = 0x0008;

            if (msg is WM_MOUSEHWHEEL or WM_MOUSEWHEEL)
            {
                var wParamInt = (int)(wParam.ToInt64() & 0xFFFFFFFF);

                var fwKeys = wParamInt & 0xffff;
                var zDelta = wParamInt >> 16;

                IntPtr DoHorizontalScroll(bool invertScroll, ref bool handled)
                {
                    if (this.GetVisualTreeParents().OfType<ScrollViewer>().FirstOrDefault(x => x.ComputedHorizontalScrollBarVisibility == Visibility.Visible) is { } scrollViewer)
                    {
                        scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + (invertScroll ? -zDelta : zDelta));
                        handled = true;
                    }

                    return IntPtr.Zero;
                }
                IntPtr DoVerticalScroll(ref bool handled)
                {
                    if (this.GetVisualTreeParents().OfType<ScrollViewer>().FirstOrDefault(x => x.ComputedVerticalScrollBarVisibility == Visibility.Visible) is { } scrollViewer)
                    {
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - zDelta);

                        if ((_draggingCursor ?? CursorToDrag(Mouse.GetPosition(this))) != null)
                        {
                            // If we're drawing lines between two cursors, invalidate to redraw the lines.
                            InvalidateVisual();
                        }
                        handled = true;
                    }

                    return IntPtr.Zero;
                }

                switch (msg, fwKeys)
                {
                    case (WM_MOUSEWHEEL, MK_CONTROL):
                        // We're scrolling up/down and holding the control key, or the user is pinching a precision touchpad.
                        // Either way, we're changing zoom.

                        var lparam = (int)(lParam.ToInt64() & 0xFFFFFFFF);

                        var p = PointFromScreen(new Point(lparam << 16 >> 16, lparam >> 16));

                        ChangeZoom(p, Math.Pow(1.05, zDelta / (double)WHEEL_DELTA));

                        handled = true;
                        return IntPtr.Zero;

                    case (WM_MOUSEHWHEEL, 0x0000):
                        // Horizontal scrolling with no modifiers.
                        return DoHorizontalScroll(false, ref handled);

                    case (WM_MOUSEWHEEL, MK_CONTROL | MK_SHIFT):
                    case (WM_MOUSEWHEEL, MK_SHIFT):
                        // We're holding the shift key and scrolling up/down.
                        // If we're scrolling with a precision touch pad (which we're going to guess based on zDelta being a multiple of WHEEL_DELTA), we process the scroll as though the user weren't holding shift.
                        return zDelta % WHEEL_DELTA != 0 ? DoVerticalScroll(ref handled) : DoHorizontalScroll(true, ref handled);

                    case (WM_MOUSEWHEEL, 0x0000):
                        // Handle default vertical scrolling.
                        return DoVerticalScroll(ref handled);

                }
            }

            return IntPtr.Zero;
        }

        private void ChangeZoom(Point relativeTo, double changeInZoom)
        {
            //Debug.WriteLine($"ChangeZoom was called relativeTo ({relativeTo})");

            if (this.GetVisualTreeParents().OfType<ScrollViewer>().FirstOrDefault() is { } scrollViewer)
            {
                var newZoom = (double)CoerceZoomCallback(this, Zoom * changeInZoom);

                if (Math.Abs(newZoom - Zoom) >= 0.0001)
                {
                    // We're either zooming in or out...

                    double GetScrollOffset(double currentMousePosition, double currentScrollOffset, double scrollViewportSize, double paddingBefore)
                    {
                        var mousePositionAsPercent = (currentMousePosition - currentScrollOffset) / scrollViewportSize;
                        mousePositionAsPercent = mousePositionAsPercent < 0.1 ? 0 : (mousePositionAsPercent > 0.9 ? 1 : mousePositionAsPercent);

                        var currentOffset = currentScrollOffset + mousePositionAsPercent * scrollViewportSize - paddingBefore;
                        return currentOffset * newZoom / Zoom - mousePositionAsPercent * scrollViewportSize + paddingBefore;
                    }

                    var newHorizontalOffset = GetScrollOffset(relativeTo.X, scrollViewer.HorizontalOffset, scrollViewer.ViewportWidth, Padding.Left);
                    var newVerticalOffset = GetScrollOffset(relativeTo.Y, scrollViewer.VerticalOffset, scrollViewer.ViewportHeight, Padding.Top);

                    Zoom = newZoom;

                    scrollViewer.ScrollToHorizontalOffset(newHorizontalOffset);
                    scrollViewer.ScrollToVerticalOffset(newVerticalOffset);
                }
            }
        }

        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }
        public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(SpikesControl), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public MeasureMode MeasureMode
        {
            get => (MeasureMode)GetValue(MeasureModeProperty);
            set => SetValue(MeasureModeProperty, value);
        }
        public static readonly DependencyProperty MeasureModeProperty = DependencyProperty.Register(nameof(MeasureMode), typeof(MeasureMode), typeof(SpikesControl), new FrameworkPropertyMetadata(MeasureMode.PHAKIC, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Wavelength
        {
            get => (double)GetValue(WavelengthProperty);
            set => SetValue(WavelengthProperty, value);
        }
        public static readonly DependencyProperty WavelengthProperty = DependencyProperty.Register(nameof(Wavelength), typeof(double), typeof(SpikesControl), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public ImageSource? SpikesImage
        {
            get => (ImageSource)GetValue(SpikesImageProperty);
            set => SetValue(SpikesImageProperty, value);
        }
        public static readonly DependencyProperty SpikesImageProperty = DependencyProperty.Register(nameof(SpikesImage), typeof(ImageSource), typeof(SpikesControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public double[]? Spikes
        {
            get => (double[])GetValue(SpikesProperty);
            set => SetValue(SpikesProperty, value);
        }
        public static readonly DependencyProperty SpikesProperty = DependencyProperty.Register(nameof(Spikes), typeof(double[]), typeof(SpikesControl));

        public ObservableCollection<CursorPosition>? Cursors
        {
            get => (ObservableCollection<CursorPosition>)GetValue(CursorsProperty);
            set => SetValue(CursorsProperty, value);
        }
        public static readonly DependencyProperty CursorsProperty = DependencyProperty.Register(nameof(Cursors), typeof(ObservableCollection<CursorPosition>), typeof(SpikesControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        private static readonly Dictionary<CursorElement, int> CursorElementsInOrder = new()
        {
            { CursorElement.AnteriorCornea, 0 },
            { CursorElement.PosteriorCornea, 1 },
            { CursorElement.AnteriorLens, 2 },
            { CursorElement.PosteriorLens, 3 },
            { CursorElement.ILM, 4 },
            { CursorElement.RPE, 5 },
        };

        public List<CursorPosition> UnmarkedCursors
        {
            get => (List<CursorPosition>)GetValue(UnmarkedCursorsProperty);
            set => SetValue(UnmarkedCursorsProperty, value);
        }
        public static readonly DependencyProperty UnmarkedCursorsProperty = DependencyProperty.Register(nameof(UnmarkedCursors), typeof(List<CursorPosition>), typeof(SpikesControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }
        public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(SpikesControl), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure, null, CoerceZoomCallback));
        private static object CoerceZoomCallback(DependencyObject d, object baseValue) => baseValue is double newZoom ? Math.Max(MinZoom, Math.Min(newZoom, MaxZoom)) : baseValue;

        public ReactiveCommand<CursorPosition, CursorPosition> DeleteCursorCommand
        {
            get => (ReactiveCommand<CursorPosition, CursorPosition>)GetValue(DeleteCursorCommandProperty);
            private set => SetValue(DeleteCursorCommandProperty, value);
        }
        public static readonly DependencyProperty DeleteCursorCommandProperty = DependencyProperty.Register(nameof(DeleteCursorCommand), typeof(ReactiveCommand<CursorPosition, CursorPosition>), typeof(SpikesControl));

        public ReactiveCommand<CursorPosition, CursorPosition> AddMissingCursorCommand
        {
            get => (ReactiveCommand<CursorPosition, CursorPosition>)GetValue(AddMissingCursorCommandProperty);
            private set => SetValue(AddMissingCursorCommandProperty, value);
        }
        public static readonly DependencyProperty AddMissingCursorCommandProperty = DependencyProperty.Register(nameof(AddMissingCursorCommand), typeof(ReactiveCommand<CursorPosition, CursorPosition>), typeof(SpikesControl));


        private CursorPosition? _draggingCursor;
        private Point _lastDragPosition;
        private Point _contextMenuRightClickLocation;
        private bool _wasQueryCursorHandLastTime;

        private const double DragToPointWithin = 90;
        private const double CursorStartDragWithin = 15;
        private const double CursorLineLength = 10;
        private const double MinZoom = 0.25;
        private const double MaxZoom = 4.00;


        protected override void OnQueryCursor(QueryCursorEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released && e.RightButton == MouseButtonState.Released && e.MiddleButton == MouseButtonState.Released && CursorToDrag(e.GetPosition(this)) is { } cursor)
            {
                if (!_wasQueryCursorHandLastTime)
                    InvalidateVisual();
                _wasQueryCursorHandLastTime = true;
                e.Cursor = Hand;
            }
            else if (_wasQueryCursorHandLastTime)
            {
                _wasQueryCursorHandLastTime = false;
                InvalidateVisual();
            }

            base.OnQueryCursor(e);
        }
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.RightButton == MouseButtonState.Released && e.MiddleButton == MouseButtonState.Released && Keyboard.Modifiers == ModifierKeys.None && CursorToDrag(e.GetPosition(this)) is { } dragCursor)
            {
                DragDrop.DoDragDrop(this, _draggingCursor = dragCursor.BeginDrag(), DragDropEffects.Move);
            }
            else if (e.ChangedButton == MouseButton.Right && e.LeftButton == MouseButtonState.Released && e.MiddleButton == MouseButtonState.Released && Keyboard.Modifiers == ModifierKeys.None)
            {
                _contextMenuRightClickLocation = e.GetPosition(this);

                if (ContextMenu?.Items.OfType<MenuItem>().FirstOrDefault(x => x.Name == "DeleteCursor") is { } item)
                {
                    item.Tag = CursorToDrag(e.GetPosition(this));
                }
            }


            base.OnMouseLeftButtonDown(e);
        }

        private void AdjustDependentCursorPosition(CursorPosition? cursor)
        {
            if (cursor?.CursorElement is CursorElement.ILM or CursorElement.RPE)
            {
                if (Cursors?.FirstOrDefault(x => x.CursorElement is CursorElement.ILM or CursorElement.RPE && x.CursorElement != cursor.CursorElement) is { } otherCursor)
                {
                    otherCursor.X = cursor.X + 350 * (cursor.CursorElement == CursorElement.ILM ? 1 : -1);
                }
            }
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(CursorPosition)) && e.Data.GetData(typeof(CursorPosition)) is CursorPosition cursor)
            {
                _draggingCursor = cursor;
                _lastDragPosition = e.GetPosition(this);
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }

            base.OnDragEnter(e);
        }
        protected override void OnDragLeave(DragEventArgs e)
        {
            _draggingCursor = _draggingCursor!.CancelDrop();
            AdjustDependentCursorPosition(_draggingCursor);

            e.Effects = DragDropEffects.None;
            e.Handled = true;
            InvalidateVisual();

            base.OnDragLeave(e);
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(CursorPosition)) &&
                e.Data.GetData(typeof(CursorPosition)) is CursorPosition cursor && cursor == _draggingCursor)
            {
                _draggingCursor.X = PixelToDragTo(_lastDragPosition = e.GetPosition(this));
                AdjustDependentCursorPosition(_draggingCursor);

                InvalidateVisual();
            }

            base.OnDragOver(e);
        }

        protected override void OnDrop(DragEventArgs e)
        {
            _draggingCursor = null;
            InvalidateVisual();

            base.OnDrop(e);
        }

        protected override void OnGiveFeedback(GiveFeedbackEventArgs e)
        {
            if (e.Effects == DragDropEffects.Move)
            {
                e.UseDefaultCursors = false;
                e.Handled = true;

                Mouse.SetCursor(Hand);
            }

            base.OnGiveFeedback(e);
        }



        protected override Size MeasureOverride(Size constraint)
        {
            if (SpikesImage is null)
                return new(Padding.Left + Padding.Right, Padding.Top + Padding.Bottom);
            else
                return new(Math.Min(SpikesImage.Width * Zoom + Padding.Left + Padding.Right, constraint.Width), Math.Min(SpikesImage.Height * Zoom + Padding.Top + Padding.Bottom, constraint.Height));
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (SpikesImage is null)
                return;

            drawingContext.DrawImage(SpikesImage, new Rect(Padding.Left, Padding.Top, SpikesImage.Width * Zoom, SpikesImage.Height * Zoom));

            if (Cursors is not null)
            {
                foreach (var c in Cursors)
                {
                    if (c.X is null)
                        continue;

                    DrawCursor(drawingContext, c.X.Value);

                    Pen CreateDottedRedLine() => new(Brushes.Red, 1) { DashStyle = new DashStyle(new[] { 5.0, 5.0 }, 0) };

                    if (c == _draggingCursor)
                    {
                        drawingContext.DrawLine(CreateDottedRedLine(), PixelToScreenCoordinates(c.X.Value), _lastDragPosition);
                    }

                    if (c == _draggingCursor || (
                        _draggingCursor == null &&
                        Mouse.LeftButton == MouseButtonState.Released &&
                        Mouse.RightButton == MouseButtonState.Released &&
                        Mouse.MiddleButton == MouseButtonState.Released &&
                        c == CursorToDrag(Mouse.GetPosition(this))))
                    {
                        // Label the cursor.
                        //var label = GetText(c.DisplayName);
                        //drawingContext.DrawText(label, PixelToScreenCoordinates(c.X!.Value) - new Vector(label.Width / 2, CursorStartDragWithin + label.Height));

                        // Draw the dimensions determined by this point.
                        var dimensions = GetDimensionsAffectedByCursor(c.CursorElement).ToList();

                        if (dimensions.Any())
                        {
                            // How far is the top of this View?
                            var yTop = Padding.Top + this.GetVisualTreeParents().OfType<ScrollViewer>().FirstOrDefault(x => x.ComputedVerticalScrollBarVisibility == Visibility.Visible)?.VerticalOffset ?? 0;
                            var toPos = PixelToScreenCoordinates(c.X!.Value);

                            var dottedRed = CreateDottedRedLine();

                            // Draw the center vertical line
                            drawingContext.DrawLine(dottedRed, new Point(toPos.X, yTop), toPos);

                            foreach (var dimen in dimensions)
                            {
                                var fromPos = PixelToScreenCoordinates(dimen.otherEndPosition);
                                // Draw the outside vertical line
                                drawingContext.DrawLine(dottedRed, new Point(fromPos.X, yTop + (fromPos.Y > yTop ? 0 : 20)), fromPos);

                                // Draw a horizontal line from the outside to the center
                                drawingContext.DrawLine(new Pen(Brushes.Red, 1), new Point(fromPos.X, yTop + 10), new Point(toPos.X, yTop + 10));

                                var min = Math.Min(fromPos.X, toPos.X);
                                var max = Math.Max(fromPos.X, toPos.X);

                                if (Math.Abs(fromPos.X - toPos.X) > 50)
                                {
                                    // Draw arrowtips
                                    drawingContext.DrawGeometry(Brushes.Red, null, new PathGeometry(new[] { new PathFigure(new Point(min, yTop + 10), new [] { new LineSegment(new Point(min + 20, yTop), false), new LineSegment(new Point(min + 20, yTop + 20), false) }, true), new PathFigure(new Point(max, yTop + 10), new [] { new LineSegment(new Point(max - 20, yTop), false), new LineSegment(new Point(max - 20, yTop + 20), false) }, true) }));
                                }

                                var opl = Math.Abs(dimen.otherEndPosition - c.X!.Value) / 1250.0;
                                var ri = RefractiveIndexMethod.Current.RefractiveIndex(dimen.dimension, MeasureMode, Wavelength);

                                var line1 = new FormattedText($"OPL = {opl:F3} mm", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, FontFamily.GetTypefaces().First(), FontSize, Brushes.Red, new NumberSubstitution(), TextFormattingMode.Display, 96);
                                var line2 = new FormattedText($"RI = {ri:F3}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, FontFamily.GetTypefaces().First(), FontSize, Brushes.Red, new NumberSubstitution(), TextFormattingMode.Display, 96);
                                var line3 = new FormattedText($"{typeof(Dimension).GetEnumName(dimen.dimension)} = {opl / ri - (dimen.dimension == Dimension.AD ? 0.1 : 0):F3} mm", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, FontFamily.GetTypefaces().First(), FontSize, Brushes.Red, new NumberSubstitution(), TextFormattingMode.Display, 96);

                                drawingContext.DrawText(line1, new Point((min + max - line1.Width) / 2, yTop + 10 + 20));
                                drawingContext.DrawText(line2, new Point((min + max - line2.Width) / 2, yTop + 10 + 20 + line1.Height));
                                drawingContext.DrawText(line3, new Point((min + max - line3.Width) / 2, yTop + 10 + 20 + line1.Height + line2.Height));
                            }
                        }
                    }
                }
            }
        }

        private FormattedText GetText(string? text) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, FontFamily.GetTypefaces().First(), FontSize, Brushes.Red, new NumberSubstitution(), TextFormattingMode.Display, 96);
        private void DrawCursor(DrawingContext drawingContext, int xCoordinate)
        {
            var cursorLocation = PixelToScreenCoordinates(xCoordinate);

            DrawCursor(drawingContext, cursorLocation);
        }
        private void DrawCursor(DrawingContext drawingContext, Point cursorLocation)
        {
            // The names of these vectors are because we originally had the cursors be a "cross" shape instead of an "X" shape.
            // dx and dy are orthogonal.
            var dx = new Vector(CursorLineLength * Math.Sqrt(2) / 2, CursorLineLength * Math.Sqrt(2) / 2);
            var dy = new Vector(dx.X, -dx.Y);

            //if (cursorLocation.X >= 800)
            //{
            //    dx = new Vector(CursorLineLength, 0);
            //    dy = new Vector(0, CursorLineLength);
            //}

            drawingContext.DrawGeometry(null, new Pen(Brushes.Red, 3), new PathGeometry(new[]
            {
                new PathFigure(cursorLocation - dx, new[] { new LineSegment(cursorLocation + dx, true) }, false),
                new PathFigure(cursorLocation - dy, new[] { new LineSegment(cursorLocation + dy, true) }, false)
            }));
        }



        private int? GetCursorPosition(CursorElement whichCursor)
        {
            if (whichCursor == CursorElement.AnteriorCornea)
            {
                return 1000;
            }
            else
            {
                var cursor = Cursors?.FirstOrDefault(x => x.CursorElement == whichCursor);

                if (cursor == null && whichCursor == CursorElement.ILM)
                {
                    return GetCursorPosition(CursorElement.RPE) - 350;
                }

                return cursor?.X;
            }
        }
        private IEnumerable<(int otherEndPosition, Dimension dimension)> GetDimensionsAffectedByCursor(CursorElement cursor)
        {
            return EnumExtensions.GetAllEnumValues<Dimension>()
                .Select(dimension => (dimension, attribute: dimension.GetCustomEnumAttributes<DimensionCursorsAttribute>().FirstOrDefault()))
                .Where(x => x.attribute is not null)
                .Where(x => x.attribute!.Start == cursor || x.attribute.End == cursor)
                .Select(x => (x.dimension, otherEnd: GetCursorPosition(cursor == x.attribute!.Start ? x.attribute.End : x.attribute.Start)))
                .Where(x => x.otherEnd is not null)
                .Select(x => (x.otherEnd!.Value, x.dimension));
        }
        private Point PixelToScreenCoordinates(Point position) => new(Padding.Left + SpikesImage.Width * Zoom * position.X / Spikes.Length, Padding.Top + SpikesImage.Height * Zoom * (1 - position.Y / MaxValue));
        private Point PixelToScreenCoordinates(int xPixel) => PixelToScreenCoordinates(new Point(xPixel, Spikes[xPixel]));
        private int ScreenCoordinatesToPixel(double x) => (int)Math.Round((x - Padding.Left) / (SpikesImage.Width * Zoom) * (Spikes.Length - 1));
        private static T? FindClosestPoint<T>(IEnumerable<T> possiblePoints, Func<T, double> getDistance, double maxDistance)
        {
            return possiblePoints.Select(x => (value: x, distance: getDistance(x)))
                .Where(x => x.distance <= maxDistance)
                .OrderBy(x => x.distance)
                .Select(x => x.value)
                .FirstOrDefault();
        }
        private CursorPosition? CursorToDrag(Point mousePosition)
        {
            if (Cursors is null || SpikesImage is null || Spikes is null)
                return null;

            return FindClosestPoint(Cursors.Where(x => x.X is not null), x => (PixelToScreenCoordinates(x.X!.Value) - mousePosition).LengthSquared, CursorStartDragWithin * CursorStartDragWithin);
        }
        private int PixelToDragTo(Point mousePosition)
        {
            // If we zoom in, we allow the user to drag the cursor farther from the graph and it will still lock on.
            var lockToWithin = DragToPointWithin * Math.Max(Zoom, 1);

            var firstPoint = Math.Max(0, ScreenCoordinatesToPixel(mousePosition.X - lockToWithin));
            var lastPoint = Math.Min(Spikes.Length - 1, ScreenCoordinatesToPixel(mousePosition.X + lockToWithin));

            var closest = FindClosestPoint(Enumerable.Range(firstPoint, lastPoint - firstPoint).Cast<int?>(), x => (PixelToScreenCoordinates(x!.Value) - mousePosition).LengthSquared, lockToWithin * lockToWithin);

            return closest ?? ScreenCoordinatesToPixel(mousePosition.X);
        }


    }
}
