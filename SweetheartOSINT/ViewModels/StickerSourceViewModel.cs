using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SweetheartOSINT.Models;

namespace SweetheartOSINT.ViewModels
{
    public class StickerSourceViewModel : INotifyPropertyChanged
    {
        private readonly StickerSource _source;

        public StickerSourceViewModel(StickerSource source)
        {
            _source = source ?? new StickerSource();
            RemoveCommand = new RelayCommand(_ => Removed?.Invoke(this));
        }

        public StickerSource Model => _source;
        public ICommand RemoveCommand { get; }
        public event Action<StickerSourceViewModel> Removed;

        public string Url
        {
            get => _source.Url;
            set
            {
                _source.Url = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
