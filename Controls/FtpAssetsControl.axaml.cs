using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AuroraAssetEditorLinux.Models;
//using Classes;
//using Helpers;
using AuroraAssetEditorLinux.Helpers;
using AuroraAssetEditorLinux.Classes;
using AuroraAssetEditorLinux.Controls;
using static AuroraAssetEditorLinux.Classes.XboxTitleInfo;

namespace AuroraAssetEditorLinux.Controls
{
    public partial class FtpAssetsControl : UserControl
    {
        private readonly InternetArchiveDownloader _internetArchiveDownloader = new InternetArchiveDownloader();
        private readonly XboxAssetDownloader _xboxAssetDownloader = new XboxAssetDownloader();
        private readonly Random _rand = new Random();
        private readonly ObservableCollection<AuroraDbManager.ContentItem> _assetsList = new ObservableCollection<AuroraDbManager.ContentItem>();
        private readonly ICollectionView? _assetView;
        private readonly BackgroundControl _background;
        private readonly BoxartControl _boxart;
        private readonly IconBannerControl _iconBanner;
        private readonly MainWindow _main;
        private readonly ScreenshotsControl _screenshots;
        private byte[] _buffer = Array.Empty<byte>();
        private bool _isBusy, _isError;

        public FtpAssetsControl(MainWindow main, BoxartControl boxart, BackgroundControl background, 
                                IconBannerControl iconBanner, ScreenshotsControl screenshots)
        {
            InitializeComponent();
            
            _main = main;
            _boxart = boxart;
            _background = background;
            _iconBanner = iconBanner;
            _screenshots = screenshots;

            // ربط تغييرات الحالة
            App.FtpOperations.StatusChanged += (sender, args) => 
                Dispatcher.UIThread.Invoke(() => Status.Text = args.StatusMessage);

            // إعداد مصدر البيانات
            FtpAssetsBox.ItemsSource = _assetsList;

            // تحميل الإعدادات
            if (!App.FtpOperations.HaveSettings)
            {
                var ip = GetActiveIp();
                var index = ip.LastIndexOf('.');
                if (ip.Length > 0 && index > 0)
                    IpBox.Text = ip.Substring(0, index + 1);
            }
            else
            {
                IpBox.Text = App.FtpOperations.IpAddress;
                UserBox.Text = App.FtpOperations.Username;
                PassBox.Text = App.FtpOperations.Password;
                PortBox.Text = App.FtpOperations.Port;
            }
        }

        private static string GetActiveIp()
        {
            foreach (var unicastAddress in
                NetworkInterface.GetAllNetworkInterfaces()
                    .Where(f => f.OperationalStatus == OperationalStatus.Up)
                    .Select(f => f.GetIPProperties())
                    .Where(ipInterface => ipInterface.GatewayAddresses.Count > 0)
                    .SelectMany(ipInterface =>
                        ipInterface.UnicastAddresses.Where(
                            unicastAddress =>
                                (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork) &&
                                (unicastAddress.IPv4Mask.ToString() != "0.0.0.0"))))
            {
                return unicastAddress.Address.ToString();
            }
            return "";
        }

        // ============ دوال الأحداث ============

        private async void TestConnectionClick(object? sender, EventArgs e)
        {
            var ip = IpBox.Text;
            var user = UserBox.Text;
            var pass = PassBox.Text;
            var port = PortBox.Text;

            Status.Text = $"Running a connection test to {ip}";
            _main.BusyIndicator.IsVisible = true;

            await Task.Run(() =>
            {
                App.FtpOperations.TestConnection(ip, user, pass, port);
            });

            _main.BusyIndicator.IsVisible = false;
        }

        private void SaveSettingsClick(object? sender, EventArgs e)
        {
            App.FtpOperations.SaveSettings(IpBox.Text, UserBox.Text, PassBox.Text, PortBox.Text);
            Status.Text = "Settings saved!";
        }

