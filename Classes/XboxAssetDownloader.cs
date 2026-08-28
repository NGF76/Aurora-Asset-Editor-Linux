using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace AuroraAssetEditorLinux.Classes
{
    internal class XboxAssetDownloader
    {
        public static EventHandler<StatusArgs>? StatusChanged;
        private readonly DataContractJsonSerializer _serializer = new DataContractJsonSerializer(typeof(XboxKeywordResponse));

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
                    ErrorLogger.LogError(ex, "XboxAssetDownloader.SendStatusChanged");
                }
            }
        }

        // ============ دوال جلب اللغات (جديدة) ============

        /// <summary>
        /// يجلب قائمة اللغات من موقع Xbox مباشرة
        /// </summary>
        public static async Task<XboxLocale[]> GetLocalesAsync()
        {
            try
            {
                var locales = await FetchLocalesFromXboxAsync();
                return locales.OrderBy(l => l.ToString()).ToArray();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, "XboxAssetDownloader.GetLocalesAsync");
                // في حالة الفشل، استخدم القائمة الثابتة كنسخة احتياطية
                return GetDefaultLocales();
            }
        }

        /// <summary>
        /// يجلب قائمة اللغات من ملف JavaScript الخاص بـ Xbox
        /// </summary>
        private static async Task<List<XboxLocale>> FetchLocalesFromXboxAsync()
        {
            var locales = new List<XboxLocale>();
            var url = "https://assets-www.xbox.com/xbox-web/static/js/LocalePickerPage.7c45fcf5.chunk.js";

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            var jsContent = await client.GetStringAsync(url);
            return ParseLocales(jsContent);
        }

        /// <summary>
        /// يستخرج اللغات من محتوى JavaScript
        /// </summary>
        private static List<XboxLocale> ParseLocales(string jsContent)
        {
            var locales = new List<XboxLocale>();

            // النمط: {title:"...",locale:...}
            var pattern = @"\{title:""([^""]+)"",locale:([^}]+)\}";
            var matches = Regex.Matches(jsContent, pattern);

            foreach (Match match in matches)
            {
                if (match.Groups.Count < 3) continue;

                var localeName = match.Groups[1].Value;
                var localeVar = match.Groups[2].Value;

                // استخراج الكود من المتغير (مثل: a.esAR -> es-AR)
                var matchCode = Regex.Match(localeVar, @"[a-z]+\.([a-zA-Z]+)");
                if (!matchCode.Success) continue;

                var code = matchCode.Groups[1].Value;
                if (code.Length < 4) continue;

                var localeId = $"{code.Substring(0, 2)}-{code.Substring(2)}";
                locales.Add(new XboxLocale(localeId, localeName));
            }

            return locales;
        }

        /// <summary>
        /// القائمة الثابتة كنسخة احتياطية في حالة فشل جلب اللغات
        /// </summary>
        private static XboxLocale[] GetDefaultLocales()
        {
            return new XboxLocale[]
            {
                new XboxLocale("en-US", "United States - English"),
                new XboxLocale("es-ES", "España - Español"),
                new XboxLocale("fr-FR", "France - Français"),
                new XboxLocale("de-DE", "Deutschland - Deutsch"),
                new XboxLocale("it-IT", "Italia - Italiano"),
                new XboxLocale("ja-JP", "日本 - 日本語"),
                new XboxLocale("ko-KR", "대한민국 - 한국어"),
                new XboxLocale("pt-BR", "Brasil - Português"),
                new XboxLocale("ru-RU", "Россия - Русский"),
                new XboxLocale("zh-CN", "中国 - 中文"),
                new XboxLocale("ar-SA", "المملكة العربية السعودية - العربية")
            };
        }

        // ============ الدوال الأساسية ============

        public XboxTitleInfo[] GetTitleInfo(uint titleId, XboxLocale locale)
        {
            return new[] { XboxTitleInfo.FromTitleId(titleId, locale) };
        }

        public XboxTitleInfo[] GetTitleInfo(string keywords, XboxLocale locale)
        {
            var url = $"http://marketplace.xbox.com/{locale.Locale}/SiteSearch/xbox/?query={WebUtility.UrlEncode(keywords)}&PageSize=5";
            var ret = new List<XboxTitleInfo>();

            try
            {
                using var wc = new WebClient();
                using var stream = wc.OpenRead(url);
                if (stream == null)
                    return ret.ToArray();

                var res = (XboxKeywordResponse?)_serializer.ReadObject(stream);
                if (res?.Entries == null)
                    return ret.ToArray();

                foreach (var entry in res.Entries)
                {
                    if (string.IsNullOrEmpty(entry.DetailsUrl))
                        continue;

                    var tid = entry.DetailsUrl.IndexOf("d802", StringComparison.Ordinal);
                    if (tid > 0 && entry.DetailsUrl.Length >= tid + 12)
                    {
                        try
                        {
                            var titleIdStr = entry.DetailsUrl.Substring(tid + 4, 8);
                            var titleId = uint.Parse(titleIdStr, NumberStyles.HexNumber);
                            ret.Add(XboxTitleInfo.FromTitleId(titleId, locale));
                        }
                        catch
                        {
                            // تجاهل الأخطاء في تحويل TitleId
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendStatusChanged($"Error searching keywords: {ex.Message}");
                ErrorLogger.LogError(ex, "XboxAssetDownloader.GetTitleInfo");
            }

            return ret.ToArray();
        }

        // ============ الدوال القديمة (للتوافق مع الكود الحالي) ============

        /// <summary>
        /// الدالة القديمة للتوافق مع الكود الحالي
        /// يُفضل استخدام GetLocalesAsync() بدلاً منها
        /// </summary>
        public static XboxLocale[] GetLocales()
        {
            try
            {
                // محاولة جلب اللغات بشكل متزامن (غير موصى به)
                // في الإصدارات الحديثة، استخدم GetLocalesAsync()
                return GetDefaultLocales();
            }
            catch
            {
                return Array.Empty<XboxLocale>();
            }
        }
    }

    // ============ باقي الكود (XboxTitleInfo, XboxLocale, XboxKeywordResponse) ============
    // ... (كما هو موجود في ملفك) ...
}
