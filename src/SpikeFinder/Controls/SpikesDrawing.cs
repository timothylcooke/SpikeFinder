using ReactiveUI;
using SpikeFinder.Extensions;
using SpikeFinder.ViewModels;
using Syncfusion.Data.Extensions;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SpikeFinder.Controls
{
    class SpikesDrawing : Control
    {
        public SpikesDrawing()
        {
            Focusable = false;
            FocusVisualStyle = null;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (constraint.Width is 0 || constraint.Height is 0)
                return constraint;

            var zoom = Math.Min(constraint.Width / LoadSpikesViewModel.ImageWidth, constraint.Height / LoadSpikesViewModel.ImageHeight);

            return new Size(zoom * LoadSpikesViewModel.ImageWidth, zoom * LoadSpikesViewModel.ImageHeight);
        }

        public Geometry[][] Geometries
        {
            get => (Geometry[][])GetValue(GeometriesProperty);
            set => SetValue(GeometriesProperty, value);
        }
        public static readonly DependencyProperty GeometriesProperty = DependencyProperty.Register(nameof(Geometries), typeof(Geometry[][]), typeof(SpikesDrawing), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        private readonly Brush[] _brushes = {
            Brushes.Red,
            Brushes.Blue,
            Brushes.Green,
            Brushes.Violet,
            Brushes.YellowGreen,
            Brushes.Turquoise,
        };

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (Geometries is null)
                return;

            drawingContext.PushClip(new RectangleGeometry(new Rect(RenderSize)));
            drawingContext.PushTransform(new ScaleTransform(RenderSize.Width / LoadSpikesViewModel.ImageWidth, RenderSize.Height / LoadSpikesViewModel.ImageHeight));

            Geometries.Select((geometries, i) => (pen: new Pen(_brushes[i % _brushes.Length], 0.75), geometries)).ForEach(x => x.geometries.ForEach(y => drawingContext.DrawGeometry(null, x.pen, y)));
        }
    }
}
