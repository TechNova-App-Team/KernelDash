using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using KernelDash.Models;

namespace KernelDash.Services
{
    public class ProcessManagerService
    {
        public ObservableCollection<ProcessInfo> GetAllProcesses()
        {
            var processes = new ObservableCollection<ProcessInfo>();

            try
            {
                var allProcesses = Process.GetProcesses();

                foreach (var process in allProcesses.OrderBy(p => p.ProcessName))
                {
                    try
                    {
                        string? filePath = null;

                        try
                        {
                            // Try to get the main module filename
                            filePath = process.MainModule?.FileName ?? "N/A";
                        }
                        catch
                        {
                            // If access is denied, mark as such
                            filePath = "[Zugriff verweigert]";
                        }

                        processes.Add(new ProcessInfo
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            FilePath = filePath
                        });
                    }
                    catch
                    {
                        // Skip processes we can't access
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting processes: {ex.Message}");
            }

            return processes;
        }

        public void KillProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.Kill();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Prozess {processId} konnte nicht beendet werden.", ex);
            }
        }
    }
}
