using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using AuroraAssetEditorLinux.Helpers;

namespace AuroraAssetEditorLinux
{
    public partial class TitleAndDbIdDialog : Window
    {
        public bool? DialogResult { get; private set; }

        public string TitleId => TitleIdBox?.Text ?? string.Empty;

        public string DbId => DbIdBox?.Text ?? string.Empty;

        public string AssetId => $"{TitleIdBox?.Text ?? string.Empty}_{DbIdBox?.Text ?? string.Empty}";

        public TitleAndDbIdDialog(Window? owner)
        {
            InitializeComponent();

            // تعيين النافذة الأم
            if (owner != null)
            {
                Owner = owner;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            // تعيين القيم الافتراضية من GlobalState
            TitleIdBox.Text = GlobalState.CurrentGame.TitleId;
            DbIdBox.Text = GlobalState.CurrentGame.DbId;

            // ربط أحداث الأزرار
            OkButton.Click += OnOkClick;
            CancelButton.Click += OnCancelClick;

            // ربط أحداث النص
            TitleIdBox.TextChanged += OnTextChanged;
            DbIdBox.TextChanged += OnTextChanged;
            TitleIdBox.KeyDown += OnTextBoxKeyDown;
            DbIdBox.KeyDown += OnTextBoxKeyDown;

            // فلترة المدخلات لمنع الأحرف غير السداسية عشرية
            TitleIdBox.TextInput += OnTextInput;
            DbIdBox.TextInput += OnTextInput;

            // تحديث حالة زر OK في البداية
            UpdateOkButtonState();
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OnTextInput(object? sender, TextInputEventArgs e)
        {
            // السماح فقط بالأحرف السداسية العشرية
            if (!string.IsNullOrEmpty(e.Text))
            {
                var isValid = uint.TryParse(e.Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out _);
                e.Handled = !isValid;
            }
        }

        private void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            // تنظيف النص من الأحرف غير السداسية عشرية
            if (TitleIdBox != null)
            {
                TitleIdBox.Text = Regex.Replace(TitleIdBox.Text ?? string.Empty, "[^a-fA-F0-9]+", "");
            }
            if (DbIdBox != null)
            {
                DbIdBox.Text = Regex.Replace(DbIdBox.Text ?? string.Empty, "[^a-fA-F0-9]+", "");
            }

            UpdateOkButtonState();
        }

        private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // إذا كان زر OK مفعلاً، ننفذ الإجراء
                if (OkButton?.IsEnabled == true)
                {
                    OnOkClick(sender, e);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                OnCancelClick(sender, e);
                e.Handled = true;
            }
        }

        private void UpdateOkButtonState()
        {
            if (OkButton != null)
            {
                var titleIdValid = TitleIdBox?.Text?.Length == 8;
                var dbIdValid = DbIdBox?.Text?.Length == 8;
                OkButton.IsEnabled = titleIdValid && dbIdValid;
            }
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            // تعيين التركيز على حقل TitleID وتحديد النص
            TitleIdBox?.Focus();
            TitleIdBox?.SelectAll();
        }
    }
}
