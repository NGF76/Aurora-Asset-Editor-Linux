using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;

namespace AuroraAssetEditorLinux
{
    public partial class InputDialog : Window
    {
        public bool? DialogResult { get; private set; }

        public string Value => ValueBox?.Text ?? string.Empty;

        public InputDialog(Window? owner, string information, string defaultValue = "")
        {
            InitializeComponent();

            // تعيين النافذة الأم
            if (owner != null)
            {
                Owner = owner;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            // تعيين النص التوضيحي والقيمة الافتراضية
            InfoLabel.Text = information;
            ValueBox.Text = defaultValue;

            // ربط أحداث الأزرار
            OkButton.Click += OnOkClick;
            CancelButton.Click += OnCancelClick;

            // ربط حدث الضغط على Enter في مربع النص
            ValueBox.KeyDown += OnValueBoxKeyDown;
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

        private void OnValueBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = true;
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            
            // تحديد النص بالكامل وتعيين التركيز
            if (ValueBox != null)
            {
                ValueBox.Focus();
                ValueBox.SelectionStart = 0;
                ValueBox.SelectionEnd = ValueBox.Text?.Length ?? 0;
            }
        }
    }
}
