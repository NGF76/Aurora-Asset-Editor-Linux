using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Threading;

namespace AuroraAssetEditorLinux.Classes
{
    public class ThreadSafeObservableCollection<T> : ObservableCollection<T>
    {
        private readonly Dispatcher _dispatcher;
        private readonly ReaderWriterLockSlim _lock;

        public ThreadSafeObservableCollection()
        {
            _dispatcher = Dispatcher.UIThread; // استخدام Dispatcher الخاص بـ Avalonia
            _lock = new ReaderWriterLockSlim();
        }

        protected override void ClearItems()
        {
            // استخدام InvokeIfRequired الخاص بـ Avalonia
            if (_dispatcher.CheckAccess())
            {
                _lock.EnterWriteLock();
                try { base.ClearItems(); }
                finally { _lock.ExitWriteLock(); }
            }
            else
            {
                _dispatcher.Invoke(() =>
                {
                    _lock.EnterWriteLock();
                    try { base.ClearItems(); }
                    finally { _lock.ExitWriteLock(); }
                });
            }
        }

        protected override void InsertItem(int index, T item)
        {
            if (_dispatcher.CheckAccess())
            {
                if (index > Count)
                    return;

                _lock.EnterWriteLock();
                try { base.InsertItem(index, item); }
                finally { _lock.ExitWriteLock(); }
            }
            else
            {
                _dispatcher.Invoke(() =>
                {
                    if (index > Count)
                        return;

                    _lock.EnterWriteLock();
                    try { base.InsertItem(index, item); }
                    finally { _lock.ExitWriteLock(); }
                });
            }
        }

        protected override void MoveItem(int oldIndex, int newIndex)
        {
            if (_dispatcher.CheckAccess())
            {
                _lock.EnterReadLock();
                var itemCount = Count;
                _lock.ExitReadLock();

                if (oldIndex >= itemCount || newIndex >= itemCount || oldIndex == newIndex)
                    return;

                _lock.EnterWriteLock();
                try { base.MoveItem(oldIndex, newIndex); }
                finally { _lock.ExitWriteLock(); }
            }
            else
            {
                _dispatcher.Invoke(() =>
                {
                    _lock.EnterReadLock();
                    var itemCount = Count;
                    _lock.ExitReadLock();

                    if (oldIndex >= itemCount || newIndex >= itemCount || oldIndex == newIndex)
                        return;

                    _lock.EnterWriteLock();
                    try { base.MoveItem(oldIndex, newIndex); }
                    finally { _lock.ExitWriteLock(); }
                });
            }
        }

        protected override void RemoveItem(int index)
        {
            if (_dispatcher.CheckAccess())
            {
                if (index >= Count)
                    return;

                _lock.EnterWriteLock();
                try { base.RemoveItem(index); }
                finally { _lock.ExitWriteLock(); }
            }
            else
            {
                _dispatcher.Invoke(() =>
                {
                    if (index >= Count)
                        return;

                    _lock.EnterWriteLock();
                    try { base.RemoveItem(index); }
                    finally { _lock.ExitWriteLock(); }
                });
            }
        }

        protected override void SetItem(int index, T item)
        {
            if (_dispatcher.CheckAccess())
            {
                _lock.EnterWriteLock();
                try { base.SetItem(index, item); }
                finally { _lock.ExitWriteLock(); }
            }
            else
            {
                _dispatcher.Invoke(() =>
                {
                    _lock.EnterWriteLock();
                    try { base.SetItem(index, item); }
                    finally { _lock.ExitWriteLock(); }
                });
            }
        }

        // إضافة دالة مساعدة للتحقق من الوصول
        public bool IsOnUIThread => _dispatcher.CheckAccess();

        // دالة لإضافة عنصر بأمان من أي خيط
        public void AddSafe(T item)
        {
            if (_dispatcher.CheckAccess())
            {
                lock (this)
                {
                    base.Add(item);
                }
            }
            else
            {
                _dispatcher.Invoke(() =>
                {
                    lock (this)
                    {
                        base.Add(item);
                    }
                });
            }
        }

        // دالة لإزالة عنصر بأمان من أي خيط
        public bool RemoveSafe(T item)
        {
            if (_dispatcher.CheckAccess())
            {
                lock (this)
                {
                    return base.Remove(item);
                }
            }
            else
            {
                return _dispatcher.Invoke(() =>
                {
                    lock (this)
                    {
                        return base.Remove(item);
                    }
                });
            }
        }
    }
}
