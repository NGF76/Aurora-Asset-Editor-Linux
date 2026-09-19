using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
//using Classes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using AuroraAssetEditorLinux.Dialogs;
using AuroraAssetEditorLinux.Controls;
using AuroraAssetEditorLinux.Classes;

namespace AuroraAssetEditorLinux.Controls
{
    public partial class ScreenshotsControl : UserControl
    {
        private readonly MainWindow _main;
        private AuroraAsset.AssetFile _assetFile;
        private Image<Rgba32>[] _screenshots;
        private bool _hasPreview;

        public bool HavePreview => _hasPreview;
        public bool HaveScreenshots => _screenshots.Any(t => t != null);

        public ScreenshotsControl(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            _assetFile = new AuroraAsset.AssetFile();
            
            var maxScreenshots = AuroraAsset.AssetType.ScreenshotEnd - AuroraAsset.AssetType.ScreenshotStart;
            _screenshots = new Image<Rgba32>[maxScreenshots];

            // ربط أحداث السحب والإفلات
            this.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
            this.AddHandler(DragDrop.DropEvent, OnDrop);

            // ربط أحداث القائمة
            if (SaveContextMenuItem != null)
            {
                SaveContextMenuItem.Click += SaveImageToFileOnClick;
            }

            if (RemoveContextMenuItem != null)
            {
                RemoveContextMenuItem.Click += RemoveScreenshot!;
            }

            // ربط عناصر القائمة الأخرى
            var contextMenu = PreviewImg?.ContextMenu;
            if (contextMenu?.Items.Count >= 3)
            {
                var replaceItem = contextMenu.Items[1] as MenuItem;
                if (replaceItem != null)
                {
                    replaceItem.Click += SelectNewScreenshot!;
                }

                var addItem = contextMenu.Items[2] as MenuItem;
                if (addItem != null)
                {
                    addItem.Click += AddNewScreenshot!;
                }
            }

            // ربط اختيار الصورة من القائمة المنسدلة
            CBox.SelectionChanged += CBox_SelectionChanged!;

            // تهيئة القائمة المنسدلة
            CBox.Items.Clear();
            for (var i = 0; i < _screenshots.Length; i++)
                CBox.Items.Add(new ScreenshotDisplay(i));
            CBox.SelectedIndex = 0;

            // تحميل الصورة الافتراضية
            LoadDefaultImage();
        }

        private void LoadDefaultImage()
        {
            try
            {
                var uri = new Uri("avares://AuroraAssetEditor/Assets/Placeholders/screenshot.png");
                PreviewImg.Source = new Bitmap(AssetLoader.Open(uri));
                _hasPreview = false;
            }
            catch
            {
                PreviewImg.Source = null;
            }
        }

        private void SetPreview(Image<Rgba32>? img)
        {
            if (img == null)
            {
                LoadDefaultImage();
                _hasPreview = false;
                return;
            }

            using var ms = new MemoryStream();
            img.SaveAsPng(ms);
            ms.Seek(0, SeekOrigin.Begin);
            PreviewImg.Source = new Bitmap(ms);
            _hasPreview = true;
        }

        public void Save()
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null) return;

