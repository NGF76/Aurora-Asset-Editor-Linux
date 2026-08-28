using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using AuroraAssetEditorLinux.Classes;
using AuroraAssetEditorLinux.Controls;
using AuroraAssetEditorLinux.Helpers;
using AuroraAssetEditorLinux.Models;
//using Classes;
//using Models;
//using Controls;
//using Helpers;

namespace AuroraAssetEditorLinux
{
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

            // add support for TLS 1.1 and TLS 1.2
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol
                | (SecurityProtocolType)768 // TLS 1.1
                | (SecurityProtocolType)3072; // TLS 1.2

            #region Boxart
            _boxart = new BoxartControl(this);
            BoxartTab.Content = _boxart;
            _boxartMenu = new[] {
                new MenuItem { Header = "Save Cover To File" },
                new MenuItem { Header = "Select new Cover" }
            };
            _boxartMenu[0].Click += _boxart.SaveImageToFileOnClick;
            _boxartMenu[1].Click += _boxart.SelectNewCover;
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
            FtpAssetsTab.Content = new FtpAssetsControl(this, _boxart, _background, _iconBanner, _screenshots);
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

        // ============ دوال تحميل الملفات ============

        private async void LoadAssetOnClick(object? sender, EventArgs e)
        {
            var files = await OpenFileDialogAsync(AssetFileFilter, true);
            if (files == null || files.Length == 0) return;

            BusyIndicator.IsVisible = true;
            await Task.Run(() =>
            {
                foreach (var fileName in files)
                {
                    if (VerifyAuroraMagic(fileName))
                        LoadAuroraAsset(fileName);
                    else
                        LoadFsdAsset(fileName);
                }
            });
            BusyIndicator.IsVisible = false;
        }

        private async void LoadAuroraAsset(string filename)
        {
            try
            {
                var asset = new AuroraAsset.AssetFile(File.ReadAllBytes(filename));
                if (asset.HasBoxArt)
                {
                    _boxart.Load(asset);
                    Dispatcher.UIThread.Invoke(() => BoxartTab.IsSelected = true);
                }
                else if (asset.HasBackground)
                {
                    _background.Load(asset);
                    Dispatcher.UIThread.Invoke(() => BackgroundTab.IsSelected = true);
                }
                else if (asset.HasScreenshots)
                {
                    _screenshots.Load(asset);
                    Dispatcher.UIThread.Invoke(() => ScreenshotsTab.IsSelected = true);
                }
                else if (asset.HasIconBanner)
                {
                    _iconBanner.Load(asset);
                    Dispatcher.UIThread.Invoke(() => IconBannerTab.IsSelected = true);
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

        private async void LoadFsdAsset(string filename)
        {
            try
            {
                var asset = new FsdAsset(File.ReadAllBytes(filename));
                var img = asset.GetBoxart();
                if (img != null)
                {
                    _boxart.Load(img);
                    Dispatcher.UIThread.Invoke(() => BoxartTab.IsSelected = true);
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
            // في Avalonia، نستخدم MessageBox
            // يمكنك استخدام MessageBox أو أي طريقة أخرى لعرض الرسائل
            await MessageBox.Show(message, title);
        }

        // ============ دوال المساعدة ============

        private static bool VerifyAuroraMagic(string fileName)
        {
            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(stream);
            return br.ReadUInt32() == 0x41455852; /* RXEA in LittleEndian format */
        }

        private static bool VerifyFsdMagic(string fileName)
        {
            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(stream);
            return br.ReadUInt32() == 0x41445346; /* FSDA in LittleEndian format */
        }

        internal static void SaveError(Exception ex)
        {
            File.AppendAllText("error.log", $"[{DateTime.Now}]:{ex}{Environment.NewLine}");
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
            if (e.Data.Contains(DataFormats.FileNames))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        internal async void DragDrop(Control sender, DragEventArgs e)
        {
            if (!e.Data.Contains(DataFormats.FileNames)) return;
            
            var files = e.Data.GetText()?.Split('\n') ?? Array.Empty<string>();
            BusyIndicator.IsVisible = true;
            
            await Task.Run(() =>
            {
                // منطق السحب والإفلات
                foreach (var file in files)
                {
                    if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
                    {
                        // معالجة الملف
                    }
                }
            });
            BusyIndicator.IsVisible = false;
        }

        // ============ دوال الحافظة ============

        private async void CopyTitleIdToClipboard_Click(object? sender, EventArgs e)
        {
            string titleId = GlobalState.CurrentGame.TitleId;
            if (!string.IsNullOrEmpty(titleId))
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(titleId);
                }
            }
        }

        private async void CopyDbIdToClipboard_Click(object? sender, EventArgs e)
        {
            string DbID = GlobalState.CurrentGame.DbId;
            if (!string.IsNullOrEmpty(DbID))
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(DbID);
                }
            }
        }

        // ============ دوال الإغلاق ============

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            GlobalState.GameChanged -= OnGameChanged;
            base.OnClosing(e);
        }

        // ============ دوال إضافية (للكمال) ============

        public static async Task SaveToFile(object img, string title, string defaultFilename, Control parent)
        {
            // تنفيذ منطق حفظ الملف
            // هذه الدالة سيتم تنفيذها لاحقاً
        }
    }
}
