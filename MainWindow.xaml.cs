using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
    public class NetworkAdapterInfo
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public string IpAddress { get; set; }
        public string Gateway { get; set; }
        public string DnsServers { get; set; }
    }

    public class ConnectionInfo
    {
        public string LocalAddress { get; set; }
        public string RemoteAddress { get; set; }
        public string State { get; set; }
    }

    public class ProcessInfoEx : ProcessInfo
    {
        public double CpuUsage { get; set; }
        public long MemoryMB { get; set; }
    }

    public partial class MainWindow : Window
    {
        private readonly SystemMonitorService _systemMonitor;
        private readonly ProcessManagerService _processManager;
        private readonly FileWatcherService _fileWatcher;
        private readonly MainViewModel _viewModel;
        private DateTime _startTime;
        private PerformanceCounter _processorCounter;

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // Services
                _systemMonitor = new SystemMonitorService();
                _processManager = new ProcessManagerService();
                _fileWatcher = new FileWatcherService();
                _viewModel = new MainViewModel();

                DataContext = _viewModel;

                _startTime = DateTime.Now;
                _processorCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);

                // Start monitoring
                _systemMonitor.StartMonitoring(UpdateHardwareInfo);

                // Load initial data
                LoadSystemInfo();
                LoadDiskInfo();
                LoadNetworkInfo();

                _fileWatcher.FileEvent += OnFileWatcherEvent;
                this.KeyDown += MainWindow_KeyDown;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Init Error: {ex.Message}\n\n{ex.StackTrace}", "Error");
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

        // ===== NAVIGATION =====
        private void OnDashboardClick(object sender, RoutedEventArgs e)
        {
            ShowPage(DashboardPage);
            PageTitle.Text = "📊 Dashboard";
            PageSubtitle.Text = "System Status Overview";
            UpdateAllDashboardData();
            UpdateNavButtons(BtnDashboard);
        }

        private void OnProcessesClick(object sender, RoutedEventArgs e)
        {
            ShowPage(ProcessesPage);
            PageTitle.Text = "⚙️ Processes";
            PageSubtitle.Text = "Manage running applications";
            OnRefreshProcesses(sender, e);
            UpdateNavButtons(BtnProcesses);
        }

        private void OnNetworkClick(object sender, RoutedEventArgs e)
        {
            ShowPage(NetworkPage);
            PageTitle.Text = "🌐 Network";
            PageSubtitle.Text = "Network adapters & connections";
            LoadNetworkInfo();
            UpdateNavButtons(BtnNetwork);
        }

        private void OnFileWatcherClick(object sender, RoutedEventArgs e)
        {
            ShowPage(FileWatcherPage);
            PageTitle.Text = "📂 File Watcher";
            PageSubtitle.Text = "Monitor file system changes";
            UpdateNavButtons(BtnFileWatch);
        }

        private void OnInfoClick(object sender, RoutedEventArgs e)
        {
            ShowPage(InfoPage);
            PageTitle.Text = "ℹ️ Information";
            PageSubtitle.Text = "About KernelDash";
            UpdateNavButtons(BtnInfo);
        }

        private void ShowPage(UIElement page)
        {
            DashboardPage.Visibility = Visibility.Collapsed;
            ProcessesPage.Visibility = Visibility.Collapsed;
            NetworkPage.Visibility = Visibility.Collapsed;
            FileWatcherPage.Visibility = Visibility.Collapsed;
            InfoPage.Visibility = Visibility.Collapsed;
            page.Visibility = Visibility.Visible;
        }

        private void UpdateNavButtons(System.Windows.Controls.Button active)
        {
            BtnDashboard.Foreground = System.Windows.Media.Brushes.Gray;
            BtnProcesses.Foreground = System.Windows.Media.Brushes.Gray;
            BtnNetwork.Foreground = System.Windows.Media.Brushes.Gray;
            BtnFileWatch.Foreground = System.Windows.Media.Brushes.Gray;
            BtnInfo.Foreground = System.Windows.Media.Brushes.Gray;
            active.Foreground = System.Windows.Media.Brushes.White;
        }

        private void Button_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && (System.Windows.Media.Brush)btn.Foreground != System.Windows.Media.Brushes.White)
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

        // ===== DASHBOARD =====
        private void UpdateHardwareInfo(double cpuUsage, long usedRam, long totalRam)
        {
            Dispatcher.Invoke(() =>
            {
                CpuValue.Text = $"{cpuUsage:F1}%";
                CpuProgress.Value = cpuUsage;
                CpuState.Text = cpuUsage > 75 ? "🔴 High" : cpuUsage > 50 ? "🟡 Medium" : "🟢 Low";

                double ramPercent = (double)usedRam / totalRam * 100;
                RamValue.Text = $"{FormatBytes(usedRam)} / {FormatBytes(totalRam)}";
                RamProgress.Value = ramPercent;
                RamPercent.Text = $"{ramPercent:F1}%";

                ProcessCount.Text = Process.GetProcesses().Length.ToString();

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
            catch { }
        }

        private void LoadSystemInfo()
        {
            ComputerName.Text = Environment.MachineName;
            UserName.Text = Environment.UserName;
            ProcessorCount.Text = Environment.ProcessorCount.ToString();
            OsInfo.Text = GetOperatingSystemName();
            TotalRam.Text = FormatBytes(_systemMonitor.GetTotalPhysicalMemory());
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
                var osObject = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
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

        // ===== PROCESSES =====
        private void OnRefreshProcesses(object sender, RoutedEventArgs e)
        {
            try
            {
                var processes = Process.GetProcesses()
                    .Select(p => new ProcessInfoEx
                    {
                        ProcessId = p.Id,
                        ProcessName = p.ProcessName,
                        FilePath = p.MainModule?.FileName ?? "N/A",
                        MemoryMB = p.WorkingSet64 / (1024 * 1024),
                        CpuUsage = GetProcessCpuUsage(p)
                    })
                    .OrderByDescending(x => x.MemoryMB)
                    .ToList();

                ProcessGrid.ItemsSource = processes;
                ProcessCountLabel.Text = processes.Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private double GetProcessCpuUsage(Process process)
        {
            try
            {
                var cpuCounter = new PerformanceCounter("Process", "% Processor Time", process.ProcessName, true);
                return cpuCounter.NextValue() / Environment.ProcessorCount;
            }
            catch
            {
                return 0;
            }
        }

        private void OnShowProcessDetails(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is ProcessInfoEx proc)
            {
                MessageBox.Show(
                    $"Process: {proc.ProcessName}\n\n" +
                    $"PID: {proc.ProcessId}\n" +
                    $"Memory: {proc.MemoryMB} MB\n" +
                    $"CPU: {proc.CpuUsage:F2}%\n" +
                    $"Path: {proc.FilePath}",
                    "Process Details");
            }
            else
            {
                MessageBox.Show("Select a process first!");
            }
        }

        private void OnKillProcess(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is int processId)
            {
                if (MessageBox.Show($"Kill process {processId}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _processManager.KillProcess(processId);
                        OnRefreshProcesses(null, null);
                        MessageBox.Show("Process killed!", "Success");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error: {ex.Message}");
                    }
                }
            }
        }

        // ===== NETWORK =====
        private void LoadNetworkInfo()
        {
            try
            {
                // Load network adapters
                var adapters = new ObservableCollection<NetworkAdapterInfo>();
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    var ipProps = adapter.GetIPProperties();
                    var ipAddresses = ipProps.UnicastAddresses
                        .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork)
                        .FirstOrDefault();

                    adapters.Add(new NetworkAdapterInfo
                    {
                        Name = adapter.Name,
                        Status = adapter.OperationalStatus.ToString(),
                        IpAddress = ipAddresses?.Address.ToString() ?? "N/A",
                        Gateway = ipProps.GatewayAddresses.FirstOrDefault()?.Address.ToString() ?? "N/A",
                        DnsServers = string.Join(", ", ipProps.DnsAddresses.Take(2))
                    });
                }
                NetworkAdapters.ItemsSource = adapters;

                // Load network stats
                LoadNetworkStatistics();
                LoadActiveConnections();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Network error: {ex.Message}");
            }
        }

        private void LoadNetworkStatistics()
        {
            try
            {
                long sentPackets = 0, receivedPackets = 0, errors = 0, sentBytes = 0, receivedBytes = 0, inboundDropped = 0;

                foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    var stats = iface.GetIPStatistics();
                    sentPackets += stats.UnicastPacketsSent;
                    receivedPackets += stats.UnicastPacketsReceived;
                    errors += stats.IncomingPacketsDiscarded;
                    sentBytes += stats.BytesSent;
                    receivedBytes += stats.BytesReceived;
                    inboundDropped += stats.IncomingPacketsDiscarded;
                }

                PacketsSent.Text = sentPackets.ToString();
                PacketsReceived.Text = receivedPackets.ToString();
                NetworkErrors.Text = errors.ToString();
                BytesSent.Text = FormatBytes(sentBytes);
                BytesReceived.Text = FormatBytes(receivedBytes);
                InboundDropped.Text = inboundDropped.ToString();
            }
            catch { }
        }

        private void LoadActiveConnections()
        {
            try
            {
                var connections = new ObservableCollection<ConnectionInfo>();
                
                // Get TCP connections
                var tcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetTcpConnections();
                foreach (var conn in tcpConnections.Take(20))
                {
                    connections.Add(new ConnectionInfo
                    {
                        LocalAddress = $"{conn.LocalEndPoint.Address}:{conn.LocalEndPoint.Port}",
                        RemoteAddress = $"{conn.RemoteEndPoint.Address}:{conn.RemoteEndPoint.Port}",
                        State = conn.State.ToString()
                    });
                }

                ActiveConnections.ItemsSource = connections;
            }
            catch { }
        }

        // ===== FILE WATCHER =====
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
                    MessageBox.Show("Path does not exist!");
                    return;
                }
                _fileWatcher.StartWatching(path);
                FileWatcherLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ✓ Watching: {path}");
                MessageBox.Show("Watcher started!", "Info");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void OnClearLog(object sender, RoutedEventArgs e)
        {
            FileWatcherLog.Items.Clear();
        }
    }
}
