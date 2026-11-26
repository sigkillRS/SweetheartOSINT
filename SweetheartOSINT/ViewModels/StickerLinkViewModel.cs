using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using SweetheartOSINT.Models;

namespace SweetheartOSINT.ViewModels
{
    public class StickerLinkViewModel : INotifyPropertyChanged
    {
        private Geometry _geometry;
        private double _playbackOpacity = 0.5;

        public StickerLinkViewModel(StickerLink model, StickerViewModel from, StickerViewModel to)
        {
            Model = model;
            From = from;
            To = to;
            Refresh();
        }

        public StickerLink Model { get; }
        public StickerViewModel From { get; }
        public StickerViewModel To { get; }
        public Geometry Geometry => _geometry;

        // Used only by the "Пуск" playback animation.
        public double PlaybackOpacity
        {
            get => _playbackOpacity;
            set
            {
                if (Math.Abs(_playbackOpacity - value) < 0.0001)
                    return;
                _playbackOpacity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlaybackOpacity)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Refresh()
        {
            Point start = From.Center;
            Point end = To.Center;

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            double bend = Math.Min(46, length * 0.16);

            double normalX = -dy / length;
            double normalY = dx / length;

            var control1 = new Point(
                start.X + dx * 0.35 + normalX * bend,
                start.Y + dy * 0.35 + normalY * bend);
            var control2 = new Point(
                start.X + dx * 0.70 + normalX * bend,
                start.Y + dy * 0.70 + normalY * bend);

            var figure = new PathFigure(start,
                new PathSegment[] { new BezierSegment(control1, control2, end, true) }, false);
            var geometry = new PathGeometry(new[] { figure });
            geometry.Freeze();

            _geometry = geometry;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Geometry)));
        }

        public static Geometry CreatePreview(Point start, Point end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            double bend = Math.Min(46, length * 0.16);
            double normalX = -dy / length;
            double normalY = dx / length;

            var control1 = new Point(start.X + dx * 0.4 + normalX * bend, start.Y + dy * 0.4 + normalY * bend);
            var control2 = new Point(start.X + dx * 0.7 + normalX * bend, start.Y + dy * 0.7 + normalY * bend);
            var figure = new PathFigure(start, new PathSegment[] { new BezierSegment(control1, control2, end, true) }, false);
            return new PathGeometry(new[] { figure });
        }
    }
}
