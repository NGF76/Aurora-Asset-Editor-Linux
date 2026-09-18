using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Threading.Tasks;

namespace AuroraAssetEditorLinux.Dialogs
{
    public partial class CustomMessageBox : Window
    {
        private TaskCompletionSource<bool?>? _tcs;

        public CustomMessageBox()
        {
            InitializeComponent();

            OkButton.Click += OnOkClick;
            CancelButton.Click += OnCancelClick;
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            _tcs?.SetResult(true);
            Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            _tcs?.SetResult(false);
            Close();
        }

        public static async Task<bool?> ShowAsync(Window? parent, string message, string title = "Message", bool showCancel = true)
        {
            var dialog = new CustomMessageBox
            {
                Title = title,
                MessageText = { Text = message }
            };

            if (parent != null)
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            if (!showCancel)
            {
                dialog.CancelButton.IsVisible = false;
            }

            dialog._tcs = new TaskCompletionSource<bool?>();

            if (parent != null)
            {
                await dialog.ShowDialog(parent);
            }
            else
            {
                dialog.Show();
            }

            return await dialog._tcs.Task;
        }
    }
}
