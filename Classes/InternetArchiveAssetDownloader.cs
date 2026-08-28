using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AuroraAssetEditorLinux.Classes
{
    internal class InternetArchiveDownloader
    {
        private const string BaseUrl = "https://archive.org/download/xboxunity-covers-fulldump_202311/xboxunity-covers-fulldump/";
        public static EventHandler<StatusArgs>? StatusChanged;

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        internal static void SendStatusChanged(string msg)
        {
            var handler = StatusChanged;
            if (handler != null)
            {
                try
                {
                    handler.Invoke(null, new StatusArgs(msg));
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError(ex, "InternetArchiveDownloader.SendStatusChanged");
                }
            }
        }

        public async Task<InternetArchiveAsset[]> GetTitleInfo(uint titleId)
        {
            string titleFolder = $"{titleId:X08}";
            var assets = new List<InternetArchiveAsset>();

            try
            {
                string folderUrl = $"{BaseUrl}{titleFolder}/";
                SendStatusChanged($"Fetching directory: {folderUrl}");

                string htmlContent = await _httpClient.GetStringAsync(folderUrl);
                
                foreach (string subDir in ParseDirectoriesFromHtmlRegex(htmlContent))
                {
                    assets.Add(new InternetArchiveAsset
                    {
                        TitleId = titleId,
                        MainFolder = titleFolder,
                        SubFolder = subDir,
                        AssetType = "Cover"
                    });
                }

                SendStatusChanged($"Found {assets.Count} asset(s) for TitleID {titleFolder}");
            }
            catch (HttpRequestException ex)
            {
                SendStatusChanged($"Network error fetching directory: {ex.Message}");
                ErrorLogger.LogError(ex, $"InternetArchiveDownloader.GetTitleInfo({titleFolder})");
            }
            catch (Exception ex)
            {
                SendStatusChanged($"Error fetching directory: {ex.Message}");
                ErrorLogger.LogError(ex, $"InternetArchiveDownloader.GetTitleInfo({titleFolder})");
            }

            return assets.ToArray();
        }

        private IEnumerable<string> ParseDirectoriesFromHtmlRegex(string html)
        {
            const string HREF_REGEX = "<a href=\"(.+)/\">(.+)/</a>";
            
            foreach (Match match in Regex.Matches(html, HREF_REGEX))
            {
                var hrefValue = match.Groups[1].Value;
                var text = match.Groups[2].Value;

                // التحقق من أن الرابط والنص متطابقان (تجاهل "../")
                if (hrefValue == text && !hrefValue.StartsWith(".."))
                {
                    yield return hrefValue;
                }
            }
        }

        public async Task<Image<Rgba32>?> DownloadCover(InternetArchiveAsset asset)
        {
            try
            {
                string coverUrl = $"{BaseUrl}{asset.MainFolder}/{asset.SubFolder}/boxart.png";
                SendStatusChanged($"Downloading cover: {coverUrl}");

                using var response = await _httpClient.GetAsync(coverUrl);
                if (!response.IsSuccessStatusCode)
                {
                    SendStatusChanged($"Failed to download cover: {response.StatusCode}");
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                
                try
                {
                    // تحميل الصورة باستخدام ImageSharp
                    var image = await Image.LoadAsync<Rgba32>(stream);
                    SendStatusChanged($"Cover downloaded successfully ({image.Width}x{image.Height})");
                    return image;
                }
                catch (Exception ex)
                {
                    SendStatusChanged($"Invalid image data received: {ex.Message}");
                    ErrorLogger.LogError(ex, "InternetArchiveDownloader.DownloadCover");
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                SendStatusChanged($"Network error downloading cover: {ex.Message}");
                ErrorLogger.LogError(ex, "InternetArchiveDownloader.DownloadCover");
                return null;
            }
            catch (Exception ex)
            {
                SendStatusChanged($"Error downloading cover: {ex.Message}");
                ErrorLogger.LogError(ex, "InternetArchiveDownloader.DownloadCover");
                return null;
            }
        }

        // دالة متوافقة مع الكود القديم (غير متزامنة)
        public Image<Rgba32>? DownloadCoverSync(InternetArchiveAsset asset)
        {
            try
            {
                string coverUrl = $"{BaseUrl}{asset.MainFolder}/{asset.SubFolder}/boxart.png";
                SendStatusChanged($"Downloading cover: {coverUrl}");

                using var client = new WebClient();
                byte[] imageData = client.DownloadData(coverUrl);

                if (imageData == null || imageData.Length == 0)
                {
                    SendStatusChanged("No image data received");
                    return null;
                }

                using var ms = new MemoryStream(imageData);
                var image = Image.Load<Rgba32>(ms);
                SendStatusChanged($"Cover downloaded successfully ({image.Width}x{image.Height})");
                return image;
            }
            catch (WebException ex)
            {
                SendStatusChanged($"Network error downloading cover: {ex.Message}");
                ErrorLogger.LogError(ex, "InternetArchiveDownloader.DownloadCoverSync");
                return null;
            }
            catch (Exception ex)
            {
                SendStatusChanged($"Error downloading cover: {ex.Message}");
                ErrorLogger.LogError(ex, "InternetArchiveDownloader.DownloadCoverSync");
                return null;
            }
        }
    }

    // فئة الأصول المعدلة
    public class InternetArchiveAsset
    {
        private Image<Rgba32>? _cover;

        public uint TitleId { get; set; }
        public string MainFolder { get; set; } = string.Empty;  // مجلد Title ID
        public string SubFolder { get; set; } = string.Empty;   // المجلد الفرعي الذي يحتوي على الغلاف
        public string AssetType { get; set; } = string.Empty;
        public bool HaveAsset => _cover != null;

        public async Task<Image<Rgba32>?> GetCoverAsync()
        {
            if (_cover == null)
            {
                var downloader = new InternetArchiveDownloader();
                _cover = await downloader.DownloadCover(this);
            }
            return _cover;
        }

        public Image<Rgba32>? GetCover()
        {
            if (_cover == null)
            {
                var downloader = new InternetArchiveDownloader();
                _cover = downloader.DownloadCoverSync(this);
            }
            return _cover;
        }

        public override string ToString()
        {
            return $"TitleID: {TitleId:X8} - Variant: {SubFolder}";
        }
    }

    // فئة StatusArgs (إذا لم تكن موجودة بالفعل)
    public class StatusArgs : EventArgs
    {
        public StatusArgs(string statusMessage)
        {
            StatusMessage = statusMessage;
        }

        public string StatusMessage { get; }
    }
}
