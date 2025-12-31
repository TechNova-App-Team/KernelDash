using System;
using System.IO;

namespace KernelDash.Services
{
    public class FileWatcherService
    {
        private FileSystemWatcher? _watcher;
        public event Action<string>? FileEvent;

        public void StartWatching(string path)
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }

            _watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true
            };

            _watcher.Created += OnFileSystemEvent;
            _watcher.Deleted += OnFileSystemEvent;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Error += OnWatcherError;

            _watcher.EnableRaisingEvents = true;
        }

        public void StopWatching()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
        }

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            string action = e.ChangeType.ToString();
            string type = File.Exists(e.FullPath) ? "Datei" : "Ordner";
            FileEvent?.Invoke($"[{action}] {type}: {e.Name}");
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            string type = File.Exists(e.FullPath) ? "Datei" : "Ordner";
            FileEvent?.Invoke($"[Umbenannt] {type}: {e.OldName} → {e.Name}");
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            if (e.GetException() is Exception ex)
            {
                FileEvent?.Invoke($"[Fehler] {ex.Message}");
            }
        }
    }
}
