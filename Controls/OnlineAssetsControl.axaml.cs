using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AuroraAssetEditorLinux.Helpers;
//using Classes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using AuroraAssetEditorLinux.Controls;
using AuroraAssetEditorLinux.Classes;

namespace AuroraAssetEditorLinux.Controls
{
    public partial class OnlineAssetsControl : UserControl
    {
        private enum OnlineAssetSources
        {
            XboxUnityOption = 0,
            ArchiveOption = 1,
            XboxComOption = 2
        }

        private readonly MainWindow _main;
        private readonly BoxartControl _boxart;
        private readonly BackgroundControl _background;
        private readonly IconBannerControl _iconBanner;
        private readonly ScreenshotsControl _screenshots;

        private readonly XboxAssetDownloader _xboxAssetDownloader = new XboxAssetDownloader();
        private readonly InternetArchiveDownloader _internetArchiveDownloader = new InternetArchiveDownloader();
        private readonly Random _rand = new Random();

        private XboxLocale[] _locales = Array.Empty<XboxLocale>();
        private XboxUnity.XboxUnityAsset[] _unityResult = Array.Empty<XboxUnity.XboxUnityAsset>();
        private XboxTitleInfo[] _xboxResult = Array.Empty<XboxTitleInfo>();
        private InternetArchiveAsset[] _archiveResult = Array.Empty<InternetArchiveAsset>();
        private Image<Rgba32>? _currentImage;
        private string? _keywords;
        private uint _titleId;
        private bool _isBusy;

        // قوائم السياق
       private object[] _coverMenu;
       private object[] _iconMenu;
       private object[] _bannerMenu;
       private object[] _backgroundMenu;
       private object[] _screenshotsMenu;

        public OnlineAssetsControl(MainWindow main, BoxartControl boxart, BackgroundControl background,
                                   IconBannerControl iconBanner, ScreenshotsControl screenshots)
        {
            InitializeComponent();
            _main = main;
            _boxart = boxart;
            _background = background;
            _iconBanner = iconBanner;
            _screenshots = screenshots;

            // ربط الأحداث
            SourceBox.SelectionChanged += SourceBox_SelectionChanged!;
            TitleIdBox.TextChanged += TitleIdBox_TextChanged!;
            TitleIdBox.KeyDown += OnTitleIdBoxKeyDown!;
            KeywordsBox.KeyDown += OnKeywordsBoxKeyDown!;

            // ربط أزرار البحث
            TitleIdButton.Click += ByTitleIdClick!;
            KeywordsButton.Click += ByKeywordsClick!;
            DownloadAllButton.Click += DownloadAllButton_Click!;

            // ربط اختيار النتيجة
            ResultBox.SelectionChanged += ResultBox_SelectionChanged!;

            // تهيئة القوائم
            InitializeMenus();

            // تحميل اللغات
            LoadLocales();

            // ربط تغيير الحالة العالمية
            GlobalState.GameChanged += OnGameChanged;

            // ضبط المصدر الافتراضي
            SourceBox.SelectedIndex = (int)OnlineAssetSources.XboxUnityOption;
        }

