using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeloShell.Controls;
using VeloShell.Models;
using VeloShell.Services.Ssh;

namespace VeloShell.ViewModels;

public partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly ISystemMetricsCollector _collector;
    private bool _disposed;

    public DashboardViewModel(ISystemMetricsCollector collector)
    {
        _collector = collector ?? throw new ArgumentNullException(nameof(collector));
        _collector.MetricsUpdated += OnMetricsUpdated;
        _collector.ErrorOccurred += OnErrorOccurred;
    }

    [ObservableProperty] private double _cpuUsagePercent;
    [ObservableProperty] private string _cpuCoresText = "1 核";

    [ObservableProperty] private double _memoryUsagePercent;
    [ObservableProperty] private string _memoryText = "0 B / 0 B";

    [ObservableProperty] private double _swapUsagePercent;
    [ObservableProperty] private string _swapText = "0 B / 0 B";

    [ObservableProperty] private string _downloadRateText = "0 B/s";
    [ObservableProperty] private string _uploadRateText = "0 B/s";
    [ObservableProperty] private string _totalNetworkText = "0 B / 0 B";

    [ObservableProperty] private string _hostname = "-";
    [ObservableProperty] private string _osVersion = "-";
    [ObservableProperty] private string _kernelVersion = "-";
    [ObservableProperty] private string _uptime = "-";
    [ObservableProperty] private string _loadAverage = "-";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseResumeButtonText))]
    private bool _isPaused;

    // Pulse-bar collapse state. Purely visual and independent of collection:
    // folding never pauses the collector nor touches IsPaused (see design.md).
    // Default expanded; each session's dashboard holds its own state.
    [ObservableProperty] private bool _isMonitorExpanded = true;

    [ObservableProperty] private string? _lastError;
    [ObservableProperty] private DateTime? _lastUpdated;

    public string PauseResumeButtonText => IsPaused ? "恢复刷新" : "暂停刷新";

    public ObservableCollection<DiskPartitionMetrics> Disks { get; } = new();

    // Fixed-capacity rolling history feeding the pulse-bar sparklines. The
    // collector only exposes the latest snapshot; these retain a short window.
    // Capacity and cadence of collection are unchanged.
    private const int HistoryCapacity = 60;
    public MetricSeries CpuHistory { get; } = new(HistoryCapacity);
    public MetricSeries MemoryHistory { get; } = new(HistoryCapacity);
    public MetricSeries NetDownHistory { get; } = new(HistoryCapacity);
    public MetricSeries NetUpHistory { get; } = new(HistoryCapacity);

    public void StartMonitoring()
    {
        _collector.Start();
    }

    /// <summary>Collapses / expands the pulse bar. Visual only; collection continues.</summary>
    [RelayCommand]
    private void ToggleMonitor()
    {
        IsMonitorExpanded = !IsMonitorExpanded;
    }

    [RelayCommand]
    private void TogglePause()
    {
        if (IsPaused)
        {
            _collector.Resume();
            IsPaused = false;
        }
        else
        {
            _collector.Pause();
            IsPaused = true;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _collector.RefreshOnceAsync();
    }

    private void OnMetricsUpdated(object? sender, SystemMetrics m)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CpuUsagePercent = m.Cpu.UsagePercent;
            CpuCoresText = $"{m.Cpu.CoreCount} 核";

            MemoryUsagePercent = m.Memory.UsagePercent;
            MemoryText = $"{SftpEntryItem.FormatHumanSize(m.Memory.UsedBytes)} / {SftpEntryItem.FormatHumanSize(m.Memory.TotalBytes)}";

            SwapUsagePercent = m.Memory.SwapUsagePercent;
            SwapText = $"{SftpEntryItem.FormatHumanSize(m.Memory.SwapUsedBytes)} / {SftpEntryItem.FormatHumanSize(m.Memory.SwapTotalBytes)}";

            DownloadRateText = $"{SftpEntryItem.FormatHumanSize((long)m.Network.DownloadBytesPerSec)}/s";
            UploadRateText = $"{SftpEntryItem.FormatHumanSize((long)m.Network.UploadBytesPerSec)}/s";
            TotalNetworkText = $"↓ {SftpEntryItem.FormatHumanSize(m.Network.TotalReceivedBytes)}  ↑ {SftpEntryItem.FormatHumanSize(m.Network.TotalTransmittedBytes)}";

            // Append to the rolling history windows driving the sparklines.
            CpuHistory.Add(m.Cpu.UsagePercent);
            MemoryHistory.Add(m.Memory.UsagePercent);
            NetDownHistory.Add(m.Network.DownloadBytesPerSec);
            NetUpHistory.Add(m.Network.UploadBytesPerSec);

            if (!string.IsNullOrWhiteSpace(m.Host.Hostname))
                Hostname = m.Host.Hostname;
            if (!string.IsNullOrWhiteSpace(m.Host.OsVersion))
                OsVersion = m.Host.OsVersion;
            if (!string.IsNullOrWhiteSpace(m.Host.KernelVersion))
                KernelVersion = m.Host.KernelVersion;
            if (!string.IsNullOrWhiteSpace(m.Host.Uptime))
                Uptime = m.Host.Uptime;
            if (!string.IsNullOrWhiteSpace(m.Host.LoadAverage))
                LoadAverage = m.Host.LoadAverage;

            Disks.Clear();
            foreach (var d in m.Disks)
                Disks.Add(d);

            LastError = null;
            LastUpdated = m.Timestamp.ToLocalTime();
        });
    }

    private void OnErrorOccurred(object? sender, string error)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            LastError = error;
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _collector.MetricsUpdated -= OnMetricsUpdated;
        _collector.ErrorOccurred -= OnErrorOccurred;
        _collector.Stop();
    }
}
