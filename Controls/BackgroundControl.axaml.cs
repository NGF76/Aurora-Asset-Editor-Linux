using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
//using Classes;
using Image = System.Drawing.Image;
using Size = System.Drawing.Size;
using AuroraAssetEditorLinux.Controls;

namespace AuroraAssetEditorLinux.Controls
{
    public partial class BackgroundControl : UserControl
    {
        private readonly MainWindow _main;
        private AuroraAsset.AssetFile _assetFile;
        private MemoryStream? _memoryStream;
        private bool _hasPreview;

        public bool HavePreview => _hasPreview;

        public BackgroundControl(MainWindow main)
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
            _assetFile = new AuroraAsset.AuroraAsset.AssetFile();
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

        private void SetPreview(Image? img)
        {
            if (img == null)
            {
                LoadDefaultImage();
                _hasPreview = false;
                return;
            }

            _memoryStream?.Close();
            _memoryStream = new MemoryStream();
            img.Save(_memoryStream, System.Drawing.Imaging.ImageFormat.Png);
            _memoryStream.Seek(0, SeekOrigin.Begin);

            var bitmap = new Bitmap(_memoryStream);
            PreviewImg.Source = bitmap;
            _hasPreview = true;
        }

        public void Load(Image img)
        {
            var shouldUseCompression = false;
            Dispatcher.UIThread.Invoke(() => shouldUseCompression = _main.UseCompression.IsChecked);
            _assetFile.SetBackground(img, shouldUseCompression);
            Dispatcher.UIThread.Invoke(() => SetPreview(img));
        }

        // ============ دوال السحب والإفلات ============

        private void OnDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.FileNames))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
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
                    var image = Image.FromStream(stream);
                    Load(image);
                    _main.BusyIndicator.IsVisible = false;
                }
                catch (Exception ex)
                {
                    await MessageBox.Show($"Error loading image: {ex.Message}", "Error");
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
        public Image? GetPreviewImage()
        {
            return _assetFile.GetBackground();
        }
    }
}
