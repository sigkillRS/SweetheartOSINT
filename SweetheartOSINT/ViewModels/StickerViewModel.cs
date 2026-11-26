using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using SweetheartOSINT.Models;

namespace SweetheartOSINT.ViewModels
{
    public class StickerViewModel : INotifyPropertyChanged
    {
        private readonly Sticker _model;
        private readonly Func<string, string> _resolveImage;
        private bool _isSelected;
        private double _playbackScale = 1;
        private double _playbackOpacity = 1;

        public StickerViewModel(Sticker model, Func<string, string> resolveImage)
        {
            _model = model ?? new Sticker();
            _resolveImage = resolveImage;

            foreach (var source in _model.Sources ?? new List<StickerSource>())
                AddSourceViewModel(source);

            AddSourceCommand = new RelayCommand(_ => AddSource());
            RemoveImageCommand = new RelayCommand(_ => RemoveImage());
            RotateImageCommand = new RelayCommand(_ => RotateImage());
        }

        public Sticker Model => _model;
        public string Id => _model.Id;
        public ObservableCollection<StickerSourceViewModel> Sources { get; } = new ObservableCollection<StickerSourceViewModel>();
        public ICommand AddSourceCommand { get; }
        public ICommand RemoveImageCommand { get; }
        public ICommand RotateImageCommand { get; }

        public event Action<StickerViewModel> GeometryChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public double X
        {
            get => _model.X;
            set
            {
                if (Math.Abs(_model.X - value) < 0.01)
                    return;
                _model.X = value;
                OnPropertyChanged();
                GeometryChanged?.Invoke(this);
            }
        }

        public double Y
        {
            get => _model.Y;
            set
            {
                if (Math.Abs(_model.Y - value) < 0.01)
                    return;
                _model.Y = value;
                OnPropertyChanged();
                GeometryChanged?.Invoke(this);
            }
        }

        public double Width
        {
            get => _model.Width;
            set
            {
                var next = Math.Max(160, value);
                if (Math.Abs(_model.Width - next) < 0.01)
                    return;
                _model.Width = next;
                OnPropertyChanged();
                GeometryChanged?.Invoke(this);
            }
        }

        public double Height
        {
            get => _model.Height;
            set
            {
                var next = Math.Max(120, value);
                if (Math.Abs(_model.Height - next) < 0.01)
                    return;
                _model.Height = next;
                OnPropertyChanged();
                GeometryChanged?.Invoke(this);
            }
        }

        public string Text
        {
            get => _model.Text;
            set
            {
                _model.Text = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        // Used only by the "Пуск" playback animation.
        public double PlaybackScale
        {
            get => _playbackScale;
            set
            {
                if (Math.Abs(_playbackScale - value) < 0.0001)
                    return;
                _playbackScale = value;
                OnPropertyChanged();
            }
        }

        public double PlaybackOpacity
        {
            get => _playbackOpacity;
            set
            {
                if (Math.Abs(_playbackOpacity - value) < 0.0001)
                    return;
                _playbackOpacity = value;
                OnPropertyChanged();
            }
        }

        public double ImageRotation
        {
            get => _model.ImageRotation;
            set
            {
                if (Math.Abs(_model.ImageRotation - value) < 0.01)
                    return;
                _model.ImageRotation = value;
                OnPropertyChanged();
            }
        }

        public bool HasImage => !string.IsNullOrEmpty(_model.ImageFile);
        public string ImagePath => HasImage ? _resolveImage?.Invoke(_model.ImageFile) : null;

        public Point Center => new Point(_model.X + _model.Width / 2.0, _model.Y + _model.Height / 2.0);

        public void SetImage(string fileName)
        {
            _model.ImageFile = fileName;
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(ImagePath));
        }

        public void RemoveImage()
        {
            _model.ImageFile = null;
            _model.ImageRotation = 0;
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(ImagePath));
            OnPropertyChanged(nameof(ImageRotation));
        }

        public void RotateImage()
        {
            if (!HasImage)
                return;
            ImageRotation = (ImageRotation + 90) % 360;
        }

        public void AddSource()
        {
            var source = new StickerSource();
            _model.Sources ??= new List<StickerSource>();
            _model.Sources.Add(source);
            AddSourceViewModel(source);
        }

        private void AddSourceViewModel(StickerSource source)
        {
            var viewModel = new StickerSourceViewModel(source);
            viewModel.Removed += item =>
            {
                Sources.Remove(item);
                _model.Sources?.Remove(source);
            };
            Sources.Add(viewModel);
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
