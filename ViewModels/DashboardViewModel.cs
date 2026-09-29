using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    [ObservableProperty] private string _kernelVersion = "-";
    [ObservableProperty] private string _uptime = "-";
    [ObservableProperty] private string _loadAverage = "-";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseResumeButtonText))]
    private bool _isPaused;

    [ObservableProperty] private string? _lastError;
    [ObservableProperty] private DateTime? _lastUpdated;

    public string PauseResumeButtonText => IsPaused ? "恢复刷新" : "暂停刷新";

    public ObservableCollection<DiskPartitionMetrics> Disks { get; } = new();

    public void StartMonitoring()
    {
        _collector.Start();
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

            if (!string.IsNullOrWhiteSpace(m.Host.Hostname))
                Hostname = m.Host.Hostname;
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
