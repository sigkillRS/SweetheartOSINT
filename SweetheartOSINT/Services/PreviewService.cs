using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SweetheartOSINT.Models;
using SweetheartOSINT.Views;

namespace SweetheartOSINT.Services
{
    public static class PreviewService
    {
        public const int PreviewWidth = 520;
        public const int PreviewHeight = 320;

        public static void GeneratePreview(ProjectData project)
        {
            var targetPath = StorageService.GetPreviewPath(project);
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Build an offscreen visual that represents the project page and render it.
            var visual = new PreviewVisual
            {
                Width = PreviewWidth,
                Height = PreviewHeight
            };
            visual.DataContext = project;

            visual.Measure(new Size(PreviewWidth, PreviewHeight));
            visual.Arrange(new Rect(0, 0, PreviewWidth, PreviewHeight));
            visual.UpdateLayout();

            var bitmap = new RenderTargetBitmap(
                PreviewWidth,
                PreviewHeight,
                96,
                96,
                PixelFormats.Pbgra32);

            bitmap.Render(visual);
            bitmap.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (var stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                encoder.Save(stream);
            }
        }
    }
}
