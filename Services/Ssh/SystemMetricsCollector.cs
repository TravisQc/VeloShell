using System;
using System.Threading;
using System.Threading.Tasks;
using VeloShell.Models;

namespace VeloShell.Services.Ssh;

public sealed class SystemMetricsCollector : ISystemMetricsCollector
{
    private readonly ISshSession _session;
    private readonly LinuxMetricsParserState _parserState = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _pollLoopTask;
    private bool _isDisposed;

    public event EventHandler<SystemMetrics>? MetricsUpdated;
    public event EventHandler<string>? ErrorOccurred;

    public SystemMetrics? CurrentMetrics { get; private set; }
    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;
    public bool IsPaused { get; private set; }
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(2);

    public SystemMetricsCollector(ISshSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        IsPaused = false;
        _pollLoopTask = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _cts?.Cancel();
        try
        {
            _pollLoopTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch { /* ignore cancel exceptions */ }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _pollLoopTask = null;
        }
    }

    public void Pause()
    {
        IsPaused = true;
    }

    public void Resume()
    {
        IsPaused = false;
        _ = Task.Run(RefreshOnceAsync);
    }

    public async Task RefreshOnceAsync()
    {
        if (!_session.IsConnected) return;

        if (!await _syncLock.WaitAsync(0).ConfigureAwait(false))
            return; // Previous poll still in progress, skip to prevent congestion

        try
        {
            var raw = await _session.ExecuteCommandAsync(
                LinuxMetricsParser.CompositeCommand,
                timeout: TimeSpan.FromSeconds(3)
            ).ConfigureAwait(false);

            var metrics = LinuxMetricsParser.Parse(raw, _parserState);
            CurrentMetrics = metrics;
            MetricsUpdated?.Invoke(this, metrics);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        // Immediate first poll
        await RefreshOnceAsync().ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (IsPaused || !_session.IsConnected)
                continue;

            await RefreshOnceAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Stop();
        _syncLock.Dispose();
        await Task.CompletedTask;
    }
}
