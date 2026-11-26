using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SweetheartOSINT.Models;

namespace SweetheartOSINT.Services
{
    public static class StorageService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static string AppRoot => AppContext.BaseDirectory;
        public static string ProjectsRoot => Path.Combine(AppRoot, "Projects");
        public static string ProfilePath => Path.Combine(AppRoot, "profile.json");

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(ProjectsRoot);
        }

        // ---------- Profile ----------

        public static UserProfile LoadProfile()
        {
            try
            {
                if (File.Exists(ProfilePath))
                {
                    var profile = JsonSerializer.Deserialize<UserProfile>(File.ReadAllText(ProfilePath));
                    return profile ?? new UserProfile();
                }
            }
            catch
            {
                // damaged profile.json -> use empty profile
            }

            return new UserProfile();
        }

        public static void SaveProfile(UserProfile profile)
        {
            File.WriteAllText(ProfilePath, JsonSerializer.Serialize(profile, JsonOptions));
        }

        public static string GetAvatarPath(UserProfile profile)
        {
            return string.IsNullOrEmpty(profile?.AvatarFile)
                ? null
                : Path.Combine(AppRoot, profile.AvatarFile);
        }

        public static string SaveAvatar(string sourcePath)
        {
            var extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".png";

            var fileName = "avatar" + extension.ToLowerInvariant();
            var destination = Path.Combine(AppRoot, fileName);

            var previous = LoadProfile();
            File.Copy(sourcePath, destination, true);

            if (!string.IsNullOrEmpty(previous.AvatarFile) &&
                !string.Equals(previous.AvatarFile, fileName, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var oldPath = Path.Combine(AppRoot, previous.AvatarFile);
                    if (File.Exists(oldPath))
                        File.Delete(oldPath);
                }
                catch
                {
                    // old avatar is not critical
                }
            }

            return fileName;
        }

        // ---------- Projects ----------

        public static List<ProjectData> LoadProjects()
        {
            var result = new List<ProjectData>();
            if (!Directory.Exists(ProjectsRoot))
                return result;

            foreach (var directory in Directory.GetDirectories(ProjectsRoot))
            {
                var jsonPath = Path.Combine(directory, "project.json");
                if (!File.Exists(jsonPath))
                    continue;

                try
                {
                    var project = JsonSerializer.Deserialize<ProjectData>(File.ReadAllText(jsonPath));
                    if (project == null)
                        continue;

                    project.Sources ??= new List<SourceItem>();
                    project.ImageFiles ??= new List<string>();
                    project.Stickers ??= new List<Sticker>();
                    project.StickerLinks ??= new List<StickerLink>();
                    project.CanvasViewport ??= new CanvasViewport();
                    if (string.IsNullOrEmpty(project.Id))
                        project.Id = Path.GetFileName(directory);
                    if (string.IsNullOrEmpty(project.PreviewFile))
                        project.PreviewFile = "preview.png";

                    result.Add(project);
                }
                catch
                {
                    // skip corrupted project folder
                }
            }

            return result.OrderByDescending(p => p.UpdatedAt).ToList();
        }

        public static string GetProjectDirectory(ProjectData project)
        {
            return Path.Combine(ProjectsRoot, project.Id);
        }

        public static string GetProjectJsonPath(ProjectData project)
        {
            return Path.Combine(GetProjectDirectory(project), "project.json");
        }

        public static string GetPreviewPath(ProjectData project)
        {
            return Path.Combine(GetProjectDirectory(project), project.PreviewFile ?? "preview.png");
        }

        public static string GetImagesDirectory(ProjectData project)
        {
            return Path.Combine(GetProjectDirectory(project), "images");
        }

        public static string GetImagePath(ProjectData project, string fileName)
        {
            return Path.Combine(GetImagesDirectory(project), fileName);
        }

        public static void CreateProject(ProjectData project)
        {
            Directory.CreateDirectory(GetProjectDirectory(project));
            Directory.CreateDirectory(GetImagesDirectory(project));
            SaveProject(project);
        }

        public static void DeleteProject(ProjectData project)
        {
            if (project == null || string.IsNullOrEmpty(project.Id))
                return;

            try
            {
                var directory = GetProjectDirectory(project);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch
            {
                // project folder is locked or already removed
            }
        }

        public static void SaveProject(ProjectData project)
        {
            EnsureDirectories();
            Directory.CreateDirectory(GetProjectDirectory(project));
            Directory.CreateDirectory(GetImagesDirectory(project));

            var json = JsonSerializer.Serialize(project, JsonOptions);
            File.WriteAllText(GetProjectJsonPath(project), json);
        }

        public static List<string> ImportImages(ProjectData project, IEnumerable<string> sourcePaths)
        {
            var imported = new List<string>();
            Directory.CreateDirectory(GetImagesDirectory(project));

            foreach (var source in sourcePaths)
            {
                try
                {
                    var extension = Path.GetExtension(source);
                    if (string.IsNullOrWhiteSpace(extension))
                        extension = ".png";

                    var fileName = Guid.NewGuid().ToString("N") + extension.ToLowerInvariant();
                    File.Copy(source, GetImagePath(project, fileName), true);
                    imported.Add(fileName);
                }
                catch
                {
                    // skip files that cannot be copied
                }
            }

            return imported;
        }

        public static void DeleteImage(ProjectData project, string fileName)
        {
            try
            {
                var path = GetImagePath(project, fileName);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // image is already in use or removed
            }
        }
    }
}
