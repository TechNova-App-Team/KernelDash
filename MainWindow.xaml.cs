using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using KernelDash.Models;
using KernelDash.Services;
using KernelDash.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace KernelDash
{
    public partial class MainWindow : Window
    {
        private readonly SystemMonitorService _systemMonitor;
        private readonly ProcessManagerService _processManager;
        private readonly FileWatcherService _fileWatcher;
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // Services initialisieren
                _systemMonitor = new SystemMonitorService();
                _processManager = new ProcessManagerService();
                _fileWatcher = new FileWatcherService();
                _viewModel = new MainViewModel();

                DataContext = _viewModel;

                // Hardware-Monitoring starten
                _systemMonitor.StartMonitoring(UpdateHardwareInfo);

                // Initiale Systeminfo laden
                LoadSystemInfo();

                // FileWatcher Event Handler
                _fileWatcher.FileEvent += OnFileWatcherEvent;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler bei Initialisierung: {ex.Message}\n\n{ex.StackTrace}", "Init-Fehler");
            }
        }

        // ===== NAVIGATION EVENTS =====
        private void OnDashboardClick(object sender, RoutedEventArgs e)
        {
            ShowPage(DashboardPage);
            PageTitle.Text = "Dashboard";
        }

        private void OnProcessesClick(object sender, RoutedEventArgs e)
        {
            ShowPage(ProcessesPage);
            PageTitle.Text = "⚙️ Prozesse";
            OnRefreshProcesses(sender, e);
        }

        private void OnFileWatcherClick(object sender, RoutedEventArgs e)
        {
            ShowPage(FileWatcherPage);
            PageTitle.Text = "📁 Dateiwächter";
        }

        private void OnInfoClick(object sender, RoutedEventArgs e)
        {
            ShowPage(InfoPage);
            PageTitle.Text = "ℹ️ Info";
        }

        private void ShowPage(Grid page)
        {
            DashboardPage.Visibility = Visibility.Collapsed;
            ProcessesPage.Visibility = Visibility.Collapsed;
            FileWatcherPage.Visibility = Visibility.Collapsed;
            InfoPage.Visibility = Visibility.Collapsed;
            page.Visibility = Visibility.Visible;
        }

        // ===== DASHBOARD EVENTS =====
        private void UpdateHardwareInfo(double cpuUsage, long usedRam, long totalRam)
        {
            Dispatcher.Invoke(() =>
            {
                CpuValue.Text = $"{cpuUsage:F1}%";
                RamValue.Text = $"{FormatBytes(usedRam)} / {FormatBytes(totalRam)}";
            });
        }

        private void LoadSystemInfo()
        {
            try
            {
                ComputerName.Text = Environment.MachineName;
                OsInfo.Text = Environment.OSVersion.VersionString;
                ProcessorCount.Text = Environment.ProcessorCount.ToString();
                TotalRam.Text = FormatBytes(_systemMonitor.GetTotalRam());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Systeminfo: {ex.Message}", "Fehler");
            }
        }

        // ===== PROCESS MANAGEMENT EVENTS =====
        private void OnRefreshProcesses(object? sender, RoutedEventArgs? e)
        {
            try
            {
                var processes = _processManager.GetAllProcesses();
                ProcessGrid.ItemsSource = processes;
                ProcessCount.Text = processes.Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Prozesse: {ex.Message}", "Fehler");
            }
        }

        private void OnKillProcess(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is ProcessInfo process)
            {
                var result = MessageBox.Show(
                    $"Soll der Prozess '{process.ProcessName}' (PID: {process.ProcessId}) beendet werden?",
                    "Bestätigung",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _processManager.KillProcess(process.ProcessId);
                        MessageBox.Show("Prozess erfolgreich beendet.", "Erfolg");
                        OnRefreshProcesses(sender, e);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Fehler beim Beenden des Prozesses: {ex.Message}", "Fehler");
                    }
                }
            }
            else
            {
                MessageBox.Show("Bitte wählen Sie einen Prozess aus.", "Information");
            }
        }

        // ===== FILE WATCHER EVENTS =====
        private void OnBrowseFolder(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    WatchPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void OnStartWatcher(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = WatchPath.Text;

                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    MessageBox.Show("Bitte geben Sie einen gültigen Pfad ein.", "Fehler");
                    return;
                }

                _fileWatcher.StartWatching(path);
                MessageBox.Show($"Dateiwächter gestartet für:\n{path}", "Erfolg");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Starten des Dateiwächters: {ex.Message}", "Fehler");
            }
        }

        private void OnClearLog(object sender, RoutedEventArgs e)
        {
            FileWatcherLog.Items.Clear();
        }

        private void OnFileWatcherEvent(string message)
        {
            Dispatcher.Invoke(() =>
            {
                FileWatcherLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
                
                // Limit to 1000 entries
                while (FileWatcherLog.Items.Count > 1000)
                {
                    FileWatcherLog.Items.RemoveAt(FileWatcherLog.Items.Count - 1);
                }
            });
        }

        // ===== UTILITY =====
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:F2} {sizes[order]}";
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _systemMonitor.StopMonitoring();
            _fileWatcher.StopWatching();
        }
    }
}
