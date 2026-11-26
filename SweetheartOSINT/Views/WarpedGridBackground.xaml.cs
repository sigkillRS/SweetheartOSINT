using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SweetheartOSINT.Views
{
    /// <summary>
    /// Animated black background with a slowly drifting mesh of warped Bezier lines.
    /// Purely decorative: never participates in hit testing.
    /// </summary>
    public partial class WarpedGridBackground : UserControl
    {
        private const double Spacing = 54;
        private const double Overscan = 100;
        private const double SegmentLength = 24;
        private const double WaveAmplitude = 6.5;

        public WarpedGridBackground()
        {
            InitializeComponent();

            SizeChanged += (_, __) => Rebuild();
            Loaded += (_, __) =>
            {
                Rebuild();
                BeginStoryboard((Storyboard)Resources["DriftStoryboard"]);
                BeginStoryboard((Storyboard)Resources["PulseStoryboard"]);
            };
        }

        private void Rebuild()
        {
            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0)
                return;

            GridCanvas.Children.Clear();

            var stroke = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A));
            stroke.Freeze();

            for (double x = -Overscan; x <= width + Overscan; x += Spacing)
                GridCanvas.Children.Add(CreateLine(stroke, x, true, -Overscan, height + Overscan, width, height));

            for (double y = -Overscan; y <= height + Overscan; y += Spacing)
                GridCanvas.Children.Add(CreateLine(stroke, y, false, -Overscan, width + Overscan, width, height));
        }

        private static Path CreateLine(Brush stroke, double fixedValue, bool vertical,
                                       double from, double to, double width, double height)
        {
            int count = Math.Max(2, (int)Math.Ceiling((to - from) / SegmentLength) + 1);
            var points = new Point[count];

            for (int i = 0; i < count; i++)
            {
                double along = from + (to - from) * i / (count - 1.0);
                double baseX = vertical ? fixedValue : along;
                double baseY = vertical ? along : fixedValue;

                double dx = baseX - width / 2.0;
                double dy = baseY - height / 2.0;
                double distance = Math.Sqrt(dx * dx + dy * dy) + 1.0;
                double ripple = 12.0 * Math.Exp(-distance / 320.0) * Math.Sin(distance * 0.011);

                double shiftX =
                    Math.Sin(baseY * 0.011 + baseX * 0.004) * WaveAmplitude +
                    Math.Sin(baseY * 0.021 - baseX * 0.008 + 1.4) * 2.2 +
                    ripple;

                double shiftY =
                    Math.Sin(baseX * 0.010 - baseY * 0.005 + 0.7) * WaveAmplitude +
                    Math.Sin(baseX * 0.018 + baseY * 0.007) * 2.0 +
                    ripple;

                points[i] = new Point(baseX + shiftX, baseY + shiftY);
            }

            // Catmull-Rom through sampled points, converted to cubic Bezier segments.
            var controlPoints = new PointCollection();
            for (int i = 0; i < points.Length - 1; i++)
            {
                Point p0 = points[Math.Max(0, i - 1)];
                Point p1 = points[i];
                Point p2 = points[i + 1];
                Point p3 = points[Math.Min(points.Length - 1, i + 2)];

                controlPoints.Add(new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0));
                controlPoints.Add(new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0));
                controlPoints.Add(p2);
            }

            var figure = new PathFigure(points[0],
                new PathSegment[] { new PolyBezierSegment(controlPoints, true) }, false);
            var geometry = new PathGeometry(new[] { figure });
            geometry.Freeze();

            return new Path
            {
                Data = geometry,
                Stroke = stroke,
                StrokeThickness = 1,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };
        }
    }
}
