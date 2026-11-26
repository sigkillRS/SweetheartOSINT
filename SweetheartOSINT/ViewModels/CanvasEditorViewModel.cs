using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SweetheartOSINT.Models;
using SweetheartOSINT.Services;

namespace SweetheartOSINT.ViewModels
{
    public class CanvasEditorViewModel : INotifyPropertyChanged
    {
        public const double MinZoom = 0.3;
        public const double MaxZoom = 3.0;

        private readonly ProjectData _project;
        private readonly Func<string, string> _resolveImage;
        private readonly Dictionary<string, StickerViewModel> _byId = new Dictionary<string, StickerViewModel>();
        private StickerViewModel _selected;
        private double _zoom = 1;
        private double _offsetX;
        private double _offsetY;
        private string _status = "Правый клик по холсту — создать стикер";

        public CanvasEditorViewModel(ProjectData project)
        {
            _project = project ?? new ProjectData();

            AddStickerCommand = new RelayCommand(_ => AddStickerAtCenter());
            DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected(), _ => Selected != null);
            ZoomInCommand = new RelayCommand(_ => ZoomBy(1.2));
            ZoomOutCommand = new RelayCommand(_ => ZoomBy(1 / 1.2));
            ResetViewCommand = new RelayCommand(_ => ResetView());

            _resolveImage = file => StorageService.GetImagePath(_project, file);

            foreach (var sticker in _project.Stickers ?? new List<Sticker>())
            {
                var viewModel = new StickerViewModel(sticker, _resolveImage);
                viewModel.GeometryChanged += OnStickerGeometryChanged;
                _byId[sticker.Id] = viewModel;
                Stickers.Add(viewModel);
            }

            foreach (var link in _project.StickerLinks ?? new List<StickerLink>())
            {
                if (_byId.TryGetValue(link.FromId, out var from) && _byId.TryGetValue(link.ToId, out var to))
                    Links.Add(new StickerLinkViewModel(link, from, to));
            }

            var viewport = _project.CanvasViewport ?? new CanvasViewport();
            _zoom = viewport.Zoom > 0 ? viewport.Zoom : 1;
            _offsetX = viewport.OffsetX;
            _offsetY = viewport.OffsetY;
        }

        public ObservableCollection<StickerViewModel> Stickers { get; } = new ObservableCollection<StickerViewModel>();
        public ObservableCollection<StickerLinkViewModel> Links { get; } = new ObservableCollection<StickerLinkViewModel>();

        public ICommand AddStickerCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ResetViewCommand { get; }

        public event Action ImagesChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public StickerViewModel Selected
        {
            get => _selected;
            set
            {
                if (_selected == value)
                    return;
                if (_selected != null)
                    _selected.IsSelected = false;
                _selected = value;
                if (_selected != null)
                    _selected.IsSelected = true;
                OnPropertyChanged();
                ((RelayCommand)DeleteSelectedCommand).RaiseCanExecuteChanged();
            }
        }

