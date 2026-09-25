using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Platform;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using AuroraAssetEditorLinux.Controls;
using AuroraAssetEditorLinux.Classes;
using AuroraAssetEditorLinux.Dialogs;
using Avalonia.VisualTree;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using static AuroraAssetEditorLinux.Classes.AuroraAsset;


namespace AuroraAssetEditorLinux.Controls
{
    public partial class BackgroundControl : UserControl
    {
        private readonly MainWindow _main;
        private AssetFile _assetFile;
        private MemoryStream? _memoryStream;
        private bool _hasPreview;

        public bool HavePreview => _hasPreview;

        public BackgroundControl(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            _assetFile = new AssetFile();

            // ربط أحداث السحب والإفلات
            this.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
            this.AddHandler(DragDrop.DropEvent, OnDrop);

            // ربط أحداث القائمة
            if (SaveContextMenuItem != null)
            {
                SaveContextMenuItem.Click += SaveImageToFileOnClick;
            }

            // ربط حدث اختيار الصورة الجديدة
            var contextMenu = PreviewImg?.ContextMenu;
            if (contextMenu?.Items.Count > 1)
            {
                var selectItem = contextMenu.Items[1] as MenuItem;
                if (selectItem != null)
                {
                    selectItem.Click += SelectNewBackground;
                }
            }

            // تحميل الصورة الافتراضية
            LoadDefaultImage();
        }

        private void LoadDefaultImage()
        {
            try
            {
                var uri = new Uri("avares://AuroraAssetEditor/Assets/Placeholders/background.png");
                PreviewImg.Source = new Bitmap(AssetLoader.Open(uri));
                _hasPreview = false;
            }
            catch
            {
                // إذا لم توجد الصورة، نتركها فارغة
                PreviewImg.Source = null;
            }
        }

        public void Save()
        {
            // استخدام StorageProvider لحفظ الملف
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null) return;

            var options = new FilePickerSaveOptions
            {
                Title = "Save Background To File",
                SuggestedFileName = "background.png",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } },
                    new FilePickerFileType("JPEG Image") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                    new FilePickerFileType("BMP Image") { Patterns = new[] { "*.bmp" } }
                }
            };

            _ = SaveFileAsync(options);
        }

        public void SaveAsset()
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null) return;

            var options = new FilePickerSaveOptions
            {
                Title = "Save Background Asset",
                SuggestedFileName = _main.GetAssetFilename("BK", "background.asset"),
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
                await using var stream = await result.OpenWriteAsync();
                await stream.WriteAsync(_assetFile.FileData);
            }
        }

        public void Save(string filename)
        {
            File.WriteAllBytes(filename, _assetFile.FileData);
        }

        public void Reset()
        {
            _assetFile = new AssetFile();
            _hasPreview = false;
            _memoryStream?.Close();
            _memoryStream = null;
            LoadDefaultImage();
        }

        public void Load(AuroraAsset.AssetFile asset)
        {
            _assetFile.SetBackground(asset);
            Dispatcher.UIThread.Invoke(() => SetPreview(_assetFile.GetBackground()));
        }

        private void SetPreview(Image<Rgba32>? img)
        {
            if (img == null)
            {
                LoadDefaultImage();
                _hasPreview = false;
                return;
            }

            _memoryStream?.Close();
            _memoryStream = new MemoryStream();
            img.SaveAsPng(_memoryStream);
            _memoryStream.Seek(0, SeekOrigin.Begin);

            var previewBitmap = new Bitmap(_memoryStream);
            PreviewImg.Source = previewBitmap;
            _hasPreview = true;
        }

        public void Load(Image<Rgba32> img)
        {
            var shouldUseCompression = false;
            Dispatcher.UIThread.Invoke(() => shouldUseCompression = _main.UseCompression.IsChecked);
            _assetFile.SetBackground(img, shouldUseCompression);
            Dispatcher.UIThread.Invoke(() => SetPreview(img));
        }

        // ============ دوال السحب والإفلات ============

        private void OnDragEnter(object? sender, DragEventArgs e)
        {
            var data = e.GetType().GetProperty("Data")?.GetValue(e);
            var contains = data?.GetType().GetMethod("Contains", new[] { typeof(string) });
            var hasFiles = contains != null && contains.Invoke(data, new object[] { "Files" }) is bool isFileDrop && isFileDrop;

            if (hasFiles)
            {
                e.DragEffects = DragDropEffects.Copy;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            // يتم التعامل مع السحب والإفلات في MainWindow
            _main.DragDrop(this, e);
        }

        // ============ دوال القائمة ============

        internal async void SaveImageToFileOnClick(object? sender, EventArgs e)
        {
            var img = _assetFile.GetBackground();
            if (img != null)
            {
                await MainWindow.SaveToFile(img, "Select where to save the Background", "background.png", this);
            }
        }

        internal async void SelectNewBackground(object? sender, EventArgs e)
{
    var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
    if (storageProvider == null) return;

    var options = new FilePickerOpenOptions
    {
        Title = "Select new background",
        AllowMultiple = false,
        FileTypeFilter = new[]
        {
            new FilePickerFileType("Images") 
            { 
                Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.tif", "*.tiff" } 
            }
        }
    };

    var result = await storageProvider.OpenFilePickerAsync(options);
    if (result?.Count > 0)
    {
        try
        {
            await using var stream = await result[0].OpenReadAsync();

                    Load(SixLabors.ImageSharp.Image.Load<Rgba32>(stream));
            _main.BusyIndicator.IsVisible = false;
        }
        catch (Exception ex)
        {
            //  تعريف parentWindow قبل الاستخدام
            var parentWindow = this.FindAncestorOfType<Window>();
            if (parentWindow != null)
            {
                await CustomMessageBox.ShowAsync(
                    parentWindow,
                    $"Error loading image: {ex.Message}",
                    "Error",
                    false
                );
            }
            _main.BusyIndicator.IsVisible = false;
        }
    }
    else
    {
        _main.BusyIndicator.IsVisible = false;
    }
}

        public byte[] GetData()
        {
            return _assetFile.FileData;
        }

        // دالة للوصول إلى الصورة (للاستخدام في MainWindow)
        public Image<Rgba32>? GetPreviewImage()
        {
            return _assetFile.GetBackground();
        }
    }
}
