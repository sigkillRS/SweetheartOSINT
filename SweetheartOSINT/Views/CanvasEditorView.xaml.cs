using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SweetheartOSINT.Models;
using SweetheartOSINT.ViewModels;

namespace SweetheartOSINT.Views
{
    public partial class CanvasEditorView : UserControl
    {
        private CanvasEditorViewModel _viewModel;
        private bool _spaceHeld;
        private bool _isPanning;
        private Point _panStart;
        private double _panOffsetX;
        private double _panOffsetY;
        private StickerViewModel _dragSticker;
        private Point _dragStart;
        private double _dragX;
        private double _dragY;
        private StickerViewModel _resizeSticker;
        private string _resizeMode;
        private Point _resizeStart;
        private double _resizeX;
        private double _resizeY;
        private double _resizeWidth;
        private double _resizeHeight;
        private StickerViewModel _connectFrom;
        private Point _connectStartScreen;
        private Point _connectStartWorld;
        private bool _connectMoved;
        private Point _lastRightClick;
        private bool _inputLanguageRu = true;

        public CanvasEditorView()
        {
            InitializeComponent();
        }

        public event Action ImagesChanged;

        public void Load(ProjectData project)
        {
            _viewModel = new CanvasEditorViewModel(project);
            DataContext = _viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.ImagesChanged += () => ImagesChanged?.Invoke();
            UpdateTransform();
        }

        public void ApplyToProject()
        {
            RestorePlaybackStateNow();
            _viewModel?.ApplyToProject();
        }

        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            _inputLanguageRu = !_inputLanguageRu;
            LanguageButton.Content = _inputLanguageRu ? "RU" : "EN";

            try
            {
                var culture = new CultureInfo(_inputLanguageRu ? "ru-RU" : "en-US");
                var target = Keyboard.FocusedElement as DependencyObject ?? Viewport;
                InputLanguageManager.SetInputLanguage(target, culture);
                InputLanguageManager.SetInputLanguage(this, culture);
                if (_viewModel != null)
                    _viewModel.Status = "Язык ввода: " + (_inputLanguageRu ? "русский" : "английский");
            }
            catch
            {
                if (_viewModel != null)
                    _viewModel.Status = "Язык ввода недоступен в системе";
            }
        }