        private void InitializeMenus()
        {
            // قائمة الغلاف
            _coverMenu = new object[]
            {
                new MenuItem { Header = "Save cover to file" },
                new MenuItem { Header = "Set as cover" }
            };
            ((MenuItem)_coverMenu[0]).Click += (s, e) => { if (_currentImage != null) _ = MainWindow.SaveToFile(_currentImage, "Select where to save the cover", "cover.png", this); };
            ((MenuItem)_coverMenu[1]).Click += (s, e) => { if (_currentImage != null) _boxart.Load(_currentImage); };

            // قائمة الأيقونة
            _iconMenu = new object[]
            {
                new MenuItem { Header = "Save icon to file" },
                new MenuItem { Header = "Set as icon" }
            };
            ((MenuItem)_iconMenu[0]).Click += (s, e) => { if (_currentImage != null) _ = MainWindow.SaveToFile(_currentImage, "Select where to save the icon", "icon.png", this); };
            ((MenuItem)_iconMenu[1]).Click += (s, e) => { if (_currentImage != null) _iconBanner.Load(_currentImage, true); };

            // قائمة البانر
            _bannerMenu = new object[]
            {
                new MenuItem { Header = "Save banner to file" },
                new MenuItem { Header = "Set as banner" }
            };
            ((MenuItem)_bannerMenu[0]).Click += (s, e) => { if (_currentImage != null) _ = MainWindow.SaveToFile(_currentImage, "Select where to save the banner", "banner.png", this); };
            ((MenuItem)_bannerMenu[1]).Click += (s, e) => { if (_currentImage != null) _iconBanner.Load(_currentImage, false); };

            // قائمة الخلفية
            _backgroundMenu = new object[]
            {
                new MenuItem { Header = "Save background to file" },
                new MenuItem { Header = "Set as background" }
            };
            ((MenuItem)_backgroundMenu[0]).Click += (s, e) => { if (_currentImage != null) _ = MainWindow.SaveToFile(_currentImage, "Select where to save the background", "background.png", this); };
            ((MenuItem)_backgroundMenu[1]).Click += (s, e) => { if (_currentImage != null) _background.Load(_currentImage); };

            // قائمة لقطات الشاشة
            _screenshotsMenu = new object[]
            {
                new MenuItem { Header = "Save screenshot to file" },
                new MenuItem { Header = "Replace current screenshot" },
                new MenuItem { Header = "Add new screenshot" }
            };
            ((MenuItem)_screenshotsMenu[0]).Click += (s, e) => { if (_currentImage != null) _ = MainWindow.SaveToFile(_currentImage, "Select where to save the screenshot", "screenshot.png", this); };
            ((MenuItem)_screenshotsMenu[1]).Click += (s, e) => { if (_currentImage != null) _screenshots.Load(_currentImage, true); };
            ((MenuItem)_screenshotsMenu[2]).Click += (s, e) => { if (_currentImage != null) _screenshots.Load(_currentImage, false); };
        }

        private async void LoadLocales()
        {
            await Task.Run(() =>
            {
                _locales = XboxAssetDownloader.GetLocales();
            });

            Dispatcher.UIThread.Invoke(() =>
            {
                LocaleBox.ItemsSource = _locales.Select(l => l.Locale).ToList();
                SourceBox.Items.Add("Xbox.com");
                
                // تعيين اللغة الافتراضية
                var index = 0;
                for (var i = 0; i < _locales.Length; i++)
                {
                    if (_locales[i].Locale.Equals("en-us", StringComparison.InvariantCultureIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }
                LocaleBox.SelectedIndex = index;
            });
        }

        private void OnGameChanged()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                TitleIdBox.Text = GlobalState.CurrentGame.TitleId;
            });
        }

        // ============ دوال الأحداث ============

