using System;
using System.IO;
using System.Threading;

namespace CasaCreta.GHSync
{
    internal sealed class GHSyncFileWatcher : IDisposable
    {
        private readonly Action _onChanged;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _debounceTimer;
        private int _disposed;

        public GHSyncFileWatcher(
            string filePath,
            Action onChanged)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "A source file path is required.",
                    nameof(filePath));

            string fullPath = Path.GetFullPath(filePath);
            string directory = Path.GetDirectoryName(fullPath);
            string fileName = Path.GetFileName(fullPath);

            if (string.IsNullOrWhiteSpace(directory)
                || !Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException(
                    "The source directory does not exist: " + directory);
            }

            _onChanged = onChanged
                ?? throw new ArgumentNullException(nameof(onChanged));

            _debounceTimer = new Timer(
                OnDebounceElapsed,
                null,
                Timeout.Infinite,
                Timeout.Infinite);

            _watcher = new FileSystemWatcher(directory, fileName)
            {
                IncludeSubdirectories = false,
                NotifyFilter =
                    NotifyFilters.FileName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
                    | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            _watcher.Renamed += OnRenamed;
        }

        private void OnFileEvent(
            object sender,
            FileSystemEventArgs eventArgs)
        {
            RestartDebounce();
        }

        private void OnRenamed(
            object sender,
            RenamedEventArgs eventArgs)
        {
            RestartDebounce();
        }

        private void RestartDebounce()
        {
            if (Volatile.Read(ref _disposed) != 0) return;

            try
            {
                _debounceTimer.Change(
                    GHSyncSettings.DebounceMilliseconds,
                    Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                // A final file event can race with component removal.
            }
        }

        private void OnDebounceElapsed(object state)
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            _onChanged();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileEvent;
            _watcher.Created -= OnFileEvent;
            _watcher.Renamed -= OnRenamed;
            _watcher.Dispose();
            _debounceTimer.Dispose();
        }
    }
}
