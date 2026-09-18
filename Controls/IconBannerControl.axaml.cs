using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Platform;
using Avalonia.VisualTree;
using AuroraAssetEditorLinux.Dialogs;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading.Tasks;
//using Classes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using AuroraAssetEditorLinux.Controls;
using AuroraAssetEditorLinux.Classes;

namespace AuroraAssetEditorLinux.Controls
{
    public partial class IconBannerControl : UserControl
    {
        private readonly MainWindow _main;
        private AuroraAsset.AssetFile _assetFile;
        private MemoryStream? _iconMemoryStream;
        private MemoryStream? _bannerMemoryStream;
        private bool _hasIcon;
        private bool _hasBanner;

        public bool HaveIcon => _hasIcon;
        public bool HaveBanner => _hasBanner;
        public bool HaveIconBanner => _hasIcon || _hasBanner;

        public IconBannerControl(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            _assetFile = new AuroraAsset.AssetFile();

            // ربط أحداث السحب والإفلات
            this.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
            this.AddHandler(DragDrop.DropEvent, OnDrop);

            // ربط أحداث قائمة الأيقونة
            if (SaveIconContextMenuItem != null)
            {
                SaveIconContextMenuItem.Click += SaveIconToFileOnClick;
            }

            var iconContextMenu = PreviewIcon?.ContextMenu;
            if (iconContextMenu?.Items.Count > 1)
            {
                var selectIconItem = iconContextMenu.Items[1] as MenuItem;
                if (selectIconItem != null)
                {
                    selectIconItem.Click += SelectNewIcon;
                }
            }

            // ربط أحداث قائمة البانر
            if (SaveBannerContextMenuItem != null)
            {
                SaveBannerContextMenuItem.Click += SaveBannerToFileOnClick;
            }

            var bannerContextMenu = PreviewBanner?.ContextMenu;
            if (bannerContextMenu?.Items.Count > 1)
            {
                var selectBannerItem = bannerContextMenu.Items[1] as MenuItem;
                if (selectBannerItem != null)
                {
                    selectBannerItem.Click += SelectNewBanner;
                }
            }

            // تحميل الصور الافتراضية
            LoadDefaultImages();
        }

        private void LoadDefaultImages()
        {
            try
            {
                var iconUri = new Uri("avares://AuroraAssetEditor/Assets/Placeholders/icon.png");
                PreviewIcon.Source = new Bitmap(AssetLoader.Open(iconUri));
                _hasIcon = false;

                var bannerUri = new Uri("avares://AuroraAssetEditor/Assets/Placeholders/banner.png");
                PreviewBanner.Source = new Bitmap(AssetLoader.Open(bannerUri));
                _hasBanner = false;
            }
            catch
            {
                PreviewIcon.Source = null;
                PreviewBanner.Source = null;
            }
        }

        public void Save()
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null) return;

            var options = new FilePickerSaveOptions
            {
                Title = "Save Icon/Banner To File",
                SuggestedFileName = "icon_banner.asset",
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
            _assetFile = new AuroraAsset.AssetFile();
            _hasIcon = false;
            _hasBanner = false;
            _iconMemoryStream?.Close();
            _iconMemoryStream = null;
            _bannerMemoryStream?.Close();
            _bannerMemoryStream = null;
            LoadDefaultImages();
        }

        public void Load(AuroraAsset.AssetFile asset)
        {
            _assetFile.SetIcon(asset);
            _assetFile.SetBanner(asset);
            Dispatcher.UIThread.Invoke(() =>
            {
                SetPreview(_assetFile.GetIcon(), true);
                SetPreview(_assetFile.GetBanner(), false);
            });
        }

        private void SetPreview(Image<Rgba32>? img, bool icon)
        {
            if (img == null)
            {
                if (icon)
                {
                    LoadDefaultImage(PreviewIcon, "icon");
                    _hasIcon = false;
                }
                else
                {
                    LoadDefaultImage(PreviewBanner, "banner");
                    _hasBanner = false;
                }
                return;
            }

            var ms = new MemoryStream();
            img.SaveAsPng(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var bitmap = new Bitmap(ms);
            
            if (icon)
            {
                _iconMemoryStream?.Close();
                _iconMemoryStream = ms;
                PreviewIcon.Source = bitmap;
                _hasIcon = true;
            }
            else
            {
                _bannerMemoryStream?.Close();
                _bannerMemoryStream = ms;
                PreviewBanner.Source = bitmap;
                _hasBanner = true;
            }
        }

        private void LoadDefaultImage(Avalonia.Controls.Image control, string type)
        {
            try
            {
                var uri = new Uri($"avares://AuroraAssetEditor/Assets/Placeholders/{type}.png");
                control.Source = new Bitmap(AssetLoader.Open(uri));
            }
            catch
            {
                control.Source = null;
            }
        }

        public void Load(Image<Rgba32> img, bool icon)
        {
            var shouldUseCompression = false;
            Dispatcher.UIThread.Invoke(() => shouldUseCompression = _main.UseCompression.IsChecked);
            
            if (icon)
                _assetFile.SetIcon(img, shouldUseCompression);
            else
                _assetFile.SetBanner(img, shouldUseCompression);
            
            Dispatcher.UIThread.Invoke(() => SetPreview(img, icon));
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

        // ============ دوال قائمة الأيقونة ============

        internal async void SaveIconToFileOnClick(object? sender, EventArgs e)
        {
            var img = _assetFile.GetIcon();
            if (img != null)
            {
                await MainWindow.SaveToFile(img, "Select where to save the Icon", "icon.png", this);
            }
        }

        internal async void SelectNewIcon(object? sender, EventArgs e)
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null) return;

            var options = new FilePickerOpenOptions
            {
                Title = "Select new Icon",
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
                    image.Mutate(ctx => ctx.Resize(64, 64));
                    Load(image, true);
                }
                catch (Exception ex)
                {
                    await CustomMessageBox.ShowAsync(this.FindAncestorOfType<Window>()!, $"Error loading image: {ex.Message}", "Error", false);
                }
            }
            _main.BusyIndicator.IsVisible = false;
        }

        // ============ دوال قائمة البانر ============

        internal async void SaveBannerToFileOnClick(object? sender, EventArgs e)
        {
            var img = _assetFile.GetBanner();
            if (img != null)
            {
                await MainWindow.SaveToFile(img, "Select where to save the Banner", "banner.png", this);
            }
        }

        internal async void SelectNewBanner(object? sender, EventArgs e)
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null) return;

            var options = new FilePickerOpenOptions
            {
                Title = "Select new Banner",
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
                    image.Mutate(ctx => ctx.Resize(420, 96));
                    Load(image, false);
                }
                catch (Exception ex)
                {
                    await CustomMessageBox.ShowAsync(this.FindAncestorOfType<Window>()!, $"Error loading image: {ex.Message}", "Error", false);
                }
            }
            _main.BusyIndicator.IsVisible = false;
        }

        public byte[] GetData()
        {
            return _assetFile.FileData;
        }

        public Image<Rgba32>? GetIconImage()
        {
            return _assetFile.GetIcon();
        }

        public Image<Rgba32>? GetBannerImage()
        {
            return _assetFile.GetBanner();
        }
    }
}
