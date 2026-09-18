using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AuroraAssetEditorLinux.Classes;

namespace AuroraAssetEditorLinux
{
    public partial class BulkActionsDialog : Window
    {
        private XboxLocale[] _locales = Array.Empty<XboxLocale>();
        private bool _dialogResult;

        public BulkActionsDialog(Window? owner)
        {
            InitializeComponent();

            // تعيين النافذة الأم
            if (owner != null)
            {
                Owner = owner;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            // ربط أحداث الأزرار
            OkButton.Click += BtnDialogOk_Click;
            CancelButton.Click += BtnDialogCancel_Click;

            // تحميل اللغات
            LoadLocalesAsync();
        }

        /// <summary>
        /// نتيجة الحوار (true = OK, false = Cancel)
        /// </summary>
        public bool? DialogResult { get; private set; }

        /// <summary>
        /// اللغة المحددة
        /// </summary>
        public XboxLocale? Locale => LocaleBox.SelectedItem as XboxLocale;

        /// <summary>
        /// هل نستبدل الموجود؟
        /// </summary>
        public bool ReplaceExisting => ReplaceExistingChk?.IsChecked ?? false;

        /// <summary>
        /// هل نحمّل غلاف فقط؟
        /// </summary>
        public bool CoverArtOnly => CoverArtOnlyChk?.IsChecked ?? true;

        private async void LoadLocalesAsync()
        {
            try
            {
                // محاولة جلب اللغات من Xbox
                _locales = await XboxAssetDownloader.GetLocalesAsync();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, "BulkActionsDialog.LoadLocalesAsync");
                // في حالة الفشل، استخدم قائمة افتراضية
                _locales = GetDefaultLocales();
            }

            // تحديث واجهة المستخدم
            LocaleBox.ItemsSource = _locales;

            // تعيين اللغة الافتراضية (en-US)
            var defaultIndex = 0;
            for (var i = 0; i < _locales.Length; i++)
            {
                if (_locales[i].Locale.Equals("en-US", StringComparison.InvariantCultureIgnoreCase))
                {
                    defaultIndex = i;
                    break;
                }
            }
            LocaleBox.SelectedIndex = defaultIndex;
        }

        /// <summary>
        /// قائمة اللغات الافتراضية في حالة فشل التحميل
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
                new XboxLocale("ja-JP", "日本 - 日本語")
            };
        }

        private void BtnDialogOk_Click(object? sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnDialogCancel_Click(object? sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            // تعيين التركيز على زر OK
            OkButton.Focus();
        }
    }
}