        // ---------- Viewport transform ----------

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CanvasEditorViewModel.Zoom) ||
                e.PropertyName == nameof(CanvasEditorViewModel.OffsetX) ||
                e.PropertyName == nameof(CanvasEditorViewModel.OffsetY))
                UpdateTransform();
        }

        private void UpdateTransform()
        {
            if (_viewModel == null)
                return;

            double zoom = _viewModel.Zoom;
            WorldTransform.Matrix = new Matrix(zoom, 0, 0, zoom, _viewModel.OffsetX, _viewModel.OffsetY);
        }

        private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            World.Width = e.NewSize.Width;
            World.Height = e.NewSize.Height;
        }

        private Point ScreenToWorld(Point screen)
        {
            return new Point(
                (screen.X - _viewModel.OffsetX) / _viewModel.Zoom,
                (screen.Y - _viewModel.OffsetY) / _viewModel.Zoom);
        }

        // ---------- Sticker creation ----------

        private void CreateStickerFromMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying || _viewModel == null)
                return;

            Point world = ScreenToWorld(_lastRightClick);
            _viewModel.AddSticker(world.X - 115, world.Y - 95);
        }

        private void DeleteLink_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying || _viewModel == null || (sender as FrameworkElement)?.DataContext is not StickerLinkViewModel link)
                return;
            _viewModel.RemoveLink(link);
        }

        private void DeleteSticker_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying || _viewModel == null || (sender as FrameworkElement)?.DataContext is not StickerViewModel sticker)
                return;

            _viewModel.Select(sticker);
            _viewModel.DeleteSelected();
        }

        // ---------- Sticker dragging ----------

        private void Sticker_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isPlaying || _viewModel == null || e.ChangedButton != MouseButton.Left)
                return;
            if ((sender as FrameworkElement)?.DataContext is not StickerViewModel sticker)
                return;

            _viewModel.Select(sticker);

            if (e.OriginalSource is TextBox || e.OriginalSource is Button)
                return;

            _dragSticker = sticker;
            _dragStart = e.GetPosition(Viewport);
            _dragX = sticker.X;
            _dragY = sticker.Y;
            Viewport.CaptureMouse();
            e.Handled = true;
        }

        // ---------- Resizing ----------

        private void Resize_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isPlaying || _viewModel == null || e.ChangedButton != MouseButton.Left)
                return;
            if ((sender as FrameworkElement)?.DataContext is not StickerViewModel sticker)
                return;

            _resizeSticker = sticker;
            _resizeMode = (sender as FrameworkElement)?.Tag as string ?? "SE";
            _resizeStart = e.GetPosition(Viewport);
            _resizeX = sticker.X;
            _resizeY = sticker.Y;
            _resizeWidth = sticker.Width;
            _resizeHeight = sticker.Height;
            _viewModel.Select(sticker);
            Viewport.CaptureMouse();
            e.Handled = true;
        }

        // ---------- Connections ----------

        private void Connect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isPlaying || _viewModel == null || e.ChangedButton != MouseButton.Left)
                return;
            if ((sender as FrameworkElement)?.DataContext is not StickerViewModel sticker)
                return;

            _connectFrom = sticker;
            _connectStartScreen = e.GetPosition(Viewport);
            _connectStartWorld = sticker.Center;
            _connectMoved = false;
            TempLink.Data = StickerLinkViewModel.CreatePreview(_connectStartWorld, _connectStartWorld);
            TempLink.Visibility = Visibility.Visible;
            _viewModel.Select(sticker);
            Viewport.CaptureMouse();
            e.Handled = true;
        }

        private StickerViewModel FindStickerAt(Point screenPoint)
        {
            if (_viewModel == null)
                return null;

            // Geometry-based hit test: reliable regardless of visual tree,
            // templates, zoom or pan. Iterate in reverse so the topmost sticker wins.
            Point world = ScreenToWorld(screenPoint);
            for (int i = _viewModel.Stickers.Count - 1; i >= 0; i--)
            {
                var sticker = _viewModel.Stickers[i];
                if (world.X >= sticker.X && world.X <= sticker.X + sticker.Width &&
                    world.Y >= sticker.Y && world.Y <= sticker.Y + sticker.Height)
                    return sticker;
            }

            return null;
        }

        // ---------- Viewport mouse ----------

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isPlaying || _viewModel == null)
                return;

            if (e.ChangedButton == MouseButton.Right)
            {
                _lastRightClick = e.GetPosition(Viewport);
                return;
            }

            if (e.ChangedButton == MouseButton.Middle ||
                (_spaceHeld && e.ChangedButton == MouseButton.Left))
            {
                _isPanning = true;
                _panStart = e.GetPosition(Viewport);
                _panOffsetX = _viewModel.OffsetX;
                _panOffsetY = _viewModel.OffsetY;
                Viewport.CaptureMouse();
                Viewport.Cursor = Cursors.Hand;
                e.Handled = true;
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                if (FindStickerAt(e.GetPosition(Viewport)) == null)
                {
                    _viewModel.Select(null);
                    Viewport.Focus();
                }
            }
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPlaying || _viewModel == null)
                return;

            Point screen = e.GetPosition(Viewport);

            if (_isPanning)
            {
                _viewModel.SetViewport(
                    _viewModel.Zoom,
                    _panOffsetX + screen.X - _panStart.X,
                    _panOffsetY + screen.Y - _panStart.Y);
                return;
            }

            if (_dragSticker != null)
            {
                _dragSticker.X = _dragX + (screen.X - _dragStart.X) / _viewModel.Zoom;
                _dragSticker.Y = _dragY + (screen.Y - _dragStart.Y) / _viewModel.Zoom;
                return;
            }

            if (_resizeSticker != null)
            {
                double dx = (screen.X - _resizeStart.X) / _viewModel.Zoom;
                double dy = (screen.Y - _resizeStart.Y) / _viewModel.Zoom;
                double x = _resizeX;
                double y = _resizeY;
                double width = _resizeWidth;
                double height = _resizeHeight;

                if (_resizeMode.Contains("E"))
                    width = Math.Max(160, _resizeWidth + dx);
                if (_resizeMode.Contains("S"))
                    height = Math.Max(120, _resizeHeight + dy);
                if (_resizeMode.Contains("W"))
                {
                    width = Math.Max(160, _resizeWidth - dx);
                    x = _resizeX + (_resizeWidth - width);
                }
                if (_resizeMode.Contains("N"))
                {
                    height = Math.Max(120, _resizeHeight - dy);
                    y = _resizeY + (_resizeHeight - height);
                }

                _resizeSticker.X = x;
                _resizeSticker.Y = y;
                _resizeSticker.Width = width;
                _resizeSticker.Height = height;
                return;
            }

            if (_connectFrom != null)
            {
                if (Math.Abs(screen.X - _connectStartScreen.X) + Math.Abs(screen.Y - _connectStartScreen.Y) > 12)
                    _connectMoved = true;
                TempLink.Data = StickerLinkViewModel.CreatePreview(_connectStartWorld, ScreenToWorld(screen));
            }
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPlaying || _viewModel == null)
                return;

            Point screen = e.GetPosition(Viewport);

            if (_isPanning)
            {
                _isPanning = false;
                Viewport.Cursor = _spaceHeld ? Cursors.Hand : null;
            }

            if (_dragSticker != null)
            {
                _viewModel.Status = "Стикер перемещён";
                _dragSticker = null;
            }

            if (_resizeSticker != null)
            {
                _viewModel.Status = "Размер изменён";
                _resizeSticker = null;
                _resizeMode = null;
            }

            if (_connectFrom != null)
            {
                if (!_connectMoved)
                {
                    _connectFrom.AddSource();
                    _viewModel.Status = "Источник добавлен — введите ссылку";
                }
                else
                {
                    var target = FindStickerAt(screen);
                    if (target != null && target != _connectFrom)
                        _viewModel.AddLink(_connectFrom.Id, target.Id);
                    else
                        _viewModel.Status = "Отпустите на другом стикере";
                }

                TempLink.Visibility = Visibility.Collapsed;
                TempLink.Data = null;
                _connectFrom = null;
            }

            if (Viewport.IsMouseCaptured)
                Viewport.ReleaseMouseCapture();
        }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_isPlaying || _viewModel == null || e.Delta == 0)
                return;

            Point screen = e.GetPosition(Viewport);
            double oldZoom = _viewModel.Zoom;

            _viewModel.Zoom = oldZoom * (e.Delta > 0 ? 1.1 : 1 / 1.1);
            double zoom = _viewModel.Zoom;

            double worldX = (screen.X - _viewModel.OffsetX) / oldZoom;
            double worldY = (screen.Y - _viewModel.OffsetY) / oldZoom;
            _viewModel.SetViewport(zoom, screen.X - worldX * zoom, screen.Y - worldY * zoom);
            e.Handled = true;
        }

        // ---------- Keyboard ----------

        private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_isPlaying)
            {
                if (e.Key == Key.Escape)
                    _playbackCts?.Cancel();
                e.Handled = true;
                return;
            }

            bool editingText = Keyboard.FocusedElement is TextBox;

            if (e.Key == Key.Space && !editingText)
            {
                _spaceHeld = true;
                if (!_isPanning)
                    Viewport.Cursor = Cursors.Hand;
                e.Handled = true;
            }
            else if (e.Key == Key.Delete && !editingText)
            {
                _viewModel?.DeleteSelected();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _connectFrom != null)
            {
                _connectFrom = null;
                TempLink.Visibility = Visibility.Collapsed;
                TempLink.Data = null;
                if (Viewport.IsMouseCaptured)
                    Viewport.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void Root_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space)
                return;
            _spaceHeld = false;
            if (!_isPanning)
                Viewport.Cursor = null;
        }

        // ---------- Images ----------

        private void AddImage_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying || _viewModel == null || (sender as FrameworkElement)?.DataContext is not StickerViewModel sticker)
                return;

            var dialog = new OpenFileDialog
            {
                Title = "Выберите изображение",
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|Все файлы|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
                _viewModel.AddImage(sticker, dialog.FileName);
        }

        private void Sticker_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void Sticker_Drop(object sender, DragEventArgs e)
        {
            if (_isPlaying || _viewModel == null || (sender as FrameworkElement)?.DataContext is not StickerViewModel sticker)
                return;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            var image = files?.FirstOrDefault(file =>
            {
                var extension = Path.GetExtension(file);
                return new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" }
                    .Contains(extension, StringComparer.OrdinalIgnoreCase);
            });

            if (image != null)
                _viewModel.AddImage(sticker, image);
        }

        // ---------- Sources ----------

        private void OpenSource_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying || (sender as FrameworkElement)?.DataContext is not StickerSourceViewModel source)
                return;
            if (string.IsNullOrWhiteSpace(source.Url))
                return;

            var target = source.Url.Trim();
            if (!target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                target = "https://" + target;

            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть ссылку: " + ex.Message, "Sweetheart OSINT",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
