using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using SweetheartOSINT.Converters;
using SweetheartOSINT.Models;
using SweetheartOSINT.Services;

namespace SweetheartOSINT.Views
{
    public partial class ProfileWindow : Window
    {
        private readonly UserProfile _profile;
        private string _selectedAvatarPath;

        public ProfileWindow()
        {
            InitializeComponent();

            _profile = StorageService.LoadProfile();
            NickTextBox.Text = _profile.Nickname ?? string.Empty;
            ShowAvatar(StorageService.GetAvatarPath(_profile));
        }

        private void BrowseAvatar_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите аватарку",
                Filter = "Изображения|*.png;*.jpg;*.jpeg|PNG|*.png|JPG|*.jpg;*.jpeg|Все файлы|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
                return;

            _selectedAvatarPath = dialog.FileName;
            ShowAvatar(_selectedAvatarPath);
        }

        private void ShowAvatar(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                AvatarImage.Source = ImageLoader.Load(path);
                AvatarImage.Visibility = Visibility.Visible;
                InitialsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                AvatarImage.Source = null;
                AvatarImage.Visibility = Visibility.Collapsed;
                InitialsText.Visibility = Visibility.Visible;
                InitialsText.Text = GetInitials(NickTextBox.Text);
            }
        }

        private static string GetInitials(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                return "??";

            var parts = nickname.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "??";

            return string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _profile.Nickname = NickTextBox.Text.Trim();

            if (!string.IsNullOrEmpty(_selectedAvatarPath))
                _profile.AvatarFile = StorageService.SaveAvatar(_selectedAvatarPath);

            StorageService.SaveProfile(_profile);

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
