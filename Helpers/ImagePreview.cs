using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AuroraAssetEditorLinux.Helpers
{
    /// <summary>
    /// Creates Avalonia preview bitmaps and releases the previous preview when it is replaced.
    /// </summary>
    internal static class ImagePreview
    {
        public static Bitmap CreateBitmap(Image<Rgba32> image)
        {
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            stream.Position = 0;
            return new Bitmap(stream);
        }

        public static Bitmap? LoadPlaceholder(string name)
        {
            try
            {
                var uri = new Uri($"avares://AuroraAssetEditor/Assets/Placeholders/{name}.png");
                return new Bitmap(AssetLoader.Open(uri));
            }
            catch
            {
                return null;
            }
        }

        public static void SetSource(Avalonia.Controls.Image control, IImage? source)
        {
            var previous = control.Source;
            if (ReferenceEquals(previous, source))
                return;

            control.Source = source;
            (previous as IDisposable)?.Dispose();
        }

        public static void Clear(Avalonia.Controls.Image control) => SetSource(control, null);
    }
}
