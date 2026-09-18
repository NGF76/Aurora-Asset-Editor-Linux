using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using AuroraAssetEditorLinux.Classes;
using AuroraAssetEditorLinux.Controls;
using AuroraAssetEditorLinux.Dialogs;
using AuroraAssetEditorLinux.Helpers;
using AuroraAssetEditorLinux.Models;
using FluentFTP;

namespace AuroraAssetEditorLinux.Classes
{
    internal static class XboxUnity
    {
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(UnityResponse[]));

        private static string GetUnityUrl(string searchTerm)
        {
            return $"http://xboxunity.net/api/Covers/{WebUtility.UrlEncode(searchTerm)}";
        }

        public static XboxUnityAsset[] GetUnityCoverInfo(string searchTerm)
        {
            try
            {
                using var wc = new WebClient();
                using var stream = wc.OpenRead(GetUnityUrl(searchTerm));
                if (stream == null)
                    return Array.Empty<XboxUnityAsset>();

                var responses = (UnityResponse[]?)Serializer.ReadObject(stream);
                if (responses == null)
                    return Array.Empty<XboxUnityAsset>();

                return responses.Select(t => new XboxUnityAsset(t)).ToArray();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, $"XboxUnity.GetUnityCoverInfo({searchTerm})");
                return Array.Empty<XboxUnityAsset>();
            }
        }

        public static async Task<string> GetHomebrewTitleFromFtp(string path)
        {
            try
            {
            var data = await App.FtpOperations.GetAssetData("GameCoverInfo.bin", path);
                if (data == null || data.Length < 10)
                    return "N/A";

                using var ms = new MemoryStream(data);
                var response = (UnityResponse[]?)Serializer.ReadObject(ms);
                if (response == null || response.Length == 0)
                    return "N/A";

                return response[0].Name ?? "N/A";
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, $"XboxUnity.GetHomebrewTitleFromFtp({path})");
                return "N/A";
            }
        }

        [DataContract]
        internal class UnityResponse
        {
            [DataMember(Name = "titleid")]
            public string? TitleId { get; set; }

            [DataMember(Name = "name")]
            public string? Name { get; set; }

            [DataMember(Name = "official")]
            public bool Official { get; set; }

            [DataMember(Name = "filesize")]
            public string? FileSize { get; set; }

            [DataMember(Name = "url")]
            public string? Url { get; set; }

            [DataMember(Name = "front")]
            public string? Front { get; set; }

            [DataMember(Name = "thumbnail")]
            public string? Thumbnail { get; set; }

            [DataMember(Name = "author")]
            public string? Author { get; set; }

            [DataMember(Name = "uploaddate")]
            public string? UploadDate { get; set; }

            [DataMember(Name = "rating")]
            public string? Rating { get; set; }

            [DataMember(Name = "link")]
            public string? Link { get; set; }
        }

        public class XboxUnityAsset
        {
            private readonly UnityResponse _unityResponse;
            private Image<Rgba32>? _cover;

            public XboxUnityAsset(UnityResponse response)
            {
                _unityResponse = response;
            }

            public bool HaveAsset => _cover != null;

            public string? Title => _unityResponse.Name;

            private static Image<Rgba32>? GetImage(string? url)
            {
                if (string.IsNullOrEmpty(url))
                    return null;

                try
                {
                    using var wc = new WebClient();
                    var data = wc.DownloadData(url);
                    using var ms = new MemoryStream(data);
                    return Image.Load<Rgba32>(ms);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError(ex, $"XboxUnityAsset.GetImage({url})");
                    return null;
                }
            }

            public Image<Rgba32>? GetCover()
            {
                if (_cover != null)
                    return _cover;

                _cover = GetImage(_unityResponse.Url);
                return _cover;
            }

            public override string ToString()
            {
                var name = _unityResponse.Name ?? "Unknown";
                var rating = _unityResponse.Rating ?? "N/A";
                return _unityResponse.Official
                    ? $"Official cover for {name} Rating: {rating}"
                    : $"Cover for {name} Rating: {rating}";
            }
        }
    }
}
