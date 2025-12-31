# KernelDash - System Manager Dashboard

## 📋 Übersicht

KernelDash ist ein modernes Windows-Desktop-Dashboard für System-Verwaltung, entwickelt mit **WPF (.NET 8)** und **MVVM-Architektur**.

## ✨ Features

### 🖥️ Hardware-Monitoring
- **CPU-Auslastung** in Echtzeit (%)
- **RAM-Auslastung** mit verfügbarem und genutztem Speicher
- Auto-Update alle 1 Sekunde
- Systeminfo (Computername, OS, Prozessoren, RAM gesamt)

### ⚙️ Prozess-Verwaltung
- Liste aller laufenden Prozesse
- **PID**, Prozessname, **Dateipfad** (MainModule.FileName)
- "Beenden"-Button für jeden Prozess
- Refresh-Button für Live-Updates
- Zugriffsfehler-Handling

### 📁 Dateiwächter
- Überwachung beliebiger Pfade mit FileSystemWatcher
- Ereignisse: **Erstellen**, **Löschen**, **Umbenennen**
- Live-Protokoll mit Timestamps
- Automatisches Cleanup (max. 1000 Einträge)
- Folder Browser für Pfadauswahl

### 🎨 UI/UX
- **Moderner Dark-Mode** (#1e1e1e Farbschema)
- **Sidebar-Navigation** mit 4 Views
- Responsive Card-Layout
- Accent-Farben (#007acc)
- Keyboard-friendly

## 🏗️ Projektstruktur

```
KernelDash/
├── KernelDash.csproj
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── Models/
│   ├── ProcessInfo.cs
│   └── HardwareInfo.cs
├── Services/
│   ├── SystemMonitorService.cs
│   ├── ProcessManagerService.cs
│   └── FileWatcherService.cs
├── ViewModels/
│   └── MainViewModel.cs (MVVM)
├── bin/ (Build-Output)
└── README.md
```

## 🚀 Getting Started

### Anforderungen
- Windows 10/11
- .NET 8 SDK

### Installation & Start
```powershell
# Projekt herunterladen
cd KernelDash

# Abhängigkeiten installieren
dotnet restore

# Projekt ausführen
dotnet run
```

### Build für Release
```powershell
dotnet publish -c Release -o ./publish
```

## 📖 Architektur

### MVVM-Pattern
- **MainViewModel**: Zentrale ViewModel mit ObservableObject
- **Services**: Getrennte Geschäftslogik (Hardware, Prozesse, Dateiwächter)
- **Models**: Datenstrukturen (ProcessInfo, HardwareInfo)
- **XAML/CodeBehind**: UI-Definition und Event-Handler

### System.Diagnostics Integration
```csharp
// CPU/RAM Monitoring mit PerformanceCounter
_cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
_ramCounter = new PerformanceCounter("Memory", "Available MBytes");
```

### FileSystemWatcher
```csharp
_watcher = new FileSystemWatcher(path)
{
    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
    IncludeSubdirectories = true
};
```

## 🎯 Code-Highlights

### Hardware-Monitoring (Echtzeit)
```csharp
public void StartMonitoring(Action<double, long, long> onUpdate)
{
    _monitoringTask = Task.Run(() =>
    {
        while (_isMonitoring)
        {
            double cpuUsage = _cpuCounter.NextValue();
            long availableRam = (long)_ramCounter.NextValue();
            long totalRam = GetTotalRam();
            long usedRam = totalRam - (availableRam * 1024 * 1024);
            
            onUpdate(cpuUsage, usedRam, totalRam);
            System.Threading.Thread.Sleep(1000);
        }
    });
}
```

### Prozess-Verwaltung
```csharp
public void KillProcess(int processId)
{
    var process = Process.GetProcessById(processId);
    process.Kill();
    process.WaitForExit();
}
```

### Dateiwächter
```csharp
private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
{
    string action = e.ChangeType.ToString();
    FileEvent?.Invoke($"[{action}] {e.Name}");
}
```

## 🛠️ Erweiterungsmöglichkeiten

- **Netzwerk-Monitoring**: Bandbreitenauslastung
- **Disk-Monitoring**: Festplatten-Auslastung und S.M.A.R.T.-Status
- **Prozess-Details**: CPU/RAM pro Prozess
- **Registry-Editor**: Registry-Einträge bearbeiten
- **Startup-Manager**: Autostart-Programme verwalten
- **Event-Log-Viewer**: Windows Event Logs anzeigen
- **Datenbankaufzeichnung**: Historische Daten speichern

## 📋 Lizenz

MIT License - Frei verwendbar für kommerzielle und private Projekte

## 🤝 Support

Für Bugs oder Feature-Requests, bitte ein Issue erstellen.

---

**Erstellt mit ❤️ für Windows-Administratoren und Power-User**