        private void SourceBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var sourceIndex = SourceBox.SelectedIndex;
            LocaleGrid.IsVisible = sourceIndex == (int)OnlineAssetSources.XboxComOption;
            KeywordsBox.IsVisible = sourceIndex != (int)OnlineAssetSources.ArchiveOption;
            KeywordsButton.IsVisible = sourceIndex != (int)OnlineAssetSources.ArchiveOption;
            DownloadAllButton.IsVisible = false;
            ResultBox.ItemsSource = null;
            SearchResultCount.Text = "0";
            PreviewImg.Source = null;
        }

        private void TitleIdBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (TitleIdBox != null)
            {
                TitleIdBox.Text = Regex.Replace(TitleIdBox.Text ?? "", "[^a-fA-F0-9]+", "");
            }
        }

        private void OnTitleIdBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ByTitleIdClick(sender, EventArgs.Empty);
            }
        }

        private void OnKeywordsBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ByKeywordsClick(sender, EventArgs.Empty);
            }
        }

        // ============ دوال البحث ============

        private async void ByTitleIdClick(object? sender, EventArgs e)
        {
            var titleIdText = TitleIdBox.Text;
            if (string.IsNullOrWhiteSpace(titleIdText) || titleIdText.Length != 8)
            {
                StatusMessage.Text = "Please enter a valid 8-character TitleID (hex)";
                return;
            }

            _titleId = Convert.ToUInt32(titleIdText, 16);
            _keywords = null;
            await PerformSearch(SearchType.TitleId);
        }

        private async void ByKeywordsClick(object? sender, EventArgs e)
        {
            var keywords = KeywordsBox.Text;
            if (string.IsNullOrWhiteSpace(keywords))
            {
                StatusMessage.Text = "Please enter search keywords";
                return;
            }

            _keywords = keywords;
            await PerformSearch(SearchType.Keywords);
        }

        private enum SearchType { TitleId, Keywords }

        private async Task PerformSearch(SearchType searchType)
        {
            if (_isBusy)
            {
                StatusMessage.Text = "Please wait for previous operation to complete!";
                return;
            }

            _isBusy = true;
            _main.BusyIndicator.IsVisible = true;
            PreviewImg.Source = null;
            _currentImage = null;
            ResultBox.ItemsSource = null;
            SearchResultCount.Text = "0";
            DownloadAllButton.IsVisible = false;

            var sourceIndex = SourceBox.SelectedIndex;
            StatusMessage.Text = "Downloading asset information...";

            try
            {
                await Task.Run(() =>
                {
                    switch (sourceIndex)
                    {
                        case (int)OnlineAssetSources.XboxUnityOption:
                            PerformXboxUnitySearch(searchType);
                            break;
                        case (int)OnlineAssetSources.XboxComOption:
                            PerformXboxComSearch(searchType);
                            break;
                        case (int)OnlineAssetSources.ArchiveOption:
                            PerformArchiveSearch();
                            break;
                    }
                });

                UpdateUIAfterSearch(sourceIndex);
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
                StatusMessage.Text = "An error has occurred, check error.log for more information...";
            }
            finally
            {
                _isBusy = false;
                _main.BusyIndicator.IsVisible = false;
            }
        }

        private void PerformXboxUnitySearch(SearchType searchType)
        {
            var query = searchType == SearchType.TitleId 
                ? _titleId.ToString("X08") 
                : _keywords;
            
            _unityResult = XboxUnity.GetUnityCoverInfo(query);
        }

        private void PerformXboxComSearch(SearchType searchType)
        {
            var locale = GetSelectedLocale();
            
            _xboxResult = searchType == SearchType.TitleId
                ? _xboxAssetDownloader.GetTitleInfo(_titleId, locale)
                : _xboxAssetDownloader.GetTitleInfo(_keywords, locale);
        }

        private void PerformArchiveSearch()
        {
            _archiveResult = _internetArchiveDownloader.GetTitleInfo(_titleId).GetAwaiter().GetResult();
        }

        private XboxLocale GetSelectedLocale()
        {
            var selectedLocale = LocaleBox.SelectedItem?.ToString();
            return _locales.FirstOrDefault(l => l.Locale == selectedLocale) ?? _locales.FirstOrDefault();
        }

        private void UpdateUIAfterSearch(int sourceIndex)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                switch (sourceIndex)
                {
                    case (int)OnlineAssetSources.XboxUnityOption:
                        if (_unityResult.Length > 0)
                        {
                            ResultBox.ItemsSource = _unityResult;
                            SearchResultCount.Text = _unityResult.Length.ToString();
                            StatusMessage.Text = $"Found {_unityResult.Length} results";
                        }
                        else
                        {
                            StatusMessage.Text = "No results found";
                        }
                        break;

                    case (int)OnlineAssetSources.XboxComOption:
                        if (_xboxResult.Length > 0)
                        {
                            var assets = _xboxResult.SelectMany(info => info.AssetsInfo).ToList();
                            ResultBox.ItemsSource = assets;
                            SearchResultCount.Text = assets.Count.ToString();
                            DownloadAllButton.IsVisible = assets.Count > 0;
                            StatusMessage.Text = $"Found {assets.Count} assets";
                        }
                        else
                        {
                            StatusMessage.Text = "No results found";
                        }
                        break;

                    case (int)OnlineAssetSources.ArchiveOption:
                        if (_archiveResult.Length > 0)
                        {
                            ResultBox.ItemsSource = _archiveResult;
                            SearchResultCount.Text = _archiveResult.Length.ToString();
                            StatusMessage.Text = $"Found {_archiveResult.Length} results";
                        }
                        else
                        {
                            StatusMessage.Text = "No results found";
                        }
                        break;
                }
            });
        }

        // ============ دوال اختيار النتيجة ============

        private async void ResultBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var selectedItem = ResultBox.SelectedItem;
            if (selectedItem == null)
            {
                PreviewImg.Source = null;
                _currentImage = null;
                return;
            }

            try
            {
                // معالجة نتائج XboxUnity
                if (selectedItem is XboxUnity.XboxUnityAsset unityAsset)
                {
                    await ProcessUnityAsset(unityAsset);
                    return;
                }

                // معالجة نتائج Xbox.com
                if (selectedItem is XboxTitleInfo.XboxAssetInfo xboxAsset)
                {
                    await ProcessXboxAsset(xboxAsset);
                    return;
                }

                // معالجة نتائج Internet Archive
                if (selectedItem is InternetArchiveAsset archiveAsset)
                {
                    await ProcessArchiveAsset(archiveAsset);
                    return;
                }
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
                StatusMessage.Text = "Error loading preview";
            }
        }

        private async Task ProcessUnityAsset(XboxUnity.XboxUnityAsset asset)
        {
            if (!asset.HaveAsset)
            {
                StatusMessage.Text = "Downloading asset data...";
                await Task.Run(() => asset.GetCover());
                StatusMessage.Text = "Finished downloading asset data...";
                // إعادة معالجة بعد التحميل
                ResultBox_SelectionChanged(null, null);
                return;
            }

            var cover = asset.GetCover();
            if (cover != null)
            {
                SetPreview(cover, 900, 600);
                PreviewImg.ContextMenu = new ContextMenu();
                PreviewImg.ContextMenu.ItemsSource = _coverMenu;
                _main.EditMenu.ItemsSource = _coverMenu;
            }
        }

        private async Task ProcessXboxAsset(XboxTitleInfo.XboxAssetInfo asset)
        {
            if (!asset.HaveAsset)
            {
                StatusMessage.Text = "Downloading asset data...";
                await Task.Run(() => asset.GetAsset());
                StatusMessage.Text = "Finished downloading asset data...";
                ResultBox_SelectionChanged(null, null);
                return;
            }

            var image = asset.GetAsset().Image;
            if (image != null)
            {
                switch (asset.AssetType)
                {
                    case XboxTitleInfo.XboxAssetType.Icon:
                        SetPreview(image, 64, 64);
                        PreviewImg.ContextMenu = new ContextMenu();
                        PreviewImg.ContextMenu.ItemsSource = _iconMenu;
                        _main.EditMenu.ItemsSource = _iconMenu;
                        break;
                    case XboxTitleInfo.XboxAssetType.Banner:
                        SetPreview(image, 420, 96);
                        PreviewImg.ContextMenu = new ContextMenu();
                        PreviewImg.ContextMenu.ItemsSource = _bannerMenu;
                        _main.EditMenu.ItemsSource = _bannerMenu;
                        break;
                    case XboxTitleInfo.XboxAssetType.Background:
                        SetPreview(image, 1280, 720);
                        PreviewImg.ContextMenu = new ContextMenu();
                        PreviewImg.ContextMenu.ItemsSource = _backgroundMenu;
                        _main.EditMenu.ItemsSource = _backgroundMenu;
                        break;
                    case XboxTitleInfo.XboxAssetType.Screenshot:
                        SetPreview(image, 1000, 562);
                        PreviewImg.ContextMenu = new ContextMenu();
                        PreviewImg.ContextMenu.ItemsSource = _screenshotsMenu;
                        _main.EditMenu.ItemsSource = _screenshotsMenu;
                        break;
                }
            }
        }

        private async Task ProcessArchiveAsset(InternetArchiveAsset asset)
        {
            if (!asset.HaveAsset)
            {
                StatusMessage.Text = "Downloading cover...";
                await Task.Run(() => asset.GetCover());
                StatusMessage.Text = "Finished downloading cover...";
                ResultBox_SelectionChanged(null, null);
                return;
            }

            var cover = asset.GetCover();
            if (cover != null)
            {
                SetPreview(cover, 900, 600);
                PreviewImg.ContextMenu = new ContextMenu();
                PreviewImg.ContextMenu.ItemsSource = _coverMenu;
                _main.EditMenu.ItemsSource = _coverMenu;
            }
            else
            {
                PreviewImg.Source = null;
                _currentImage = null;
                StatusMessage.Text = "Failed to load cover image.";
            }
        }

        private void SetPreview(Image<Rgba32> img, int maxWidth, int maxHeight)
        {
            _currentImage = img;
            PreviewImg.MaxHeight = maxHeight;
            PreviewBox.MaxHeight = maxHeight + 20;
            PreviewImg.MaxWidth = maxWidth;
            PreviewBox.MaxWidth = maxWidth + 20;

            using var ms = new MemoryStream();
            img.SaveAsPng(ms);
            ms.Seek(0, SeekOrigin.Begin);
            PreviewImg.Source = new Bitmap(ms);
        }

        // ============ تحميل جميع الأصول ============

        private async void DownloadAllButton_Click(object? sender, EventArgs e)
        {
            if (ResultBox.ItemsSource == null) return;

            var assets = ResultBox.ItemsSource.OfType<XboxTitleInfo.XboxAssetInfo>().ToList();
            if (!assets.Any())
            {
                StatusMessage.Text = "No assets found to download.";
                return;
            }

            _background.Reset();
            _iconBanner.Reset();
            _screenshots.Reset();

            DownloadAllButton.IsEnabled = false;
            StatusMessage.Text = "Downloading all assets...";
            _main.BusyIndicator.IsVisible = true;

            await Task.Run(() =>
            {
                int max_ss = 3;
                int current_ss = 0;
                int current = 0;
                int total = assets.Count;

                foreach (var asset in assets)
                {
                    try
                    {
                        if (!asset.HaveAsset)
                        {
                            if (asset.AssetType == XboxTitleInfo.XboxAssetType.Screenshot)
                            {
                                if (current_ss >= max_ss) continue;
                                current_ss++;
                            }
                            asset.GetAsset();
                        }

                        var image = asset.GetAsset().Image;
                        current++;

                        Dispatcher.UIThread.Invoke(() =>
                        {
                            ApplyAsset(asset.AssetType, image);
                            StatusMessage.Text = $"Downloaded and applied {asset.AssetType} ({current}/{total})";
                        });
                    }
                    catch (Exception ex)
                    {
                        MainWindow.SaveError(ex);
                    }
                }
            });

            _main.BusyIndicator.IsVisible = false;
            DownloadAllButton.IsEnabled = true;
            StatusMessage.Text = "Finished downloading and applying all assets.";
        }

        private void ApplyAsset(XboxTitleInfo.XboxAssetType type, Image<Rgba32> image)
        {
            if (image == null) return;

            switch (type)
            {
                case XboxTitleInfo.XboxAssetType.Background:
                    _background.Load(image);
                    break;
                case XboxTitleInfo.XboxAssetType.Icon:
                    _iconBanner.Load(image, true);
                    break;
                case XboxTitleInfo.XboxAssetType.Banner:
                    _iconBanner.Load(image, false);
                    break;
                case XboxTitleInfo.XboxAssetType.Screenshot:
                    _screenshots.Load(image, false);
                    break;
            }
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
    }
}
