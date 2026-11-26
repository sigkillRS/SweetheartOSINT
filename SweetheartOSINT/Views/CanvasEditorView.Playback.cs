using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SweetheartOSINT.ViewModels;

namespace SweetheartOSINT.Views
{
    public partial class CanvasEditorView
    {
        private CancellationTokenSource _playbackCts;
        private CanvasEditorViewModel.PlaybackState _playbackState;
        private bool _isPlaying;

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null)
                return;

            if (_isPlaying)
            {
                _playbackCts?.Cancel();
                return;
            }

            if (_viewModel.Stickers.Count == 0)
            {
                _viewModel.Status = "Сначала добавьте стикеры на карту";
                return;
            }

            _ = PlayAsync();
        }

        internal void RestorePlaybackStateNow()
        {
            if (!_isPlaying)
                return;

            _playbackCts?.Cancel();
            FinishPlayback();
        }

        private async Task PlayAsync()
        {
            _isPlaying = true;
            _playbackCts = new CancellationTokenSource();
            var token = _playbackCts.Token;
            _playbackState = _viewModel.CapturePlaybackState();

            PlayButton.Content = "■ Стоп";
            PlayOverlay.Children.Clear();
            PanHint.Visibility = Visibility.Collapsed;
            World.IsHitTestVisible = false;
            PlaybackBanner.Visibility = Visibility.Visible;
            _viewModel.Status = "Воспроизведение · подготовка";

            try
            {
                var playwright = _viewModel.CreatePlaybackStickers(_playbackState);
                _viewModel.BeginPlayback();

                await AnimateAsync(320, t => PlaybackBanner.Opacity = t, token);
                await Task.Delay(280, token);

                bool parallel = playwright.Count > 5;
                if (parallel)
                {
                    for (int i = 0; i < playwright.Count; i += 3)
                    {
                        var wave = playwright.Skip(i).Take(3).ToList();
                        PlaybackBannerText.Text = "ВОСПРОИЗВЕДЕНИЕ · ГРУППА " + (i / 3 + 1);
                        await PlayWaveAsync(wave, token);
                    }
                }
                else
                {
                    PlaybackBannerText.Text = "ВОСПРОИЗВЕДЕНИЕ · ПУСК";
                    for (int i = 0; i < playwright.Count; i++)
                        await PlayWaveAsync(new List<StickerViewModel> { playwright[i] }, token);
                }

                await RevealLinksAsync(token);
                await AnimateViewportAsync(_playbackState.Zoom, _playbackState.OffsetX,
                    _playbackState.OffsetY, 700, token);

                PlaybackBannerText.Text = "ГОТОВО";
                _viewModel.Status = "Воспроизведение завершено";
                await Task.Delay(900, token);
            }
            catch (OperationCanceledException)
            {
                _viewModel.Status = "Воспроизведение остановлено";
            }
            catch (Exception ex)
            {
                _viewModel.Status = "Ошибка воспроизведения: " + ex.Message;
            }
            finally
            {
                FinishPlayback();
            }
        }

        private void FinishPlayback()
        {
            var state = _playbackState;
            _playbackCts?.Cancel();

            PlayOverlay.Children.Clear();
            PlaybackBanner.Visibility = Visibility.Collapsed;
            PlaybackBanner.Opacity = 0;
            PlaybackBannerText.Text = "ВОСПРОИЗВЕДЕНИЕ · ПУСК";
            PanHint.Visibility = Visibility.Visible;
            World.IsHitTestVisible = true;
            PlayButton.Content = "▶ Пуск";

            if (state != null && _viewModel != null)
                _viewModel.EndPlayback(state);

            _playbackState = null;
            // Do not dispose here: an async delay may still hold this token.
            _playbackCts = null;
            _isPlaying = false;
        }

        // ---------- Scenes ----------

        private async Task PlayWaveAsync(IReadOnlyList<StickerViewModel> stickers, CancellationToken token)
        {
            if (stickers.Count == 0)
                return;

            var first = stickers[0];
            double initialZoom = 0.42;
            SetCameraOn(first, initialZoom);

            var camera = AnimateCameraToAsync(stickers, 760, token);
            var reveals = stickers.Select((sticker, index) =>
                RevealStickerAsync(sticker, token, index * 90)).ToList();

            await Task.WhenAll(new[] { camera }.Concat(reveals));
            await Task.Delay(180, token);
        }

        private async Task RevealStickerAsync(StickerViewModel sticker, CancellationToken token, int delay)
        {
            await Task.Delay(delay, token);

            await Dispatcher.InvokeAsync(() =>
            {
                sticker.PlaybackOpacity = 0;
                sticker.PlaybackScale = 0.72;
                _viewModel.AddPlaybackSticker(sticker);
            });

            SystemSounds.Beep.Play();

            await AnimateAsync(420, t =>
            {
                sticker.PlaybackOpacity = Math.Min(1, t * 1.6);
                sticker.PlaybackScale = 0.72 + 0.28 * EaseOutBack(t);
            }, token);

            // Text and image work together, but only after the sticker itself has appeared.
            var text = TypeSceneAsync(sticker, token);
            var image = sticker.HasImage || !string.IsNullOrEmpty(_viewModel.GetPlaybackImageFile(_playbackState, sticker.Id))
                ? ImageSceneAsync(sticker, token)
                : Task.CompletedTask;

            await Task.WhenAll(text, image);
        }

        private async Task TypeSceneAsync(StickerViewModel sticker, CancellationToken token)
        {
            var cursor = CreateCursor("TEXT", Brushes.White, new SolidColorBrush(Color.FromRgb(230, 230, 230)));
            var caret = new Border
            {
                Width = 1.6,
                Height = 15,
                Background = Brushes.White,
                IsHitTestVisible = false,
                Opacity = 1
            };
            PlayOverlay.Children.Add(caret);

            string fullText = sticker.Text ?? string.Empty;

            await Dispatcher.InvokeAsync(() => sticker.Text = string.Empty);
            await ShowCursorAsync(cursor, new Point(sticker.X + 24, sticker.Y + 34), token);

            if (!string.IsNullOrEmpty(fullText))
            {
                for (int i = 1; i <= fullText.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    int count = i;
                    await Dispatcher.InvokeAsync(() => sticker.Text = fullText.Substring(0, count));

                    string typed = fullText.Substring(0, count);
                    var caretWorld = new Point(
                        sticker.X + 22 + Math.Min(typed.Length % 26, 26) * 5.6,
                        sticker.Y + 36 + typed.Count(c => c == '\n') * 15);
                    PositionCursor(cursor, caretWorld);
                    PositionCursor(caret, WorldToScreen(caretWorld));

                    // Blinking caret makes the typing visible even for short texts.
                    caret.Opacity = i % 2 == 0 ? 1 : 0.25;

                    int pause = new[] { 70, 85, 105 }[i % 3];
                    if (typed[i - 1] == ' ' || typed[i - 1] == '_')
                        pause += 60;
                    await Task.Delay(pause, token);
                    if (i % 10 == 0)
                        SystemSounds.Asterisk.Play();
                }
            }
            else
            {
                await Task.Delay(360, token);
            }

            await AnimateAsync(160, t => caret.Opacity = 1 - t, token);
            await Dispatcher.InvokeAsync(() => PlayOverlay.Children.Remove(caret));
            await HideCursorAsync(cursor, token);
        }

        private async Task ImageSceneAsync(StickerViewModel sticker, CancellationToken token)
        {
            var cursor = CreateCursor("IMAGE", new SolidColorBrush(Color.FromRgb(120, 220, 255)),
                new SolidColorBrush(Color.FromRgb(20, 60, 80)));

            await ShowCursorAsync(cursor, new Point(sticker.X + sticker.Width - 40, sticker.Y + 70), token);
            await Task.Delay(220, token);

            var imageTarget = new Point(sticker.X + sticker.Width / 2, sticker.Y + 116);
            await MoveCursorToWorldAsync(cursor, imageTarget, 420, token);

            string imageFile = _viewModel.GetPlaybackImageFile(_playbackState, sticker.Id);
            if (!string.IsNullOrEmpty(imageFile))
            {
                await Dispatcher.InvokeAsync(() => sticker.SetImage(imageFile));
                SystemSounds.Exclamation.Play();
            }

            ScrollViewer scroll = null;
            for (int attempt = 0; attempt < 8 && scroll == null; attempt++)
            {
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                await Dispatcher.InvokeAsync(() =>
                {
                    var container = StickersItemsControl.ItemContainerGenerator.ContainerFromItem(sticker) as DependencyObject;
                    scroll = FindScrollViewer(container);
                });
                if (scroll == null)
                    await Task.Delay(40, token);
            }

            if (scroll != null)
            {
                await AnimateAsync(900, t =>
                {
                    double eased = EaseInOutCubic(t);
                    double maxV = Math.Max(0, scroll.ExtentHeight - scroll.ViewportHeight);
                    double maxH = Math.Max(0, scroll.ExtentWidth - scroll.ViewportWidth);
                    scroll.ScrollToVerticalOffset(maxV * eased);
                    scroll.ScrollToHorizontalOffset(maxH * eased * 0.65);
                }, token);
            }

            await HideCursorAsync(cursor, token);
        }

        private async Task RevealLinksAsync(CancellationToken token)
        {
            var links = _playbackState.Links;
            if (links.Count == 0)
                return;

            PlaybackBannerText.Text = "ВОСПРОИЗВЕДЕНИЕ · СВЯЗИ";
            var created = new List<StickerLinkViewModel>();

            await Dispatcher.InvokeAsync(() =>
            {
                foreach (var model in links)
                {
                    var link = _viewModel.AddPlaybackLink(model);
                    if (link != null)
                        created.Add(link);
                }
            });

            SystemSounds.Asterisk.Play();
            await AnimateAsync(700, t =>
            {
                foreach (var link in created)
                    link.PlaybackOpacity = 0.5 * EaseOutCubic(t);
            }, token);
        }

        // ---------- Camera ----------

        private void SetCameraOn(StickerViewModel sticker, double zoom)
        {
            var center = new Point(sticker.X + sticker.Width / 2.0, sticker.Y + sticker.Height / 2.0);
            _viewModel.SetViewport(
                zoom,
                Viewport.ActualWidth / 2 - center.X * zoom,
                Viewport.ActualHeight / 2 - center.Y * zoom);
        }

        private async Task AnimateCameraToAsync(IReadOnlyList<StickerViewModel> stickers, int duration, CancellationToken token)
        {
            double minX = stickers.Min(s => s.X);
            double minY = stickers.Min(s => s.Y);
            double maxX = stickers.Max(s => s.X + s.Width);
            double maxY = stickers.Max(s => s.Y + s.Height);

            double width = Math.Max(1, maxX - minX);
            double height = Math.Max(1, maxY - minY);
            double zoom = Math.Min(1.05,
                Math.Min((Viewport.ActualWidth - 220) / width, (Viewport.ActualHeight - 200) / height));
            zoom = Math.Max(0.42, zoom);

            double targetX = Viewport.ActualWidth / 2 - ((minX + maxX) / 2) * zoom;
            double targetY = Viewport.ActualHeight / 2 - ((minY + maxY) / 2) * zoom;
            await AnimateViewportAsync(zoom, targetX, targetY, duration, token);
        }

        private async Task AnimateViewportAsync(double zoom, double offsetX, double offsetY, int duration, CancellationToken token)
        {
            double startZoom = _viewModel.Zoom;
            double startX = _viewModel.OffsetX;
            double startY = _viewModel.OffsetY;

            await AnimateAsync(duration, t =>
            {
                double eased = EaseInOutCubic(t);
                _viewModel.SetViewport(
                    startZoom + (zoom - startZoom) * eased,
                    startX + (offsetX - startX) * eased,
                    startY + (offsetY - startY) * eased);
            }, token);
        }

        // ---------- Cursors ----------

        private UIElement CreateCursor(string label, Brush fill, Brush labelBackground)
        {
            var grid = new Grid { IsHitTestVisible = false, Opacity = 0 };

            var arrow = new Path
            {
                Data = Geometry.Parse("M0,0 L0,17 L4.2,13 L7.2,20 L10.2,18.6 L7.2,12 L13,12 Z"),
                Fill = fill,
                Stroke = Brushes.Black,
                StrokeThickness = 0.9,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            grid.Children.Add(arrow);

            var captionBorder = new Border
            {
                Background = labelBackground,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0.6),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 2, 5, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(11, 17, 0, 0)
            };
            captionBorder.Child = new TextBlock
            {
                Text = label,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black
            };
            grid.Children.Add(captionBorder);

            Canvas.SetLeft(grid, -200);
            Canvas.SetTop(grid, -200);
            PlayOverlay.Children.Add(grid);
            return grid;
        }

        private async Task ShowCursorAsync(UIElement cursor, Point world, CancellationToken token)
        {
            PositionCursor(cursor, WorldToScreen(world));
            await AnimateAsync(180, t => cursor.Opacity = t, token);
        }

        private async Task HideCursorAsync(UIElement cursor, CancellationToken token)
        {
            await AnimateAsync(180, t => cursor.Opacity = 1 - t, token);
            await Dispatcher.InvokeAsync(() => PlayOverlay.Children.Remove(cursor));
        }

        private async Task MoveCursorToWorldAsync(UIElement cursor, Point world, int duration, CancellationToken token)
        {
            double startX = Canvas.GetLeft(cursor);
            double startY = Canvas.GetTop(cursor);
            if (double.IsNaN(startX) || double.IsNaN(startY))
            {
                startX = Viewport.ActualWidth / 2;
                startY = Viewport.ActualHeight / 2;
            }

            await AnimateAsync(duration, t =>
            {
                var target = WorldToScreen(world);
                double eased = EaseOutCubic(t);
                Canvas.SetLeft(cursor, startX + (target.X - startX) * eased);
                Canvas.SetTop(cursor, startY + (target.Y - startY) * eased);
            }, token);
        }

        private void PositionCursor(UIElement cursor, Point screen)
        {
            Canvas.SetLeft(cursor, screen.X);
            Canvas.SetTop(cursor, screen.Y);
        }

        private Point WorldToScreen(Point world)
        {
            return new Point(
                world.X * _viewModel.Zoom + _viewModel.OffsetX,
                world.Y * _viewModel.Zoom + _viewModel.OffsetY);
        }

        // ---------- Helpers ----------

        private async Task AnimateAsync(int durationMs, Action<double> apply, CancellationToken token)
        {
            if (durationMs <= 0)
            {
                apply(1);
                return;
            }

            var watch = Stopwatch.StartNew();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                double t = Math.Min(1, watch.ElapsedMilliseconds / (double)durationMs);
                await Dispatcher.InvokeAsync(() => apply(t));
                if (t >= 1)
                    break;
                await Task.Delay(16, token);
            }
        }

        private static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);

        private static double EaseInOutCubic(double t) =>
            t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;

        private static double EaseOutBack(double t)
        {
            const double c1 = 1.70158;
            const double c3 = c1 + 1;
            return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
        }

        private static ScrollViewer FindScrollViewer(DependencyObject root)
        {
            if (root == null)
                return null;

            if (root is ScrollViewer viewer &&
                viewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto &&
                viewer.VerticalScrollBarVisibility == ScrollBarVisibility.Auto)
                return viewer;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
