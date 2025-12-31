using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KernelDash.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string currentPage = "Dashboard";

        [ObservableProperty]
        private double cpuUsage;

        [ObservableProperty]
        private string ramUsage = "0 MB / 0 MB";

        [RelayCommand]
        private void NavigateToDashboard()
        {
            CurrentPage = "Dashboard";
        }

        [RelayCommand]
        private void NavigateToProcesses()
        {
            CurrentPage = "Processes";
        }

        [RelayCommand]
        private void NavigateToFileWatcher()
        {
            CurrentPage = "FileWatcher";
        }
    }
}
