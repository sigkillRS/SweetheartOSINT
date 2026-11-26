using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using SweetheartOSINT.Converters;
using SweetheartOSINT.Models;
using SweetheartOSINT.Services;

namespace SweetheartOSINT.Views
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<ProjectData> _projects = new ObservableCollection<ProjectData>();

        public MainWindow()
        {
            InitializeComponent();
            ProjectsItemsControl.ItemsSource = _projects;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshProjects();
            RefreshProfile();
        }

        private void RefreshProjects()
        {
            _projects.Clear();
            foreach (var project in StorageService.LoadProjects())
                _projects.Add(project);

            EmptyHint.Visibility = _projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshProfile()
        {
            var profile = StorageService.LoadProfile();
            var nickname = string.IsNullOrWhiteSpace(profile.Nickname) ? "Без ника" : profile.Nickname;
            NickText.Text = nickname;
            InitialsText.Text = GetInitials(nickname);

            var avatarPath = StorageService.GetAvatarPath(profile);
            if (!string.IsNullOrEmpty(avatarPath) && File.Exists(avatarPath))
            {
                AvatarImage.Source = ImageLoader.Load(avatarPath);
                AvatarImage.Visibility = Visibility.Visible;
                InitialsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                AvatarImage.Source = null;
                AvatarImage.Visibility = Visibility.Collapsed;
                InitialsText.Visibility = Visibility.Visible;
            }
        }

        private static string GetInitials(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                return "??";

            var parts = nickname.Split(new[] { ' ', '_', '-', '.' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "??";

            var initials = string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
            return initials.Length == 0 ? "??" : initials;
        }

        private void CreateProject_Click(object sender, RoutedEventArgs e)
        {
            var window = new CreateProjectWindow { Owner = this };
            if (window.ShowDialog() == true)
                RefreshProjects();
        }

        private void ProjectCard_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ProjectData project)
                return;

            var window = new ProjectWindow(project) { Owner = this };
            window.ShowDialog();
            RefreshProjects();
        }

        private void DeleteProject_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ProjectData project)
                return;

            var answer = MessageBox.Show(
                $"Удалить проект «{project.Name}» вместе со всеми данными?",
                "Sweetheart OSINT", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
                return;

            StorageService.DeleteProject(project);
            RefreshProjects();
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            var window = new ProfileWindow { Owner = this };
            if (window.ShowDialog() == true)
                RefreshProfile();
        }
    }
}
