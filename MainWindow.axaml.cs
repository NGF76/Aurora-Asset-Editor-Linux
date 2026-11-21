using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using AuroraAssetEditorLinux.Controls;
using AuroraAssetEditorLinux.Classes;
using AuroraAssetEditorLinux.Helpers;
using AuroraAssetEditorLinux.Models;
using AuroraAssetEditorLinux.Dialogs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AuroraAssetEditorLinux
{
    public class GameData
    {
        public bool IsGameSelected { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TitleId { get; set; } = string.Empty;
        public string DbId { get; set; } = string.Empty;
    }

    public static class GlobalState
    {
        public static GameData CurrentGame { get; set; } = new();

        public static event Action? GameChanged;

        public static void RaiseGameChanged()
        {
            GameChanged?.Invoke();
        }
    }

    public partial class MainWindow : Window
    {
        private const string AssetFileFilter =
            "Game Cover/Boxart Asset File(defaultFilename) (GC*.asset)|GC*.asset|Background Asset File(defaultFilename) (BK*.asset)|BK*.asset|Icon/Banner Asset File(defaultFilename) (GL*.asset)|GL*.asset|Screenshot Asset File(defaultFilename) (SS*.asset)|SS*.asset|Aurora Asset Files (*.asset)|*.asset|FSD Assets Files (*.assets)|*.assets|All Files(*)|*";

        private const string ImageFileFilter =
            "All Supported Images|*.png;*.bmp;*.jpg;*.jpeg;*.gif;*.tif;*.tiff;|BMP (*.bmp)|*.bmp|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|GIF (*.gif)|*.gif|TIFF (*.tif;*.tiff)|*.tiff;*.tif|PNG (*.png)|*.png|All Files|*";

        private readonly BackgroundControl _background;
        private readonly MenuItem[] _backgroundMenu;
        private readonly BoxartControl _boxart;
        private readonly MenuItem[] _boxartMenu;
        private readonly IconBannerControl _iconBanner;
        private readonly object[] _iconBannerMenu;
        private readonly ScreenshotsControl _screenshots;
        private readonly MenuItem[] _screenshotsMenu;

        public MainWindow()
        {
            InitializeComponent();

            var ver = Assembly.GetAssembly(typeof(MainWindow))?.GetName().Version;
            if (ver != null)
            {
                Title = string.Format(Title?.ToString() ?? "Aurora Asset Editor v{0}.{1}.{2}", ver.Major, ver.Minor, ver.Build);
            }

            DataContext = GlobalState.CurrentGame;
            GlobalState.GameChanged += OnGameChanged;

            CreateNewAssetMenu.Click += CreateNewAssetMenu_Click;
            LoadAssetMenu.Click += LoadAssetOnClick;
            SaveAllAssetsMenu.Click += SaveAllAssetsMenu_Click;
            SaveBoxartMenu.Click += (s, e) => _boxart.Save();
            SaveBackgroundMenu.Click += (s, e) => _background.Save();
            SaveScreenshotsMenu.Click += (s, e) => _screenshots.Save();
            SaveIconBannerMenu.Click += (s, e) => _iconBanner.Save();
            ExitMenu.Click += (s, e) => Close();
            GameTitleIdMenu.Click += CopyTitleIdToClipboard_Click;
            GameDbIdMenu.Click += CopyDbIdToClipboard_Click;

            // add support for TLS 1.1 and TLS 1.2
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol
                | (SecurityProtocolType)768 // TLS 1.1
                | (SecurityProtocolType)3072; // TLS 1.2

            #region Boxart
            _boxart = new BoxartControl(this);
            BoxartTab.Content = _boxart;
            _boxartMenu = new[] {
                new MenuItem { Header = "Save Cover To File" },
                new MenuItem { Header = "Crop Cover to 2:3" },
                new MenuItem { Header = "Select new Cover" }
            };
            _boxartMenu[0].Click += _boxart.SaveImageToFileOnClick;
            _boxartMenu[1].Click += _boxart.CropCover;
            _boxartMenu[2].Click += _boxart.SelectNewCover;
            #endregion

            #region Background
            _background = new BackgroundControl(this);
            BackgroundTab.Content = _background;
            _backgroundMenu = new[] {
                new MenuItem { Header = "Save Background To File" },
                new MenuItem { Header = "Select new Background" }
            };
            _backgroundMenu[0].Click += _background.SaveImageToFileOnClick;
            _backgroundMenu[1].Click += _background.SelectNewBackground;
            #endregion

            #region Icon & Banner
            _iconBanner = new IconBannerControl(this);
            IconBannerTab.Content = _iconBanner;
            _iconBannerMenu = new object[] {
                new MenuItem { Header = "Save Icon To File" },
                new MenuItem { Header = "Select new Icon" },
                new Separator(),
                new MenuItem { Header = "Save Banner To File" },
                new MenuItem { Header = "Select new Banner" }
            };
            ((MenuItem)_iconBannerMenu[0]).Click += _iconBanner.SaveIconToFileOnClick;
            ((MenuItem)_iconBannerMenu[1]).Click += _iconBanner.SelectNewIcon;
            ((MenuItem)_iconBannerMenu[3]).Click += _iconBanner.SaveBannerToFileOnClick;
            ((MenuItem)_iconBannerMenu[4]).Click += _iconBanner.SelectNewBanner;
            #endregion

            #region Screenshots
            _screenshots = new ScreenshotsControl(this);
            ScreenshotsTab.Content = _screenshots;
            _screenshotsMenu = new[] {
                new MenuItem { Header = "Save Screenshot To File" },
                new MenuItem { Header = "Replace Screenshot" },
                new MenuItem { Header = "Add new Screenshot(s)" },
                new MenuItem { Header = "Remove screenshot" }
            };
            _screenshotsMenu[0].Click += _screenshots.SaveImageToFileOnClick;
            _screenshotsMenu[1].Click += _screenshots.SelectNewScreenshot;
            _screenshotsMenu[2].Click += _screenshots.AddNewScreenshot;
            _screenshotsMenu[3].Click += _screenshots.RemoveScreenshot;
            #endregion

            OnlineAssetsTab.Content = new OnlineAssetsControl(this, _boxart, _background, _iconBanner, _screenshots);
            FtpAssetsTab.Content = new TextBlock
            {
                Text = "FTP asset controls are unavailable in this build.",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
        }

        private void OnGameChanged()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                GameSelector.IsVisible = GlobalState.CurrentGame.IsGameSelected;
                GameSelector.Header = GlobalState.CurrentGame.Title;
                GameTitleIdMenu.Header = GlobalState.CurrentGame.TitleId;
                GameDbIdMenu.Header = GlobalState.CurrentGame.DbId;
            });
        }

        internal string GetAssetFilename(string prefix, string fallback)
        {
            var titleId = GlobalState.CurrentGame.TitleId?.Trim().ToUpperInvariant();
            return !string.IsNullOrEmpty(titleId) && Regex.IsMatch(titleId, "^[0-9A-F]{8}$")
                ? $"{prefix}{titleId}.asset"
                : fallback;
        }

        private async void CreateNewAssetMenu_Click(object? sender, EventArgs e)
        {
            _boxart.Reset();
            _background.Reset();
            _iconBanner.Reset();
            _screenshots.Reset();
            GlobalState.CurrentGame = new GameData();
            GlobalState.RaiseGameChanged();

            var idDialog = new TitleAndDbIdDialog(this);
            await idDialog.ShowDialog(this);
            if (idDialog.DialogResult != true)
                return;

            var folder = await FolderBrowserDialogAsync("Select the game folder containing the cover image");
            if (string.IsNullOrWhiteSpace(folder))
                return;

            var coverFile = FindCoverImage(folder);
            if (coverFile == null)
            {
                await ShowMessageAsync("No supported image was found in the selected folder.", "Create New Asset");
                return;
            }

            try
            {
                await using var stream = File.OpenRead(coverFile);
                var image = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(stream);
                _boxart.Load(image);

                GlobalState.CurrentGame = new GameData
                {
                    IsGameSelected = true,
                    Title = new DirectoryInfo(folder).Name,
                    TitleId = idDialog.TitleId.ToUpperInvariant(),
                    DbId = idDialog.DbId.ToUpperInvariant()
                };
                GlobalState.RaiseGameChanged();
                BoxartTab.IsSelected = true;
            }
            catch (Exception ex)
            {
                SaveError(ex);
                await ShowMessageAsync($"Error loading the cover image:\n\n{ex.Message}", "Create New Asset");
            }
        }

        private static string? FindCoverImage(string folder)
        {
            var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".bmp", ".jpg", ".jpeg", ".gif", ".tif", ".tiff"
            };
            var images = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file)))
                .ToArray();

            return images
                .OrderByDescending(file =>
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    return name.Contains("cover", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("boxart", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("front", StringComparison.OrdinalIgnoreCase);
                })
                .ThenByDescending(file => File.GetLastWriteTimeUtc(file))
                    .ThenBy(file => file, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private async void SaveAllAssetsMenu_Click(object? sender, EventArgs e)
        {
            var folder = await FolderBrowserDialogAsync("Select a folder to save all Aurora assets");
            if (string.IsNullOrWhiteSpace(folder))
                return;

            _boxart.Save(Path.Combine(folder, GetAssetFilename("GC", "cover.asset")));
            _background.Save(Path.Combine(folder, GetAssetFilename("BK", "background.asset")));
            _iconBanner.Save(Path.Combine(folder, GetAssetFilename("GL", "icon_banner.asset")));
            _screenshots.Save(Path.Combine(folder, GetAssetFilename("SS", "screenshots.asset")));
        }

        // ============ دوال تحميل الملفات ============

        private async void LoadAssetOnClick(object? sender, EventArgs e)
        {
            var files = await OpenFileDialogAsync(AssetFileFilter, true);
            if (files == null || files.Length == 0) return;

            BusyIndicator.IsVisible = true;
            try
            {
                foreach (var fileName in files)
                {
                    if (VerifyAuroraMagic(fileName))
                        await LoadAuroraAsset(fileName);
                    else
                        await LoadFsdAsset(fileName);
                }
            }
            finally
            {
                BusyIndicator.IsVisible = false;
            }
        }

        private async Task LoadAuroraAsset(string filename)
        {
            try
            {
                var asset = await Task.Run(() => new AuroraAsset.AssetFile(File.ReadAllBytes(filename)));
                if (asset.HasBoxArt)
                {
                    _boxart.Load(asset);
                    BoxartTab.IsSelected = true;
                }
                else if (asset.HasBackground)
                {
                    _background.Load(asset);
                    BackgroundTab.IsSelected = true;
                }
                else if (asset.HasScreenshots)
                {
                    _screenshots.Load(asset);
                    ScreenshotsTab.IsSelected = true;
                }
                else if (asset.HasIconBanner)
                {
                    _iconBanner.Load(asset);
                    IconBannerTab.IsSelected = true;
                }
                else
                {
                    await ShowMessageAsync($"ERROR: {filename} Doesn't contain any Assets", "ERROR");
                }
            }
            catch (Exception ex)
            {
                SaveError(ex);
                await ShowMessageAsync($"ERROR: While processing {filename}\n\n{ex.Message}\n\nSee error.log for more details", "ERROR");
            }
        }

        private async Task LoadFsdAsset(string filename)
        {
            try
            {
                var image = await Task.Run(() =>
                {
                    var asset = new FsdAsset(File.ReadAllBytes(filename));
                    return asset.GetBoxart();
                });

                if (image is Image<Rgba32> boxart)
                {
                    _boxart.Load(boxart);
                    BoxartTab.IsSelected = true;
                }
                else
                {
                    await ShowMessageAsync($"ERROR: {filename} Doesn't contain any Assets", "ERROR");
                }
            }
            catch (Exception ex)
            {
                SaveError(ex);
                await ShowMessageAsync($"ERROR: While processing {filename}\n\n{ex.Message}\n\nSee error.log for more details", "ERROR");
            }
        }

        private async Task ShowMessageAsync(string message, string title)
        {
            //  صحيح (Avalonia مع CustomMessageBox)
            await CustomMessageBox.ShowAsync(
            this,           // النافذة الأم
            message,
            title,
            false           // لا حاجة لزر Cancel
           );
        }

        // ============ دوال المساعدة ============

        private static bool VerifyAuroraMagic(string fileName)
        {
            try
            {
                using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length < 4)
                    return false;
                using var br = new BinaryReader(stream);
                return br.ReadUInt32() == 0x41455852; /* RXEA in LittleEndian format */
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool VerifyFsdMagic(string fileName)
        {
            try
            {
                using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length < 4)
                    return false;
                using var br = new BinaryReader(stream);
                return br.ReadUInt32() == 0x41445346; /* FSDA in LittleEndian format */
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static void SaveError(Exception ex)
        {
            try
            {
                File.AppendAllText("error.log", $"[{DateTime.Now}]:{ex}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never hide the original failure.
            }
        }

        // ============ دوال الحوار المحدثة ============

        private async Task<string[]?> OpenFileDialogAsync(string filter, bool multiselect = false)
        {
            var storageProvider = this.StorageProvider;
            if (storageProvider == null) return null;

            var options = new FilePickerOpenOptions
            {
                AllowMultiple = multiselect,
                FileTypeFilter = ParseFilter(filter)
            };
            var result = await storageProvider.OpenFilePickerAsync(options);
            return result?.Select(f => f.Path.LocalPath).ToArray();
        }

        private async Task<string?> SaveFileDialogAsync(string title, string defaultFilename, string filter)
        {
            var storageProvider = this.StorageProvider;
            if (storageProvider == null) return null;

            var options = new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = defaultFilename,
                FileTypeChoices = ParseFilter(filter)
            };
            var result = await storageProvider.SaveFilePickerAsync(options);
            return result?.Path.LocalPath;
        }

        private async Task<string?> FolderBrowserDialogAsync(string description)
        {
            var storageProvider = this.StorageProvider;
            if (storageProvider == null) return null;

            var options = new FolderPickerOpenOptions
            {
                Title = description
            };
            var result = await storageProvider.OpenFolderPickerAsync(options);
            return result?.FirstOrDefault()?.Path.LocalPath;
        }

        private List<FilePickerFileType> ParseFilter(string filter)
        {
            var result = new List<FilePickerFileType>();
            if (string.IsNullOrEmpty(filter)) return result;

            var parts = filter.Split('|');
            for (int i = 0; i < parts.Length; i += 2)
            {
                if (i + 1 < parts.Length)
                {
                    var name = parts[i].Trim();
                    var patterns = parts[i + 1].Split(';')
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToArray();

                    if (patterns.Length > 0)
                    {
                        result.Add(new FilePickerFileType(name)
                        {
                            Patterns = patterns
                        });
                    }
                }
            }
            return result;
        }

        // ============ دوال السحب والإفلات ============

        internal void OnDragEnter(object? sender, DragEventArgs e)
        {
            if (e.DataTransfer.Contains(DataFormat.File))
                e.DragEffects = DragDropEffects.Copy;
            else
                e.DragEffects = DragDropEffects.None;
        }

        internal async void DragDrop(Control sender, DragEventArgs e)
        {
            if (!e.DataTransfer.Contains(DataFormat.File)) return;

            var files = e.DataTransfer.TryGetFiles()?.Select(f => f.Path.LocalPath).ToArray() ?? Array.Empty<string>();
            BusyIndicator.IsVisible = true;
            try
            {
                foreach (var file in files)
                {
                    if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
                    {
                        if (VerifyAuroraMagic(file))
                            await LoadAuroraAsset(file);
                        else
                            await LoadFsdAsset(file);
                    }
                }
            }
            finally
            {
                BusyIndicator.IsVisible = false;
            }
        }


        private async void CopyTitleIdToClipboard_Click(object? sender, EventArgs e)
        {
            string titleId = GlobalState.CurrentGame.TitleId;
            if (!string.IsNullOrEmpty(titleId))
            {
                await CopyTextToClipboardAsync(this, titleId);
            }
        }

        private async void CopyDbIdToClipboard_Click(object? sender, EventArgs e)
        {
            string DbID = GlobalState.CurrentGame.DbId;
            if (!string.IsNullOrEmpty(DbID))
            {
                await CopyTextToClipboardAsync(this, DbID);
            }
        }

        private static async Task CopyTextToClipboardAsync(Window window, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            var clipboard = TopLevel.GetTopLevel(window)?.Clipboard;
            if (clipboard == null) return;

            await clipboard.SetTextAsync(text);
        }

        // ============ دوال الإغلاق ============

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            GlobalState.GameChanged -= OnGameChanged;
            base.OnClosing(e);
        }

        public static async Task SaveToFile(object img, string title, string defaultFilename, Control parent)
        {
            var storageProvider = TopLevel.GetTopLevel(parent)?.StorageProvider;
            if (storageProvider == null || img is not Image<Rgba32> image)
                return;

            var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = defaultFilename,
                FileTypeChoices = new[] { new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } } }
            });
            if (result == null)
                return;

            await using var stream = await result.OpenWriteAsync();
            await image.SaveAsPngAsync(stream);
        }
    }
}