using System;
using System.Windows;
using System.Windows.Input;
using SweetheartOSINT.Models;
using SweetheartOSINT.Services;

namespace SweetheartOSINT.Views
{
    public partial class CreateProjectWindow : Window
    {
        private bool _syncingPercent;

        public CreateProjectWindow()
        {
            InitializeComponent();
            PercentSlider.ValueChanged += PercentSlider_ValueChanged;
            NameTextBox.Focus();
        }

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

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            var name = NameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введите название проекта.", "Sweetheart OSINT",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                NameTextBox.Focus();
                return;
            }

            if (!int.TryParse(PercentTextBox.Text, out var percent))
                percent = (int)PercentSlider.Value;
            percent = Math.Min(100, Math.Max(0, percent));

            var project = new ProjectData
            {
                Name = name,
                Percent = percent,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            StorageService.CreateProject(project);
            PreviewService.GeneratePreview(project);

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Create_Click(sender, null);
        }
    }
}