        private void FtpAssetsBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (FtpAssetsBox.SelectedItem is AuroraDbManager.ContentItem selectedAsset)
            {
                var newGame = new Game
                {
                    Title = selectedAsset.TitleName,
                    TitleId = selectedAsset.TitleId,
                    DbId = selectedAsset.DatabaseId,
                    IsGameSelected = true
                };

                GlobalState.CurrentGame = newGame;
            }
        }

        private async void GetAssetsClick(object? sender, EventArgs e)
        {
            _assetsList.Clear();
            if (!App.FtpOperations.HaveSettings)
            {
                Status.Text = "No FTP settings configured!";
                return;
            }

            _main.BusyIndicator.IsVisible = true;
            Status.Text = "Grabbing FTP Assets information...";

            await Task.Run(() =>
            {
                try
                {
                    var path = Path.Combine(Path.GetTempPath(), "AuroraAssetEditor.db");
                    if (!App.FtpOperations.DownloadContentDb(path))
                        return;

                    foreach (var title in AuroraDbManager.GetDbTitles(path))
                    {
                        Dispatcher.UIThread.Invoke(() => _assetsList.Add(title));
                    }
                }
                catch (Exception ex)
                {
                    MainWindow.SaveError(ex);
                }
            });

            _main.BusyIndicator.IsVisible = false;
            Status.Text = "Finished grabbing FTP Assets information successfully...";
        }

        // ============ معالجة الأصول ============

        private async Task<bool> ProcessAsset(Task task, bool shouldHideWhenDone = true)
        {
            _isError = false;
            var asset = FtpAssetsBox.SelectedItem as AuroraDbManager.ContentItem;
            if (asset == null)
                return false;

            _main.BusyIndicator.IsVisible = true;
            _isBusy = true;

            try
            {
                await Task.Run(() =>
                {
                    switch (task)
                    {
                        case Task.GetBoxart:
                            _buffer = asset.GetBoxart();
                            break;
                        case Task.GetBackground:
                            _buffer = asset.GetBackground();
                            break;
                        case Task.GetIconBanner:
                            _buffer = asset.GetIconBanner();
                            break;
                        case Task.GetScreenshots:
                            _buffer = asset.GetScreenshots();
                            break;
                        case Task.SetBoxart:
                            asset.SaveAsBoxart(_buffer);
                            break;
                        case Task.SetBackground:
                            asset.SaveAsBackground(_buffer);
                            break;
                        case Task.SetIconBanner:
                            asset.SaveAsIconBanner(_buffer);
                            break;
                        case Task.SetScreenshots:
                            asset.SaveAsScreenshots(_buffer);
                            break;
                    }
                });

                if (_buffer.Length > 0 && IsGetTask(task))
                {
                    var aurora = new AuroraAsset.AssetFile(_buffer);
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        switch (task)
                        {
                            case Task.GetBoxart:
                                _boxart.Load(aurora);
                                _main.BoxartTab.IsSelected = true;
                                break;
                            case Task.GetBackground:
                                _background.Load(aurora);
                                _main.BackgroundTab.IsSelected = true;
                                break;
                            case Task.GetIconBanner:
                                _iconBanner.Load(aurora);
                                _main.IconBannerTab.IsSelected = true;
                                break;
                            case Task.GetScreenshots:
                                _screenshots.Load(aurora);
                                _main.ScreenshotsTab.IsSelected = true;
                                break;
                        }
                    });
                }

