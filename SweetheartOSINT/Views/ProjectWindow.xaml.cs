using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SweetheartOSINT.Models;
using SweetheartOSINT.Services;

namespace SweetheartOSINT.Views
{
    public partial class ProjectWindow : Window
    {
        private readonly ProjectData _project;
        private readonly ObservableCollection<SourceItem> _sources;
        private readonly ObservableCollection<string> _imageFiles = new ObservableCollection<string>();
        private readonly ObservableCollection<string> _imagePaths = new ObservableCollection<string>();
        private bool _saved;
        private bool _syncingPercent;

        public ProjectWindow(ProjectData project)
        {
            InitializeComponent();

            _project = project;
            DataContext = project;

            _sources = new ObservableCollection<SourceItem>(project.Sources ?? new List<SourceItem>());
            foreach (var file in project.ImageFiles ?? new List<string>())
                _imageFiles.Add(file);

            SourcesItemsControl.ItemsSource = _sources;
            ImagesItemsControl.ItemsSource = _imagePaths;
            ToolsItemsControl.ItemsSource = GetToolLinks();

            CanvasEditor.Load(project);
            CanvasEditor.ImagesChanged += CanvasImagesChanged;

            NameTextBox.Text = project.Name;
            PercentSlider.Value = Math.Min(100, Math.Max(0, project.Percent));
            PercentTextBox.Text = project.Percent.ToString("0");

            PercentSlider.ValueChanged += PercentSlider_ValueChanged;
            RefreshEmptyStates();
            RefreshImagePaths();
        }

        private static List<ToolLink> GetToolLinks()
        {
            return new List<ToolLink>
            {
                new ToolLink
                {
                    Name = "Sherlock (поиск по нику)",
                    Description = "Поиск имени пользователя на множестве сайтов",
                    Url = "https://github.com/sherlock-project/sherlock"
                },
                new ToolLink
                {
                    Name = "WhatsMyName (поиск по нику)",
                    Description = "Каталог сайтов для проверки ников",
                    Url = "https://whatsmyname.app/"
                },
                new ToolLink
                {
                    Name = "Epieos (поиск по email/телефону)",
                    Description = "Проверка email, телефонов и аккаунтов",
                    Url = "https://epieos.com/"
                },
                new ToolLink
                {
                    Name = "Have I Been Pwned (проверка утечек)",
                    Description = "Поиск адреса в известных утечках данных",
                    Url = "https://haveibeenpwned.com/"
                },
                new ToolLink
                {
                    Name = "Whois (домены)",
                    Description = "Информация о регистрации домена",
                    Url = "https://who.is/"
                },
                new ToolLink
                {
                    Name = "Wayback Machine (архивы)",
                    Description = "Архивные копии веб-страниц",
                    Url = "https://web.archive.org/"
                }
            };
        }

        private void RefreshEmptyStates()
        {
            SourcesEmptyText.Visibility = _sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ImagesEmptyText.Visibility = _imageFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatsText.Text = $"Источников: {_sources.Count} · Изображений: {_imageFiles.Count}";
        }

        private void CanvasImagesChanged()
        {
            foreach (var file in _project.ImageFiles ?? new List<string>())
            {
                if (!_imageFiles.Contains(file))
                    _imageFiles.Add(file);
            }
            RefreshImagePaths();
        }

        private void RefreshImagePaths()
        {
            _imagePaths.Clear();
            foreach (var file in _imageFiles)
                _imagePaths.Add(StorageService.GetImagePath(_project, file));
            RefreshEmptyStates();
        }

        // ---------- Percent ----------

        private void PercentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingPercent)
                return;

            _syncingPercent = true;
            PercentTextBox.Text = Math.Round(PercentSlider.Value).ToString("0");
            _syncingPercent = false;
        }

        private void PercentTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // XAML sets Text before the slider field is assigned.
            if (_syncingPercent || PercentSlider == null ||
                !int.TryParse(PercentTextBox.Text, out var value))
                return;

            _syncingPercent = true;
            PercentSlider.Value = Math.Min(100, Math.Max(0, value));
            _syncingPercent = false;
        }

        // ---------- Sources ----------

        private void AddSource_Click(object sender, RoutedEventArgs e)
        {
            var content = SourceInputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(content))
            {
                MessageBox.Show("Введите ссылку или текст заметки.", "Sweetheart OSINT",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                SourceInputTextBox.Focus();
                return;
            }

            var type = SourceTypeCombo.SelectedIndex == 0 ? "link" : "note";
            _sources.Add(new SourceItem
            {
                Type = type,
                Content = content,
                CreatedAt = DateTime.Now
            });

            SourceInputTextBox.Clear();
            SourceInputTextBox.Focus();
            RefreshEmptyStates();
        }

        private void SourceInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                AddSource_Click(sender, null);
        }

        private void DeleteSource_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not SourceItem source)
                return;
            _sources.Remove(source);
            RefreshEmptyStates();
        }

        private void OpenSource_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not SourceItem source)
                return;

            OpenUrl(source.Content);
        }

        private static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            var target = url.Trim();
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

        // ---------- Images ----------

        private void AddImages_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите изображения",
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|Все файлы|*.*",
                Multiselect = true,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
                return;

            var imported = StorageService.ImportImages(_project, dialog.FileNames);
            foreach (var file in imported)
                _imageFiles.Add(file);

            RefreshImagePaths();
        }

        private void DeleteImage_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not string path)
                return;

            var fileName = Path.GetFileName(path);
            if (!_imageFiles.Remove(fileName))
                _imageFiles.Remove(path);

            StorageService.DeleteImage(_project, fileName);
            RefreshImagePaths();
        }

        // ---------- Tools ----------

        private void ToolsToggle_Click(object sender, RoutedEventArgs e)
        {
            ToolsPanel.Visibility = ToolsPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void ToolLink_Click(object sender, RoutedEventArgs e)
        {
            var url = (sender as FrameworkElement)?.Tag as string;
            OpenUrl(url);
        }

        // ---------- Save ----------

        private bool Persist()
        {
            var name = NameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введите название проекта.", "Sweetheart OSINT",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                NameTextBox.Focus();
                return false;
            }

            if (!int.TryParse(PercentTextBox.Text, out var percent))
                percent = (int)PercentSlider.Value;

            CanvasEditor.ApplyToProject();

            _project.Name = name;
            _project.Percent = Math.Min(100, Math.Max(0, percent));
            _project.Sources = _sources.ToList();
            CanvasImagesChanged();
            _project.ImageFiles = _imageFiles.ToList();
            _project.UpdatedAt = DateTime.Now;

            StorageService.SaveProject(_project);
            PreviewService.GeneratePreview(_project);
            return true;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!Persist())
                return;

            _saved = true;
            DialogResult = true;
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_saved)
                return;

            var answer = MessageBox.Show("Сохранить изменения проекта?", "Sweetheart OSINT",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (answer == MessageBoxResult.Yes && !Persist())
                e.Cancel = true;
        }
    }
}
