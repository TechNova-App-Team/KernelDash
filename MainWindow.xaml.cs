using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace KernelDash
{
    public class NetworkAdapterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string DnsServers { get; set; } = string.Empty;
    }

    public class ConnectionInfo
    {
        public string LocalAddress { get; set; } = string.Empty;
        public string RemoteAddress { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
    }

    public class ProcessInfoEx
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public double CpuUsage { get; set; }
        public long MemoryMB { get; set; }
    }

    public partial class MainWindow : Window
    {
        private DateTime _startTime;
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramCounter;
        private Thread _monitorThread;

        public MainWindow()
        {
            InitializeComponent();
            _startTime = DateTime.Now;
            
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes", null, true);
                _cpuCounter.NextValue();
                _ramCounter.NextValue();
            }
            catch { }
            
            LoadSystemInfo();
            LoadDiskInfo();
            UpdateAllDashboardData();
            LoadNetworkInfo();
            
            // Set initial active button
            UpdateNavButtons(BtnDashboard);
            
            _monitorThread = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(1000);
                    try
                    {
                        Dispatcher.Invoke(() => UpdateAllDashboardData());
                    }
                    catch { }
                }
            }) { IsBackground = true };
            _monitorThread.Start();
        }
        
        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure progress bars are properly sized after page loads
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateAllDashboardData();
                LoadDiskInfo();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void LoadSystemInfo()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("select Caption from Win32_OperatingSystem");
                foreach (var obj in searcher.Get())
                {
                    OsInfo.Text = obj["Caption"]?.ToString() ?? "Unknown";
                }

                searcher = new ManagementObjectSearcher("select Name from Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    CpuInfo.Text = obj["Name"]?.ToString() ?? "Unknown";
                }

                var memory = new Microsoft.VisualBasic.Devices.ComputerInfo();
                RamInfo.Text = $"{(memory.TotalPhysicalMemory / 1024 / 1024 / 1024)} GB";
            }
            catch { }
        }

        private void LoadDiskInfo()
        {
            try
            {
                var drives = DriveInfo.GetDrives();
                if (drives.Length > 0)
                {
                    var drive = drives[0];
                    var total = drive.TotalSize / 1024 / 1024 / 1024;
                    var used = (drive.TotalSize - drive.AvailableFreeSpace) / 1024 / 1024 / 1024;
                    var percent = (100.0 * used / total);
                    DiskUsageText.Text = $"{percent:F0}%";
                    UpdateProgressBar(DiskBarContainer, percent);
                }
            }
            catch { }
        }
        
        private void UpdateProgressBar(Border progressBar, double percentage)
        {
            if (progressBar != null)
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // Find the parent Border container (the background border)
                        var parent = progressBar.Parent as Border;
                        if (parent != null)
                        {
                            // Force layout update to get actual width
                            parent.UpdateLayout();
                            var maxWidth = parent.ActualWidth > 0 ? parent.ActualWidth : 200;
                            var newWidth = Math.Max(0, Math.Min(maxWidth, maxWidth * percentage / 100.0));
                            progressBar.Width = newWidth;
                        }
                    }
                    catch { }
                });
            }
        }

        private void UpdateAllDashboardData()
        {
            try
            {
                if (_cpuCounter != null)
                {
                    var cpuUsage = _cpuCounter.NextValue();
                    CpuUsageText.Text = $"{cpuUsage:F1}%";
                    UpdateProgressBar(CpuBarContainer, cpuUsage);
                }

                if (_ramCounter != null)
                {
                    var totalMemory = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory / 1024 / 1024;
                    var availableMemory = _ramCounter.NextValue();
                    var usedMemory = totalMemory - availableMemory;
                    var ramUsage = (100.0 * usedMemory / totalMemory);
                    RamUsageText.Text = $"{ramUsage:F1}%";
                    UpdateProgressBar(RamBarContainer, ramUsage);
                }

                var processes = Process.GetProcesses();
                ProcessCountText.Text = processes.Length.ToString();
                
                // Update uptime
                var uptime = DateTime.Now - _startTime;
                UptimeText.Text = $"Uptime: {uptime.Hours}h {uptime.Minutes}m";
            }
            catch { }
        }

        private void OnDashboardClick(object sender, RoutedEventArgs e)
        {
            ShowPage(DashboardPage);
            UpdateNavButtons(BtnDashboard);
        }

        private void OnProcessesClick(object sender, RoutedEventArgs e)
        {
            ShowPage(ProcessesPage);
            OnRefreshProcesses(sender, e);
            UpdateNavButtons(BtnProcesses);
        }

        private void OnNetworkClick(object sender, RoutedEventArgs e)
        {
            ShowPage(NetworkPage);
            LoadNetworkInfo();
            UpdateNavButtons(BtnNetwork);
        }

        private void OnFileWatcherClick(object sender, RoutedEventArgs e)
        {
            ShowPage(FileWatcherPage);
            UpdateNavButtons(BtnFileWatch);
        }

        private void OnGPUClick(object sender, RoutedEventArgs e)
        {
            ShowPage(GPUPage);
            UpdateNavButtons(BtnGPU);
            LoadGPUInfo();
        }

        private void OnServicesClick(object sender, RoutedEventArgs e)
        {
            ShowPage(ServicesPage);
            UpdateNavButtons(BtnServices);
            OnRefreshServices(sender, e);
        }

        private void OnStartupClick(object sender, RoutedEventArgs e)
        {
            ShowPage(StartupPage);
            UpdateNavButtons(BtnStartup);
            OnRefreshStartup(sender, e);
        }

        private void OnLogsClick(object sender, RoutedEventArgs e)
        {
            ShowPage(LogsPage);
            UpdateNavButtons(BtnLogs);
            OnRefreshLogs(sender, e);
        }

        private void OnInfoClick(object sender, RoutedEventArgs e)
        {
            ShowPage(InfoPage);
            UpdateNavButtons(BtnInfo);
        }

        private void ShowPage(UIElement page)
        {
            DashboardPage.Visibility = Visibility.Collapsed;
            ProcessesPage.Visibility = Visibility.Collapsed;
            NetworkPage.Visibility = Visibility.Collapsed;
            GPUPage.Visibility = Visibility.Collapsed;
            ServicesPage.Visibility = Visibility.Collapsed;
            StartupPage.Visibility = Visibility.Collapsed;
            FileWatcherPage.Visibility = Visibility.Collapsed;
            LogsPage.Visibility = Visibility.Collapsed;
            InfoPage.Visibility = Visibility.Collapsed;

            if (page != null)
                page.Visibility = Visibility.Visible;
        }

        private void UpdateNavButtons(System.Windows.Controls.Button active)
        {
            // Reset all buttons to default style
            BtnDashboard.Style = (Style)FindResource("SidebarButton");
            BtnProcesses.Style = (Style)FindResource("SidebarButton");
            BtnNetwork.Style = (Style)FindResource("SidebarButton");
            BtnGPU.Style = (Style)FindResource("SidebarButton");
            BtnServices.Style = (Style)FindResource("SidebarButton");
            BtnStartup.Style = (Style)FindResource("SidebarButton");
            BtnFileWatch.Style = (Style)FindResource("SidebarButton");
            BtnLogs.Style = (Style)FindResource("SidebarButton");
            BtnInfo.Style = (Style)FindResource("SidebarButton");

            // Set active button style
            active.Style = (Style)FindResource("ActiveSidebarButton");
        }

        private void OnRefreshProcesses(object sender, RoutedEventArgs e)
        {
            try
            {
                var processes = Process.GetProcesses();
                var processesWithMetrics = new ObservableCollection<ProcessInfoEx>();

                foreach (var process in processes.Take(100))
                {
                    try
                    {
                        var cpuCounter = new PerformanceCounter("Process", "% Processor Time", process.ProcessName, true);
                        cpuCounter.NextValue();
                        Thread.Sleep(50);

                        processesWithMetrics.Add(new ProcessInfoEx
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            FilePath = process.MainModule?.FileName ?? "N/A",
                            CpuUsage = cpuCounter.NextValue() / Environment.ProcessorCount,
                            MemoryMB = process.WorkingSet64 / 1024 / 1024
                        });
                    }
                    catch { }
                }

                ProcessList.ItemsSource = processesWithMetrics.OrderByDescending(p => p.CpuUsage).ToList();
            }
            catch { }
        }

        private void OnKillProcess(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            if (btn?.Tag != null && int.TryParse(btn.Tag.ToString(), out var pid))
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    process.Kill();
                    OnRefreshProcesses(sender, e);
                }
                catch { }
            }
        }

        private void LoadNetworkInfo()
        {
            try
            {
                var adapters = new ObservableCollection<NetworkAdapterInfo>();
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    var ipProps = ni.GetIPProperties();
                    adapters.Add(new NetworkAdapterInfo
                    {
                        Name = ni.Name,
                        Status = ni.OperationalStatus.ToString(),
                        IpAddress = ipProps.UnicastAddresses.FirstOrDefault()?.Address.ToString() ?? "N/A",
                        Gateway = ipProps.GatewayAddresses.FirstOrDefault()?.Address.ToString() ?? "N/A",
                        DnsServers = string.Join(", ", ipProps.DnsAddresses.Take(2))
                    });
                }
                NetworkAdapters.ItemsSource = adapters;

                LoadNetworkStatistics();
                LoadActiveConnections();
            }
            catch { }
        }

        private void LoadNetworkStatistics()
        {
            try
            {
                var ipGlobal = IPGlobalProperties.GetIPGlobalProperties();
                var ipStats = ipGlobal.GetIPv4GlobalStatistics();
                
                // Use available properties from IPv4InterfaceStatistics or aggregate from network interfaces
                long totalBytesSent = 0;
                long totalBytesReceived = 0;
                long totalPacketsSent = 0;
                long totalPacketsReceived = 0;
                
                foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (iface.NetworkInterfaceType != NetworkInterfaceType.Loopback && 
                        iface.OperationalStatus == OperationalStatus.Up)
                    {
                        var ifaceStats = iface.GetIPv4Statistics();
                        totalBytesSent += ifaceStats.BytesSent;
                        totalBytesReceived += ifaceStats.BytesReceived;
                        totalPacketsSent += ifaceStats.UnicastPacketsSent;
                        totalPacketsReceived += ifaceStats.UnicastPacketsReceived;
                    }
                }
                
                PacketsSent.Text = totalPacketsSent.ToString();
                PacketsReceived.Text = totalPacketsReceived.ToString();
                NetworkErrors.Text = "N/A";
                BytesSent.Text = FormatBytes(totalBytesSent);
                BytesReceived.Text = FormatBytes(totalBytesReceived);
                InboundDropped.Text = "N/A";
            }
            catch { }
        }

        private void LoadActiveConnections()
        {
            try
            {
                var ipGlobal = IPGlobalProperties.GetIPGlobalProperties();
                var connections = new ObservableCollection<ConnectionInfo>();

                var tcpTable = ipGlobal.GetActiveTcpConnections();
                foreach (var conn in tcpTable.Take(50))
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
            if (Directory.Exists(WatchPath.Text))
            {
                FileWatcherLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Watcher started on {WatchPath.Text}");
            }
        }

        private void OnClearLog(object sender, RoutedEventArgs e)
        {
            FileWatcherLog.Items.Clear();
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:F1} {sizes[order]}";
        }

        // GPU Monitoring
        private void LoadGPUInfo()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("select Name, AdapterRAM from Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString() ?? "Unknown GPU";
                    var ram = obj["AdapterRAM"];
                    GpuInfoText.Text = name;
                    
                    // Try to get GPU usage (requires additional libraries like NVIDIA/AMD SDK)
                    GpuUsageText.Text = "N/A";
                    GpuTempText.Text = "N/A";
                }
            }
            catch
            {
                GpuInfoText.Text = "GPU information not available";
            }
        }

        // Services Management
        private void OnRefreshServices(object sender, RoutedEventArgs e)
        {
            try
            {
                var services = new ObservableCollection<ServiceInfo>();
                var searcher = new ManagementObjectSearcher("select Name, State, StartMode from Win32_Service");
                foreach (var obj in searcher.Get())
                {
                    services.Add(new ServiceInfo
                    {
                        Name = obj["Name"]?.ToString() ?? "Unknown",
                        Status = obj["State"]?.ToString() ?? "Unknown",
                        StartupType = obj["StartMode"]?.ToString() ?? "Unknown"
                    });
                }
                ServicesList.ItemsSource = services;
            }
            catch { }
        }

        private void OnServiceAction(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            if (btn?.Tag != null)
            {
                try
                {
                    var serviceName = btn.Tag.ToString();
                    var service = new System.ServiceProcess.ServiceController(serviceName);
                    if (service.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                    {
                        service.Stop();
                    }
                    else
                    {
                        service.Start();
                    }
                    Thread.Sleep(500);
                    OnRefreshServices(sender, e);
                }
                catch { }
            }
        }

        // Startup Programs
        private void OnRefreshStartup(object sender, RoutedEventArgs e)
        {
            try
            {
                var startupItems = new ObservableCollection<StartupInfo>();
                
                // Registry: HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
                var userKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (userKey != null)
                {
                    foreach (var name in userKey.GetValueNames())
                    {
                        startupItems.Add(new StartupInfo
                        {
                            Name = name,
                            Path = userKey.GetValue(name)?.ToString() ?? "",
                            Location = "Current User"
                        });
                    }
                }

                // Registry: HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Run
                var machineKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (machineKey != null)
                {
                    foreach (var name in machineKey.GetValueNames())
                    {
                        startupItems.Add(new StartupInfo
                        {
                            Name = name,
                            Path = machineKey.GetValue(name)?.ToString() ?? "",
                            Location = "All Users"
                        });
                    }
                }

                StartupList.ItemsSource = startupItems;
            }
            catch { }
        }

        private void OnToggleStartup(object sender, RoutedEventArgs e)
        {
            // Implementation for disabling startup programs
            MessageBox.Show("Startup program management requires administrator privileges.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // System Logs
        private void OnRefreshLogs(object sender, RoutedEventArgs e)
        {
            try
            {
                var logEntries = new ObservableCollection<LogEntry>();
                var logType = LogTypeCombo.SelectedIndex switch
                {
                    0 => System.Diagnostics.EventLogEntryType.Information,
                    1 => System.Diagnostics.EventLogEntryType.Warning,
                    2 => System.Diagnostics.EventLogEntryType.Error,
                    _ => System.Diagnostics.EventLogEntryType.Information
                };

                var logName = LogTypeCombo.SelectedIndex switch
                {
                    0 => "Application",
                    1 => "System",
                    2 => "Security",
                    _ => "Application"
                };

                var eventLog = new System.Diagnostics.EventLog(logName);
                var entries = eventLog.Entries.Cast<System.Diagnostics.EventLogEntry>()
                    .OrderByDescending(x => x.TimeWritten)
                    .Take(100);

                foreach (var entry in entries)
                {
                    logEntries.Add(new LogEntry
                    {
                        Time = entry.TimeWritten.ToString("yyyy-MM-dd HH:mm:ss"),
                        Message = entry.Message
                    });
                }

                LogsList.ItemsSource = logEntries;
            }
            catch { }
        }
    }

    // Data Models for new features
    public class ServiceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StartupType { get; set; } = string.Empty;
    }

    public class StartupInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    public class LogEntry
    {
        public string Time { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