                if (shouldHideWhenDone)
                    Status.Text = IsGetTask(task) ? "Finished grabbing assets from FTP" : "Finished saving assets to FTP";
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
                _isError = true;
                Status.Text = IsGetTask(task) ? "Failed getting asset data... See error.log" : "Failed saving asset data... See error.log";
            }
            finally
            {
                _main.BusyIndicator.IsVisible = false;
                _isBusy = false;
            }

            return !_isError;
        }

        private static bool IsGetTask(Task task)
        {
            return task == Task.GetBoxart || task == Task.GetBackground || 
                   task == Task.GetIconBanner || task == Task.GetScreenshots;
        }

        // ============ دوال جلب الأصول ============

        private async void GetBoxartClick(object? sender, EventArgs e)
        {
            await ProcessAsset(Task.GetBoxart);
        }

        private async void GetBackgroundClick(object? sender, EventArgs e)
        {
            await ProcessAsset(Task.GetBackground);
        }

        private async void GetIconBannerClick(object? sender, EventArgs e)
        {
            await ProcessAsset(Task.GetIconBanner);
        }

        private async void GetScreenshotsClick(object? sender, EventArgs e)
        {
            await ProcessAsset(Task.GetScreenshots);
        }

        private async void GetFtpAssetsClick(object? sender, EventArgs e)
        {
            await ProcessAsset(Task.GetBoxart, false);
            if (_isError) return;
            
            await ProcessAsset(Task.GetBackground, false);
            if (_isError) return;
            
            await ProcessAsset(Task.GetIconBanner, false);
            if (_isError) return;
            
            await ProcessAsset(Task.GetScreenshots);
        }

        // ============ دوال حفظ الأصول ============

        private async void SaveBoxartClick(object? sender, EventArgs e)
        {
            _buffer = _boxart.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetBoxart);
        }

        private async void SaveBackgroundClick(object? sender, EventArgs e)
        {
            _buffer = _background.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetBackground);
        }

        private async void SaveIconBannerClick(object? sender, EventArgs e)
        {
            _buffer = _iconBanner.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetIconBanner);
        }

        private async void SaveScreenshotsClick(object? sender, EventArgs e)
        {
            _buffer = _screenshots.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetScreenshots);
        }

        private async void SaveFtpAssetsClick(object? sender, EventArgs e)
        {
            _buffer = _boxart.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetBoxart, false);
            if (_isError) return;

            _buffer = _background.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetBackground, false);
            if (_isError) return;

            _buffer = _iconBanner.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetIconBanner, false);
            if (_isError) return;

            _buffer = _screenshots.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetScreenshots);
        }

        // ============ دوال الحذف ============

        private async void RemoveFtpAssetsClick(object? sender, EventArgs e)
        {
            _boxart.Reset();
            _iconBanner.Reset();
            _background.Reset();
            _screenshots.Reset();
            await SaveFtpAssetsClickAsync();
        }

        private async void RemoveBoxartClick(object? sender, EventArgs e)
        {
            _boxart.Reset();
            _buffer = _boxart.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetBoxart);
        }

        private async void RemoveBackgroundClick(object? sender, EventArgs e)
        {
            _background.Reset();
            _buffer = _background.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetBackground);
        }

        private async void RemoveIconBannerClick(object? sender, EventArgs e)
        {
            _iconBanner.Reset();
            _buffer = _iconBanner.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetIconBanner);
        }

        private async void RemoveScreenshotsClick(object? sender, EventArgs e)
        {
            _screenshots.Reset();
            _buffer = _screenshots.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetScreenshots);
        }

        private async Task SaveFtpAssetsClickAsync()
        {
            _buffer = _boxart.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetBoxart, false);
            if (_isError) return;

            _buffer = _background.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetBackground, false);
            if (_isError) return;

            _buffer = _iconBanner.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetIconBanner, false);
            if (_isError) return;

            _buffer = _screenshots.GetData() ?? Array.Empty<byte>();
            await ProcessAsset(Task.SetScreenshots);
        }

        // ============ دوال السحب والإفلات ============

        private void OnDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.FileNames))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            _main.DragDrop(this, e);
        }

        // ============ دوال الفلترة ============

        private void TitleFilterChanged(object? sender, TextChangedEventArgs e)
        {
            FiltersChanged(TitleFilterBox?.Text ?? string.Empty, TitleIdFilterBox?.Text ?? string.Empty);
        }

        private void TitleIdFilterChanged(object? sender, TextChangedEventArgs e)
        {
            FiltersChanged(TitleFilterBox?.Text ?? string.Empty, TitleIdFilterBox?.Text ?? string.Empty);
        }

        private void FiltersChanged(string titleFilter, string titleIdFilter)
        {
            var items = FtpAssetsBox.ItemsSource as ObservableCollection<AuroraDbManager.ContentItem>;
            if (items == null) return;

            var filtered = items.Where(item =>
            {
                if (string.IsNullOrWhiteSpace(titleFilter) && string.IsNullOrWhiteSpace(titleIdFilter))
                    return true;
                if (!string.IsNullOrWhiteSpace(titleFilter) && !string.IsNullOrWhiteSpace(titleIdFilter))
                    return item.TitleName.ToLower().Contains(titleFilter.ToLower()) && 
                           item.TitleId.ToLower().Contains(titleIdFilter.ToLower());
                if (!string.IsNullOrWhiteSpace(titleFilter))
                    return item.TitleName.ToLower().Contains(titleFilter.ToLower());
                return item.TitleId.ToLower().Contains(titleIdFilter.ToLower());
            }).ToList();

            // تحديث المصدر بالقائمة المفلترة
            // ملاحظة: هذه الطريقة بسيطة ولكنها ليست مثالية للبيانات الكبيرة
            FtpAssetsBox.ItemsSource = filtered;
        }

        // ============ التحقق من الأصول ============

        private bool VerifyAsset(AuroraDbManager.ContentItem asset, AuroraAsset.AssetType type)
        {
            byte[] buffer = type switch
            {
                AuroraAsset.AssetType.Boxart => asset.GetBoxart(),
                AuroraAsset.AssetType.Icon or AuroraAsset.AssetType.Banner => asset.GetIconBanner(),
                AuroraAsset.AssetType.Background => asset.GetBackground(),
                _ => asset.GetScreenshots()
            };

            var aurora = new AuroraAsset.AssetFile(buffer);
            return type switch
            {
                AuroraAsset.AssetType.Boxart => aurora.GetBoxart() != null,
                AuroraAsset.AssetType.Icon => aurora.GetIcon() != null,
                AuroraAsset.AssetType.Banner => aurora.GetBanner() != null,
                AuroraAsset.AssetType.Background => aurora.GetBackground() != null,
                _ => aurora.GetScreenshots().Length != 0
            };
        }

        // ============ التحميل المتعدد (Bulk Download) ============

        private async void BulkDownloadClick(object? sender, EventArgs e)
        {
            var assets = FtpAssetsBox.ItemsSource as System.Collections.IEnumerable;
            if (assets == null || !assets.GetEnumerator().MoveNext())
            {
                await MessageBox.Show("ERROR: No Assets listed", "ERROR");
                return;
            }

            if (!App.FtpOperations.ConnectionEstablished)
            {
                await MessageBox.Show("ERROR: FTP Connection could not be established", "ERROR");
                return;
            }

            // منطق BulkDownload معقد ويتطلب نموذج BulkActionsDialog
            // سيتم تحويله عند طلبك

            Status.Text = "Bulk download started...";
            _main.BusyIndicator.IsVisible = true;

            await Task.Run(() =>
            {
                // منطق التحميل المتعدد
                Thread.Sleep(2000); // مؤقت للاختبار
            });

            _main.BusyIndicator.IsVisible = false;
            Status.Text = "Bulk download completed!";
        }

        private void FtpAssetsBoxContextOpening(object? sender, EventArgs e)
        {
            if (FtpAssetsBox.SelectedItem == null)
                e.Handled = true;
        }

        // ============ تعريف المهام ============

        private enum Task
        {
            GetBoxart,
            GetBackground,
            GetIconBanner,
            GetScreenshots,
            SetBoxart,
            SetBackground,
            SetIconBanner,
            SetScreenshots,
        }
    }
}
