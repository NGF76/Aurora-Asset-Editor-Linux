using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using System;
using System.IO;
using System.Threading.Tasks;
using AuroraAssetEditorLinux.Classes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AuroraAssetEditorLinux.Controls
{
    public partial class BoxartControl : UserControl
    {
        private readonly MainWindow _main;
        private AuroraAsset.AssetFile _assetFile;
        private MemoryStream? _memoryStream;
        private bool _hasPreview;

        public bool HavePreview => _hasPreview;

        public BoxartControl(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            _assetFile = new AuroraAsset.AssetFile();

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
                    selectItem.Click += SelectNewCover;
                }
            }

            // تحميل الصورة الافتراضية
            LoadDefaultImage();
        }

        private void LoadDefaultImage()
        {
            try
            {
                var uri = new Uri("avares://AuroraAssetEditorLinux/Assets/Placeholders/cover.png");
                PreviewImg.Source = new Bitmap(AssetLoader.Open(uri));
                _hasPreview = false;
            }
            catch
            {
                PreviewImg.Source = null;
            }
        }

        public void Save()
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null) return;

            var options = new FilePickerSaveOptions
            {
                Title = "Save Cover To File",
                SuggestedFileName = "cover.png",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } },
                    new FilePickerFileType("JPEG Image") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                    new FilePickerFileType("BMP Image") { Patterns = new[] { "*.bmp" } }
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
            _assetFile = new AuroraAsset.AssetFile();
            _hasPreview = false;
            _memoryStream?.Close();
            _memoryStream = null;
            LoadDefaultImage();
        }

        public void Load(AuroraAsset.AssetFile asset)
        {
            _assetFile.SetBoxart(asset);
            Dispatcher.UIThread.Invoke(() => SetPreview(_assetFile.GetBoxart()));
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

            var bitmap = new Bitmap(_memoryStream);
            PreviewImg.Source = bitmap;
            _hasPreview = true;
        }

        public void Load(Image<Rgba32> img)
        {
            var shouldUseCompression = false;
            Dispatcher.UIThread.Invoke(() => shouldUseCompression = _main.UseCompression.IsChecked);
            _assetFile.SetBoxart(img, shouldUseCompression);
            Dispatcher.UIThread.Invoke(() => SetPreview(img));
        }

        // ============ دوال السحب والإفلات ============

        private void OnDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.FileNames))
            {
                e.DragEffects = DragDropEffects.Copy;  // ✅
            }
            else
            {
                e.DragEffects = DragDropEffects.None;   // ✅
            }
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            _main.DragDrop(this, e);
        }

        // ============ دوال القائمة ============

        internal async void SaveImageToFileOnClick(object? sender, EventArgs e)
        {
            var img = _assetFile.GetBoxart();
            if (img != null)
            {
                await MainWindow.SaveToFile(img, "Select where to save the Cover", "cover.png", this);
            }
        }

        internal async void SelectNewCover(object? sender, EventArgs e)
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null) return;

            var options = new FilePickerOpenOptions
            {
                Title = "Select new Cover",
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
                    var image = Image.Load<Rgba32>(stream);
                    Load(image);
                    _main.BusyIndicator.IsVisible = false;
                }
                catch (Exception ex)
                {
                    await MessageBoxManager
                        .GetMessageBoxStandardWindow("Error", $"Error loading image: {ex.Message}")
                        .Show();
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

        public Image<Rgba32>? GetPreviewImage()
        {
            return _assetFile.GetBoxart();
        }
    }
}