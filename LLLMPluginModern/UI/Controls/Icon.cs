using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace LLLMPluginModern.UI.Controls
{
    /// <summary>
    /// Vector icon drawn from a 24×24 geometry. Stroke icons use the inherited text foreground,
    /// so they automatically match the button or label they sit in.
    /// </summary>
    public sealed class Icon : FrameworkElement
    {
        private const double GridSize = 24.0;

        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            nameof(Data), typeof(Geometry), typeof(Icon),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
            typeof(Icon), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
            nameof(Fill), typeof(Brush), typeof(Icon),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            nameof(StrokeThickness), typeof(double), typeof(Icon),
            new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsFilledProperty = DependencyProperty.Register(
            nameof(IsFilled), typeof(bool), typeof(Icon),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        static Icon()
        {
            WidthProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(16.0));
            HeightProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(16.0));
            SnapsToDevicePixelsProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(true));
            IsHitTestVisibleProperty.OverrideMetadata(typeof(Icon), new UIPropertyMetadata(false));
        }

        public Geometry Data
        {
            get => (Geometry)GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        /// <summary>Optional brush (e.g. a gradient) used instead of the foreground.</summary>
        public Brush Fill
        {
            get => (Brush)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        /// <summary>Stroke width in icon units (24 = full icon size).</summary>
        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public bool IsFilled
        {
            get => (bool)GetValue(IsFilledProperty);
            set => SetValue(IsFilledProperty, value);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var geometry = Data;
            if (geometry == null || ActualWidth <= 0 || ActualHeight <= 0)
                return;

            double scale = Math.Min(ActualWidth, ActualHeight) / GridSize;
            drawingContext.PushTransform(new TranslateTransform((ActualWidth - GridSize * scale) / 2, (ActualHeight - GridSize * scale) / 2));
            drawingContext.PushTransform(new ScaleTransform(scale, scale));

            Brush brush = Fill ?? Foreground;
            if (IsFilled)
            {
                drawingContext.DrawGeometry(brush, null, geometry);
            }
            else
            {
                var pen = new Pen(brush, StrokeThickness)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                drawingContext.DrawGeometry(null, pen, geometry);
            }

            drawingContext.Pop();
            drawingContext.Pop();
        }
    }
}
