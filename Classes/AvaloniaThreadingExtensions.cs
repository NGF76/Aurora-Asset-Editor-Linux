using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace AuroraAssetEditorLinux.Classes
{
    public static class AvaloniaThreadingExtensions
    {
        public static void InvokeIfRequired(this Dispatcher dispatcher, Action action)
        {
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (!dispatcher.CheckAccess())
                dispatcher.Invoke(action);
            else
                action();
        }

        public static void BeginInvokeIfRequired(this Dispatcher dispatcher, Action action)
        {
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (!dispatcher.CheckAccess())
                dispatcher.InvokeAsync(action);
            else
                action();
        }

        public static T InvokeIfRequired<T>(this Dispatcher dispatcher, Func<T> func)
        {
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
            if (func == null) throw new ArgumentNullException(nameof(func));

            if (!dispatcher.CheckAccess())
                return dispatcher.Invoke(func);
            else
                return func();
        }

        public static bool IsUIThread(this Dispatcher dispatcher)
        {
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
            return dispatcher.CheckAccess();
        }
    }
}
