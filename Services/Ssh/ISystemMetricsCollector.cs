using System;
using System.Threading.Tasks;
using VeloShell.Models;

namespace VeloShell.Services.Ssh;

public interface ISystemMetricsCollector : IAsyncDisposable
{
    event EventHandler<SystemMetrics>? MetricsUpdated;
    event EventHandler<string>? ErrorOccurred;

    SystemMetrics? CurrentMetrics { get; }
    bool IsRunning { get; }
    bool IsPaused { get; }
    TimeSpan Interval { get; set; }

    void Start();
    void Stop();
    void Pause();
    void Resume();
    Task RefreshOnceAsync();
}