        public double Zoom
        {
            get => _zoom;
            set
            {
                var next = Math.Min(MaxZoom, Math.Max(MinZoom, value));
                if (Math.Abs(_zoom - next) < 0.0001)
                    return;
                _zoom = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ZoomPercent));
            }
        }

        public double OffsetX
        {
            get => _offsetX;
            set
            {
                if (Math.Abs(_offsetX - value) < 0.01)
                    return;
                _offsetX = value;
                OnPropertyChanged();
            }
        }

        public double OffsetY
        {
            get => _offsetY;
            set
            {
                if (Math.Abs(_offsetY - value) < 0.01)
                    return;
                _offsetY = value;
                OnPropertyChanged();
            }
        }

        public string ZoomPercent => Math.Round(_zoom * 100) + " %";

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public StickerViewModel AddSticker(double x, double y)
        {
            var model = new Sticker { X = x, Y = y };
            var viewModel = new StickerViewModel(model, _resolveImage);
            viewModel.GeometryChanged += OnStickerGeometryChanged;

            _byId[model.Id] = viewModel;
            Stickers.Add(viewModel);
            Selected = viewModel;
            Status = "Стикер создан · перетащите его мышью";
            return viewModel;
        }

        public StickerViewModel AddStickerAtCenter()
        {
            double width = 900 / Zoom;
            double height = 600 / Zoom;
            return AddSticker(-OffsetX / Zoom + width / 2 - 115, -OffsetY / Zoom + height / 2 - 95);
        }

        public void Select(StickerViewModel sticker)
        {
            Selected = sticker;
            if (sticker != null)
                Status = sticker.Id;
        }

        public bool DeleteSelected()
        {
            if (Selected == null)
                return false;

            var target = Selected;
            foreach (var link in Links.Where(l => l.From == target || l.To == target).ToList())
                Links.Remove(link);

            Stickers.Remove(target);
            _byId.Remove(target.Id);
            Selected = null;
            Status = "Стикер удалён";
            return true;
        }

        public bool AddLink(string fromId, string toId)
        {
            if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId) || fromId == toId)
                return false;
            if (!_byId.TryGetValue(fromId, out var from) || !_byId.TryGetValue(toId, out var to))
                return false;
            if (Links.Any(l => l.From == from && l.To == to))
            {
                Status = "Такая связь уже есть";
                return false;
            }

            var model = new StickerLink { FromId = fromId, ToId = toId };
            Links.Add(new StickerLinkViewModel(model, from, to));
            Status = "Связь добавлена";
            return true;
        }

        public void RemoveLink(StickerLinkViewModel link)
        {
            if (link == null)
                return;
            if (Links.Remove(link))
                Status = "Связь удалена";
        }

        public void AddImage(StickerViewModel sticker, string sourcePath)
        {
            var imported = StorageService.ImportImages(_project, new[] { sourcePath });
            if (imported.Count == 0)
                return;

            _project.ImageFiles ??= new List<string>();
            if (!_project.ImageFiles.Contains(imported[0]))
                _project.ImageFiles.Add(imported[0]);

            sticker.SetImage(imported[0]);
            ImagesChanged?.Invoke();
            Status = "Картинка добавлена";
        }

        public void RemoveImage(StickerViewModel sticker)
        {
            if (!sticker.HasImage)
                return;
            sticker.RemoveImage();
            Status = "Картинка убрана";
        }

        public void SetViewport(double zoom, double offsetX, double offsetY)
        {
            Zoom = zoom;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        public void ZoomBy(double factor)
        {
            Zoom *= factor;
            Status = "Масштаб: " + ZoomPercent;
        }

        public void ResetView()
        {
            SetViewport(1, 0, 0);
            Status = "Вид сброшен";
        }

        public void ApplyToProject()
        {
            _project.Stickers = Stickers.Select(s => s.Model).ToList();
            _project.StickerLinks = Links.Select(l => l.Model).ToList();
            _project.CanvasViewport = new CanvasViewport
            {
                Zoom = _zoom,
                OffsetX = _offsetX,
                OffsetY = _offsetY
            };

            var files = new HashSet<string>(_project.ImageFiles ?? new List<string>());
            foreach (var sticker in Stickers)
            {
                if (!string.IsNullOrEmpty(sticker.Model.ImageFile))
                    files.Add(sticker.Model.ImageFile);
            }
            _project.ImageFiles = files.ToList();
        }

        // ---------- Playback state ----------

        public sealed class PlaybackState
        {
            public List<Sticker> Stickers { get; set; } = new List<Sticker>();
            public List<StickerLink> Links { get; set; } = new List<StickerLink>();
            public double Zoom { get; set; }
            public double OffsetX { get; set; }
            public double OffsetY { get; set; }
        }

        public PlaybackState CapturePlaybackState()
        {
            return new PlaybackState
            {
                Stickers = Stickers.Select(s => s.Model).ToList(),
                Links = Links.Select(l => l.Model).ToList(),
                Zoom = _zoom,
                OffsetX = _offsetX,
                OffsetY = _offsetY
            };
        }

        public List<StickerViewModel> CreatePlaybackStickers(PlaybackState state)
        {
            var result = new List<StickerViewModel>();
            foreach (var source in state.Stickers)
            {
                var clone = CloneSticker(source);
                // The image is injected by the image cursor at the right moment.
                clone.ImageFile = null;
                var viewModel = new StickerViewModel(clone, _resolveImage);
                viewModel.GeometryChanged += OnStickerGeometryChanged;
                result.Add(viewModel);
            }
            return result;
        }

        public string GetPlaybackImageFile(PlaybackState state, string stickerId)
        {
            return state.Stickers.FirstOrDefault(s => s.Id == stickerId)?.ImageFile;
        }

        private static Sticker CloneSticker(Sticker source)
        {
            return new Sticker
            {
                Id = source.Id,
                X = source.X,
                Y = source.Y,
                Width = source.Width,
                Height = source.Height,
                Text = source.Text,
                ImageFile = source.ImageFile,
                ImageRotation = source.ImageRotation,
                Sources = (source.Sources ?? new List<StickerSource>())
                    .Select(s => new StickerSource { Id = s.Id, Url = s.Url })
                    .ToList()
            };
        }

        public void BeginPlayback()
        {
            Selected = null;
            Stickers.Clear();
            Links.Clear();
            _byId.Clear();
        }

        public void AddPlaybackSticker(StickerViewModel sticker)
        {
            if (sticker == null)
                return;
            _byId[sticker.Id] = sticker;
            Stickers.Add(sticker);
        }

        public StickerLinkViewModel AddPlaybackLink(StickerLink model)
        {
            if (model == null ||
                !_byId.TryGetValue(model.FromId, out var from) ||
                !_byId.TryGetValue(model.ToId, out var to))
                return null;

            var link = new StickerLinkViewModel(model, from, to)
            {
                PlaybackOpacity = 0
            };
            Links.Add(link);
            return link;
        }

        public void EndPlayback(PlaybackState state)
        {
            Stickers.Clear();
            Links.Clear();
            _byId.Clear();
            Selected = null;

            foreach (var model in state.Stickers)
            {
                var viewModel = new StickerViewModel(model, _resolveImage);
                viewModel.GeometryChanged += OnStickerGeometryChanged;
                _byId[model.Id] = viewModel;
                Stickers.Add(viewModel);
            }

            foreach (var link in state.Links)
            {
                if (_byId.TryGetValue(link.FromId, out var from) && _byId.TryGetValue(link.ToId, out var to))
                    Links.Add(new StickerLinkViewModel(link, from, to));
            }

            SetViewport(state.Zoom, state.OffsetX, state.OffsetY);
        }

        private void OnStickerGeometryChanged(StickerViewModel sticker)
        {
            foreach (var link in Links)
            {
                if (link.From == sticker || link.To == sticker)
                    link.Refresh();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