            var options = new FilePickerSaveOptions
            {
                Title = "Save Screenshots To File",
                SuggestedFileName = "screenshots.asset",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Asset File") { Patterns = new[] { "*.asset" } }
                }
            };

            _ = SaveFileAsync(options);
        }

        private async Task SaveFileAsync(FilePickerSaveOptions options)
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null) return;

            var result = await storageProvider.SaveFilePickerAsync(options);
            if (result != null)
            {
                // تجميع البيانات قبل الحفظ
                SaveData();
                await using var stream = await result.OpenWriteAsync();
                await stream.WriteAsync(_assetFile.FileData);
            }
        }

        public void Save(string filename)
        {
            SaveData();
            File.WriteAllBytes(filename, _assetFile.FileData);
        }

        private void SaveData()
        {
            var shouldUseCompression = false;
            Dispatcher.UIThread.Invoke(() => shouldUseCompression = _main.UseCompression.IsChecked);

            for (var index = 0; index < _screenshots.Length; index++)
            {
                var screenshot = _screenshots[index];
                if (!Equals(screenshot, _assetFile.GetScreenshot(index + 1)))
                    _assetFile.SetScreenshot(screenshot, index + 1, shouldUseCompression);
            }
        }

        public void Reset()
        {
            SetPreview(null);
            _assetFile = new AuroraAsset.AssetFile();
            _screenshots = new Image<Rgba32>[AuroraAsset.AssetType.ScreenshotEnd - AuroraAsset.AssetType.ScreenshotStart];
            
            // تحديث القائمة المنسدلة
            CBox.Items.Clear();
            for (var i = 0; i < _screenshots.Length; i++)
                CBox.Items.Add(new ScreenshotDisplay(i));
            CBox.SelectedIndex = 0;
        }

        public void Load(AuroraAsset.AssetFile asset)
        {
            _assetFile.SetScreenshots(asset);
            Dispatcher.UIThread.Invoke(() =>
            {
                var convertedScreenshots = new Image<Rgba32>[_screenshots.Length];

                for (var i = 0; i < convertedScreenshots.Length; i++)
                {
                    var screenshot = _assetFile.GetScreenshot(i + 1);
                    if (screenshot == null) continue;

                    using var ms = new MemoryStream();
                    screenshot.SaveAsPng(ms);
                    ms.Position = 0;
                    convertedScreenshots[i] = SixLabors.ImageSharp.Image.Load<Rgba32>(ms);
                }

                _screenshots = convertedScreenshots;
                CBox_SelectionChanged(null, null);
            });
        }

        public void Load(Image<Rgba32> img, bool replace)
        {
            var index = -1;

            if (replace)
            {
                var disp = CBox.SelectedItem as ScreenshotDisplay;
                if (disp == null)
                {
                    // البحث عن أول فتحة فارغة
                    for (var i = 0; i < _screenshots.Length; i++)
                    {
                        if (_screenshots[i] != null) continue;
                        index = i;
                        break;
                    }
                }
                else
                {
                    index = disp.Index;
                }
            }
            else
            {
                // البحث عن أول فتحة فارغة
                for (var i = 0; i < _screenshots.Length; i++)
                {
                    if (_screenshots[i] != null) continue;
                    index = i;
                    break;
                }
            }

            if (index == -1)
            {
                _ = CustomMessageBox.ShowAsync(this.FindAncestorOfType<Window>()!, "There is no space left for new screenshots :(", "No space left", false);
                return;
            }

            var shouldUseCompression = false;
            Dispatcher.UIThread.Invoke(() => shouldUseCompression = _main.UseCompression.IsChecked);
            
            _assetFile.SetScreenshot(img, index + 1, shouldUseCompression);
            Dispatcher.UIThread.Invoke(() =>
            {
                _screenshots[index] = img;
                SetPreview(img);
                CBox.SelectedIndex = index;
            });
        }

        public bool SelectedExists()
        {
            var disp = CBox.SelectedItem as ScreenshotDisplay;
            if (disp != null)
                return _screenshots[disp.Index] != null;
            return false;
        }

        public bool SpaceLeft()
        {
            return _screenshots.Any(t => t == null);
        }

        // ============ أحداث القائمة المنسدلة ============

        private void CBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var disp = CBox.SelectedItem as ScreenshotDisplay;
            if (disp == null) return;
            SetPreview(_screenshots[disp.Index]);
        }

        // ============ دوال السحب والإفلات ============

        private void OnDragEnter(object? sender, DragEventArgs e)
        {
            if (e.DataTransfer.Contains(DataFormat.File))
                e.DragEffects = DragDropEffects.Copy;
            else
                e.DragEffects = DragDropEffects.None;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            _main.DragDrop(this, e);
        }

        // ============ دوال القائمة ============

        internal void RemoveScreenshot(object? sender, EventArgs e)
        {
            Load(null!, true);
        }

        internal async void SaveImageToFileOnClick(object? sender, EventArgs e)
        {
            var disp = CBox.SelectedItem as ScreenshotDisplay;
            if (disp == null) return;
            
            var img = _screenshots[disp.Index];
            if (img != null)
            {
                await MainWindow.SaveToFile(img, "Select where to save the Screenshot", $"screenshot{disp.Index + 1}.png", this);
            }
        }

        internal async void SelectNewScreenshot(object? sender, EventArgs e)
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null) return;

            var options = new FilePickerOpenOptions
            {
                Title = "Select new screenshot",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images") 
                    { 
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.tif", "*.tiff" } 
                    }
                }
            };

            _main.BusyIndicator.IsVisible = true;
            var result = await storageProvider.OpenFilePickerAsync(options);
            
            if (result?.Count > 0)
            {
                try
                {
                    await using var stream = await result[0].OpenReadAsync();
                    var image = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);
                    image.Mutate(ctx => ctx.Resize(1000, 562));
                    Load(image, true);
                }
                catch (Exception ex)
                {
                    await CustomMessageBox.ShowAsync(this.FindAncestorOfType<Window>()!, $"Error loading image: {ex.Message}", "Error", false);
                }
            }
            _main.BusyIndicator.IsVisible = false;
        }

        internal async void AddNewScreenshot(object? sender, EventArgs e)
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null) return;

            var options = new FilePickerOpenOptions
            {
                Title = "Select new screenshot(s)",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images") 
                    { 
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.tif", "*.tiff" } 
                    }
                }
            };

            _main.BusyIndicator.IsVisible = true;
            var results = await storageProvider.OpenFilePickerAsync(options);
            
            if (results?.Count > 0)
            {
                try
                {
                    foreach (var file in results)
                    {
                        if (!SpaceLeft())
                        {
                            await CustomMessageBox.ShowAsync(this.FindAncestorOfType<Window>()!, "No space left for more screenshots.", "No Space", false);
                            break;
                        }

                        await using var stream = await file.OpenReadAsync();
                        var image = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);
                        image.Mutate(ctx => ctx.Resize(1000, 562));
                        Load(image, false);
                    }
                }
                catch (Exception ex)
                {
                    await CustomMessageBox.ShowAsync(this.FindAncestorOfType<Window>()!, $"Error loading images: {ex.Message}", "Error", false);
                }
            }
            _main.BusyIndicator.IsVisible = false;
        }

        public byte[] GetData()
        {
            // التأكد من حفظ البيانات قبل الحصول عليها
            SaveData();
            return _assetFile.FileData;
        }

        // ============ نموذج عرض لقطة الشاشة ============

        private class ScreenshotDisplay
        {
            private readonly int _index;

            public ScreenshotDisplay(int index)
            {
                _index = index;
            }

            public int Index => _index;

            public override string ToString()
            {
                return $"Screenshot {_index + 1}";
            }
        }
    }
}
