using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace KernelDash.Services
{
    public class SystemMonitorService
    {
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramCounter;
        private bool _isMonitoring;
        private Task? _monitoringTask;
        private bool _countersInitialized = false;

        public SystemMonitorService()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes", null, true);
                
                // Trigger initial reads to initialize counters
                var cpu = _cpuCounter.NextValue();
                var ram = _ramCounter.NextValue();
                
                _countersInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Performance Counter init error: {ex.Message}");
                _countersInitialized = false;
            }
        }

        public void StartMonitoring(Action<double, long, long> onUpdate)
        {
            if (_isMonitoring)
                return;

            _isMonitoring = true;
            _monitoringTask = Task.Run(() => MonitoringLoop(onUpdate));
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            _monitoringTask?.Wait(TimeSpan.FromSeconds(5));
        }

        private void MonitoringLoop(Action<double, long, long> onUpdate)
        {
            while (_isMonitoring)
            {
                try
                {
                    if (!_countersInitialized || _cpuCounter == null || _ramCounter == null)
                    {
                        onUpdate(0, 0, GetTotalRam());
                        System.Threading.Thread.Sleep(1000);
                        continue;
                    }

                    double cpuUsage = _cpuCounter.NextValue();
                    long availableRam = (long)_ramCounter.NextValue();
                    long totalRam = GetTotalRam();
                    long usedRam = totalRam - (availableRam * 1024 * 1024);

                    onUpdate(cpuUsage, usedRam, totalRam);

                    System.Threading.Thread.Sleep(1000); // Update every second
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Monitoring error: {ex.Message}");
                    System.Threading.Thread.Sleep(1000);
                }
            }
        }

        public long GetTotalRam()
        {
            try
            {
                // Get total physical memory using Windows API
                return GetTotalPhysicalMemory();
            }
            catch
            {
                return 8L * 1024L * 1024L * 1024L; // Default 8GB
            }
        }

        private long GetTotalPhysicalMemory()
        {
            try
            {
                // Using System.Diagnostics to get memory info
                var computerInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();
                return (long)computerInfo.TotalPhysicalMemory;
            }
            catch
            {
                return 8L * 1024L * 1024L * 1024L; // Fallback to 8GB
            }
        }
    }
}
