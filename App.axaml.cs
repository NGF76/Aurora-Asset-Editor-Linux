using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AuroraAssetEditorLinux.Classes;
using AuroraAssetEditorLinux.Controls;
using AuroraAssetEditorLinux.Models;
using AuroraAssetEditorLinux.Helpers;
//using Classes;

namespace AuroraAssetEditorLinux
{
    public partial class App : Application
    {
        internal static readonly FtpOperations FtpOperations = new FtpOperations();
        internal static Bitmap? AppIcon;
        internal static string[] StartupArgs = Array.Empty<string>();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // محاولة تحميل الأيقونة من الموارد المضمنة
            try
            {
                AppIcon = LoadIconFromResources();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ لا يمكن تحميل الأيقونة: {ex.Message}");
                ErrorLogger.LogError(ex, "App.Initialize");
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // تخزين وسائط سطر الأوامر
                StartupArgs = desktop.Args ?? Array.Empty<string>();

                var mainWindow = new MainWindow();

                // تمرير وسائط سطر الأوامر إلى النافذة إذا كانت موجودة
                if (StartupArgs.Length > 0)
                {
                    try
                    {
                        // استخدام reflection للاتصال بـ ProcessArguments إذا كانت موجودة
                        var method = mainWindow.GetType().GetMethod("ProcessArguments");
                        if (method != null)
                        {
                            method.Invoke(mainWindow, new object[] { StartupArgs });
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError(ex, "App.OnFrameworkInitializationCompleted");
                    }
                }

                desktop.MainWindow = mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// تحميل الأيقونة من الموارد المضمنة
        /// </summary>
        private static Bitmap? LoadIconFromResources()
        {
            try
            {
                // محاولة تحميل الأيقونة من الموارد المضمنة
                var uris = new[]
                {
                    new Uri("avares://AuroraAssetEditor/Assets/icon.ico"),
                    new Uri("avares://AuroraAssetEditor/Assets/icon.png"),
                    new Uri("avares://AuroraAssetEditor/icon.ico"),
                    new Uri("avares://AuroraAssetEditor/icon.png")
                };

                foreach (var uri in uris)
                {
                    try
                    {
                        if (AssetLoader.Exists(uri))
                        {
                            using var stream = AssetLoader.Open(uri);
                            if (stream != null && stream.Length > 0)
                            {
                                return new Bitmap(stream);
                            }
                        }
                    }
                    catch
                    {
                        // تجاهل الأخطاء في محاولة تحميل أيقونة معينة
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, "App.LoadIconFromResources");
                return null;
            }
        }

        /// <summary>
        /// دالة لتسجيل الأخطاء (للتوافق مع الكود القديم)
        /// </summary>
        public static void LogError(Exception ex)
        {
            ErrorLogger.LogError(ex);
        }
    }
}
