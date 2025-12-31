using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
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
        private DateTime _startTime;

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

                // Start time merken für Uptime
                _startTime = DateTime.Now;

                // Hardware-Monitoring starten
                _systemMonitor.StartMonitoring(UpdateHardwareInfo);

                // Initiale Systeminfo laden
                LoadSystemInfo();
                LoadDiskInfo();

                // FileWatcher Event Handler
                _fileWatcher.FileEvent += OnFileWatcherEvent;

                // Tastenkürzel
                this.KeyDown += MainWindow_KeyDown;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler bei Initialisierung: {ex.Message}\n\n{ex.StackTrace}", "Init-Fehler");
            }
        }

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                OnRefreshProcesses(sender, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        // ===== NAVIGATION EVENTS =====
        private void OnDashboardClick(object sender, RoutedEventArgs e)
        {
            ShowPage(DashboardPage);
            PageTitle.Text = "📊 Dashboard";
            UpdateAllDashboardData();
            BtnDashboard.Foreground = System.Windows.Media.Brushes.White;
            BtnProcesses.Foreground = System.Windows.Media.Brushes.Gray;
            BtnFileWatch.Foreground = System.Windows.Media.Brushes.Gray;
            BtnInfo.Foreground = System.Windows.Media.Brushes.Gray;
        }

        private void OnProcessesClick(object sender, RoutedEventArgs e)
        {
            ShowPage(ProcessesPage);
            PageTitle.Text = "⚙️ Prozesse";
            OnRefreshProcesses(sender, e);
            BtnDashboard.Foreground = System.Windows.Media.Brushes.Gray;
            BtnProcesses.Foreground = System.Windows.Media.Brushes.White;
            BtnFileWatch.Foreground = System.Windows.Media.Brushes.Gray;
            BtnInfo.Foreground = System.Windows.Media.Brushes.Gray;
        }

        private void OnFileWatcherClick(object sender, RoutedEventArgs e)
        {
            ShowPage(FileWatcherPage);
            PageTitle.Text = "📂 Dateiwächter";
            BtnDashboard.Foreground = System.Windows.Media.Brushes.Gray;
            BtnProcesses.Foreground = System.Windows.Media.Brushes.Gray;
            BtnFileWatch.Foreground = System.Windows.Media.Brushes.White;
            BtnInfo.Foreground = System.Windows.Media.Brushes.Gray;
        }

        private void OnInfoClick(object sender, RoutedEventArgs e)
        {
            ShowPage(InfoPage);
            PageTitle.Text = "ℹ️ Information";
            BtnDashboard.Foreground = System.Windows.Media.Brushes.Gray;
            BtnProcesses.Foreground = System.Windows.Media.Brushes.Gray;
            BtnFileWatch.Foreground = System.Windows.Media.Brushes.Gray;
            BtnInfo.Foreground = System.Windows.Media.Brushes.White;
        }

        private void ShowPage(UIElement page)
        {
            DashboardPage.Visibility = Visibility.Collapsed;
            ProcessesPage.Visibility = Visibility.Collapsed;
            FileWatcherPage.Visibility = Visibility.Collapsed;
            InfoPage.Visibility = Visibility.Collapsed;
            page.Visibility = Visibility.Visible;
        }

        private void Button_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && (System.Windows.Media.Brush)btn.Foreground == System.Windows.Media.Brushes.Gray)
            {
                btn.Foreground = System.Windows.Media.Brushes.LightGray;
            }
        }

        private void Button_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && (System.Windows.Media.Brush)btn.Foreground == System.Windows.Media.Brushes.LightGray)
            {
                btn.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        // ===== DASHBOARD EVENTS =====
        private void UpdateHardwareInfo(double cpuUsage, long usedRam, long totalRam)
        {
            Dispatcher.Invoke(() =>
            {
                CpuValue.Text = $"{cpuUsage:F1}%";
                CpuProgress.Value = cpuUsage;
                CpuState.Text = cpuUsage > 75 ? "🔴 Hoch" : cpuUsage > 50 ? "🟡 Mittel" : "🟢 Niedrig";

                double ramPercent = (double)usedRam / totalRam * 100;
                RamValue.Text = $"{FormatBytes(usedRam)} / {FormatBytes(totalRam)}";
                RamProgress.Value = ramPercent;
                RamPercent.Text = $"{ramPercent:F1}%";

                ProcessCount.Text = Process.GetProcesses().Length.ToString();

                // Update Uptime
                TimeSpan uptime = DateTime.Now - _startTime;
                UpTime.Text = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
            });
        }

        private void UpdateAllDashboardData()
        {
            try
            {
                LoadSystemInfo();
                LoadDiskInfo();
            }
            catch (Exception ex)
            {
                // Silently handle
            }
        }

        private void LoadSystemInfo()
        {
            try
            {
                ComputerName.Text = Environment.MachineName;
                UserName.Text = Environment.UserName;
                ProcessorCount.Text = Environment.ProcessorCount.ToString();
                OsInfo.Text = GetOperatingSystemName();
                TotalRam.Text = FormatBytes(_systemMonitor.GetTotalPhysicalMemory());
                Architecture.Text = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";

                // IP & MAC Adresse
                try
                {
                    var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                    var ipAddress = hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    IpAddress.Text = ipAddress?.ToString() ?? "-";

                    var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                    var activeInterface = networkInterfaces.FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up);
                    MacAddress.Text = activeInterface?.GetPhysicalAddress().ToString() ?? "-";

                    var dnsAddresses = activeInterface?.GetIPProperties().DnsAddresses;
                    DnsServer.Text = dnsAddresses?.FirstOrDefault()?.ToString() ?? "-";
                }
                catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading system info: {ex.Message}");
            }
        }

        private void LoadDiskInfo()
        {
            try
            {
                var driveInfo = DriveInfo.GetDrives().FirstOrDefault(d => d.Name.StartsWith("C:"));
                if (driveInfo != null)
                {
                    long usedSpace = driveInfo.TotalSize - driveInfo.AvailableFreeSpace;
                    DiskValue.Text = $"{FormatBytes(usedSpace)} / {FormatBytes(driveInfo.TotalSize)}";
                    DiskProgress.Value = (double)usedSpace / driveInfo.TotalSize * 100;
                    DiskPercent.Text = $"{(double)usedSpace / driveInfo.TotalSize * 100:F1}%";
                }
            }
            catch { }
        }

        private string GetOperatingSystemName()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                var osCollection = searcher.Get();
                var osObject = osCollection.Cast<ManagementObject>().FirstOrDefault();
                if (osObject != null)
                {
                    string caption = osObject["Caption"]?.ToString() ?? "-";
                    string version = osObject["Version"]?.ToString() ?? "";
                    return $"{caption} ({version})";
                }
            }
            catch { }
            return $"Windows {Environment.OSVersion.VersionString}";
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:F1} {sizes[order]}";
        }

        // ===== PROCESS EVENTS =====
        private void OnRefreshProcesses(object sender, RoutedEventArgs e)
        {
            try
            {
                var processes = _processManager.GetAllProcesses();
                ProcessGrid.ItemsSource = processes;
                ProcessCountLabel.Text = processes.Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Aktualisieren: {ex.Message}");
            }
        }

        private void OnKillProcess(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is int processId)
            {
                if (MessageBox.Show($"Soll der Prozess wirklich beendet werden?", "Bestätigung", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _processManager.KillProcess(processId);
                        OnRefreshProcesses(null, null);
                        MessageBox.Show("Prozess beendet!", "Erfolg");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Fehler beim Beenden: {ex.Message}");
                    }
                }
            }
        }

        // ===== FILE WATCHER EVENTS =====
        private void OnFileWatcherEvent(string message)
        {
            Dispatcher.Invoke(() =>
            {
                FileWatcherLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
                if (FileWatcherLog.Items.Count > 1000)
                {
                    FileWatcherLog.Items.RemoveAt(FileWatcherLog.Items.Count - 1);
                }
            });
        }

        private void OnBrowseFolder(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                WatchPath.Text = dialog.SelectedPath;
            }
        }

        private void OnStartWatcher(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = WatchPath.Text;
                if (!Directory.Exists(path))
                {
                    MessageBox.Show("Pfad existiert nicht!");
                    return;
                }
                _fileWatcher.StartWatching(path);
                FileWatcherLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ✓ Überwachung gestartet: {path}");
                MessageBox.Show("Überwachung gestartet!", "Info");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}");
            }
        }

        private void OnClearLog(object sender, RoutedEventArgs e)
        {
            FileWatcherLog.Items.Clear();
        }
    }
}
