using System;
using System.Threading.Tasks;
using VeloShell.Models;
using VeloShell.Services.Ssh;
using VeloShell.ViewModels;
using Xunit;

namespace VeloShell.Tests;

public class DashboardViewModelTests
{
    /// <summary>
    /// Minimal collector double that counts lifecycle calls so a test can prove the
    /// pulse-bar collapse never touches collection or pause state.
    /// </summary>
    private sealed class FakeMetricsCollector : ISystemMetricsCollector
    {
        public int StartCount, StopCount, PauseCount, ResumeCount;

        public event EventHandler<SystemMetrics>? MetricsUpdated;
        public event EventHandler<string>? ErrorOccurred;

        public SystemMetrics? CurrentMetrics => null;
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(2);

        public void Start() { IsRunning = true; StartCount++; }
        public void Stop() { IsRunning = false; StopCount++; }
        public void Pause() { IsPaused = true; PauseCount++; }
        public void Resume() { IsPaused = false; ResumeCount++; }
        public Task RefreshOnceAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        // Exercised to keep the subscribed events live (no CS0067).
        public void RaiseMetrics(SystemMetrics m) => MetricsUpdated?.Invoke(this, m);
        public void RaiseError(string e) => ErrorOccurred?.Invoke(this, e);
    }

    [Fact]
    public void IsMonitorExpanded_DefaultsToExpanded()
    {
        var vm = new DashboardViewModel(new FakeMetricsCollector());

        Assert.True(vm.IsMonitorExpanded);
    }

    [Fact]
    public void ToggleMonitor_FlipsExpandedState()
    {
        var vm = new DashboardViewModel(new FakeMetricsCollector());

        vm.ToggleMonitorCommand.Execute(null);
        Assert.False(vm.IsMonitorExpanded);

        vm.ToggleMonitorCommand.Execute(null);
        Assert.True(vm.IsMonitorExpanded);
    }

    [Fact]
    public void ToggleMonitor_DoesNotChangeCollectionOrPauseState()
    {
        var collector = new FakeMetricsCollector();
        var vm = new DashboardViewModel(collector);
        vm.StartMonitoring(); // collection is running

        vm.ToggleMonitorCommand.Execute(null); // collapse
        vm.ToggleMonitorCommand.Execute(null); // expand

        // Folding is purely visual: the collector keeps running, is never paused /
        // resumed / stopped, and the dashboard's own pause flag is untouched.
        Assert.True(collector.IsRunning);
        Assert.Equal(1, collector.StartCount);
        Assert.Equal(0, collector.StopCount);
        Assert.Equal(0, collector.PauseCount);
        Assert.Equal(0, collector.ResumeCount);
        Assert.False(collector.IsPaused);
        Assert.False(vm.IsPaused);
    }

    [Fact]
    public void TogglePause_IsIndependentOfMonitorCollapse()
    {
        var collector = new FakeMetricsCollector();
        var vm = new DashboardViewModel(collector);

        vm.TogglePauseCommand.Execute(null);

        // Pausing collection does not fold the bar, and vice versa.
        Assert.True(vm.IsPaused);
        Assert.Equal(1, collector.PauseCount);
        Assert.True(vm.IsMonitorExpanded);
    }
}
