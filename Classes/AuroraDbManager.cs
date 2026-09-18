using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace AuroraAssetEditorLinux.Classes
{
    internal static class AuroraDbManager
    {
        private static SQLiteConnection? _content;

        private static void ConnectToContent(string path)
        {
            _content?.Close();
            _content = new SQLiteConnection($"Data Source=\"{path}\";Version=3;");
            _content.Open();
        }

        private static DataTable? GetContentDataTable(string sql)
        {
            var dt = new DataTable();
            try
            {
                if (_content == null) return null;
                
                using var cmd = new SQLiteCommand(sql, _content);
                using var reader = cmd.ExecuteReader();
                dt.Load(reader);
                return dt;
            }
            catch (Exception ex)
            {
                // استخدام دالة حفظ الأخطاء المعدلة
                ErrorLogger.LogError(ex);
                return null;
            }
        }

        public static IEnumerable<ContentItem> GetDbTitles(string path)
        {
            ConnectToContent(path);
            var ret = GetContentItems()?.ToList() ?? new List<ContentItem>();
            _content?.Close();
            
            // تنظيف الذاكرة
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            // محاولة حذف الملف المؤقت
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // انتظار ثم المحاولة مرة أخرى
                Thread.Sleep(100);
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch
                {
                    // تجاهل إذا لم نتمكن من الحذف
                }
            }
            
            return ret;
        }

        private static IEnumerable<ContentItem>? GetContentItems()
        {
            var table = GetContentDataTable("SELECT * FROM ContentItems");
            if (table == null) return null;
            
            return table
                .AsEnumerable()
                .Select(row => new ContentItem(row))
                .ToArray();
        }

        // ============ فئة ContentItem ============

        internal class ContentItem
        {
            public ContentItem(DataRow row)
            {
                DatabaseId = ((int)((long)row["Id"])).ToString("X08");
                TitleId = ((int)((long)row["TitleId"])).ToString("X08");
                MediaId = ((int)((long)row["MediaId"])).ToString("X08");
                
                var discNum = (int)((long)row["DiscNum"]);
                if (discNum <= 0)
                    discNum = 1;
                DiscNum = discNum.ToString(CultureInfo.InvariantCulture);
                TitleName = (string)row["TitleName"];
            }

            public string TitleId { get; private set; } = string.Empty;
            public string MediaId { get; private set; } = string.Empty;
            public string DiscNum { get; private set; } = string.Empty;
            public string TitleName { get; private set; } = string.Empty;
            public string DatabaseId { get; private set; } = string.Empty;
            
            public string Path => $"{TitleId}_{DatabaseId}";

            // ========== دوال FTP ==========

            public void SaveAsBoxart(byte[] data)
            {
                App.FtpOperations.SendAssetData($"GC{TitleId}.asset", Path, data);
            }

            public void SaveAsBackground(byte[] data)
            {
                App.FtpOperations.SendAssetData($"BK{TitleId}.asset", Path, data);
            }

            public void SaveAsIconBanner(byte[] data)
            {
                App.FtpOperations.SendAssetData($"GL{TitleId}.asset", Path, data);
            }

            public void SaveAsScreenshots(byte[] data)
            {
                App.FtpOperations.SendAssetData($"SS{TitleId}.asset", Path, data);
            }

            public async Task<byte[]?> GetBoxart()
            {
                return await App.FtpOperations.GetAssetData($"GC{TitleId}.asset", Path);
            }

            public async Task<byte[]?> GetBackground()
            {
                return await App.FtpOperations.GetAssetData($"BK{TitleId}.asset", Path);
            }

            public async Task<byte[]?> GetIconBanner()
            {
                return await App.FtpOperations.GetAssetData($"GL{TitleId}.asset", Path);
            }

            public async Task<byte[]?> GetScreenshots()
            {
                return await App.FtpOperations.GetAssetData($"SS{TitleId}.asset", Path);
            }
        }
    }

    // ============ فئة مساعدة لتسجيل الأخطاء ============

    public static class ErrorLogger
    {
        private static readonly object _lock = new object();

        public static void LogError(Exception ex, string? additionalInfo = null)
        {
            try
            {
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]";
                if (!string.IsNullOrEmpty(additionalInfo))
                    logMessage += $" [{additionalInfo}]";
                logMessage += $": {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}";

                lock (_lock)
                {
                    File.AppendAllText("error.log", logMessage);
                }
            }
            catch
            {
                // تجاهل أخطاء التسجيل
            }
        }

        public static void LogMessage(string message)
        {
            try
            {
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                lock (_lock)
                {
                    File.AppendAllText("app.log", logMessage);
                }
            }
            catch
            {
                // تجاهل أخطاء التسجيل
            }
        }
    }
}
