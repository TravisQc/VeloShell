using System.Collections.Generic;
using VeloShell.Models;
using VeloShell.Services.Ssh;
using VeloShell.ViewModels;
using Xunit;

namespace VeloShell.Tests;

public class SessionViewModelShellTests
{
    private static SessionViewModel NewSession()
    {
        var conn = new Connection { Host = "127.0.0.1", Username = "user" };
        var session = new SshSession(conn, "pwd");
        var collector = new SystemMetricsCollector(session);
        var sftp = new SftpService(session);
        return new SessionViewModel(session, collector, sftp);
    }

    [Fact]
    public void CurrentMode_DefaultsToTerminal()
    {
        var vm = NewSession();

        Assert.Equal(SessionMode.Terminal, vm.CurrentMode);
        Assert.True(vm.IsTerminalMode);
        Assert.False(vm.IsHostMode);
        Assert.False(vm.IsSettingsMode);
    }

    [Fact]
    public void SetMode_ChangesCurrentMode_AndRaisesNotifications()
    {
        var vm = NewSession();
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.SetModeCommand.Execute(SessionMode.Host);

        Assert.Equal(SessionMode.Host, vm.CurrentMode);
        Assert.True(vm.IsHostMode);
        Assert.False(vm.IsTerminalMode);
        Assert.Contains(nameof(SessionViewModel.CurrentMode), changed);
        Assert.Contains(nameof(SessionViewModel.IsHostMode), changed);
        Assert.Contains(nameof(SessionViewModel.IsTerminalMode), changed);
    }

    [Fact]
    public void SetMode_ToSettings_UpdatesFlags()
    {
        var vm = NewSession();

        vm.SetModeCommand.Execute(SessionMode.Settings);

        Assert.Equal(SessionMode.Settings, vm.CurrentMode);
        Assert.True(vm.IsSettingsMode);
        Assert.False(vm.IsTerminalMode);
        Assert.False(vm.IsHostMode);
    }

    [Theory]
    [InlineData(SessionState.Connected, SessionHealth.Healthy)]
    [InlineData(SessionState.Disconnected, SessionHealth.Error)]
    [InlineData(SessionState.Error, SessionHealth.Error)]
    [InlineData(SessionState.Connecting, SessionHealth.Error)]
    [InlineData(SessionState.Disconnecting, SessionHealth.Error)]
    public void Health_MapsFromState(SessionState state, SessionHealth expected)
    {
        var vm = NewSession();

        vm.State = state;

        Assert.Equal(expected, vm.Health);
        Assert.Equal(expected == SessionHealth.Healthy, vm.IsHealthy);
        Assert.Equal(expected == SessionHealth.Error, vm.IsError);
    }

    [Fact]
    public void Health_IsWarning_WhenConnectedButDegraded()
    {
        var vm = NewSession();
        vm.State = SessionState.Connected;

        vm.IsDegraded = true;

        Assert.Equal(SessionHealth.Warning, vm.Health);
        Assert.True(vm.IsWarning);
        Assert.False(vm.IsHealthy);
        // A degraded-but-connected session is still alive, so it keeps breathing.
        Assert.True(vm.IsBreathing);
    }

    [Fact]
    public void IsBreathing_OnlyWhileConnected()
    {
        var vm = NewSession();

        vm.State = SessionState.Connected;
        Assert.True(vm.IsBreathing);

        vm.State = SessionState.Error;
        Assert.False(vm.IsBreathing);

        vm.State = SessionState.Disconnected;
        Assert.False(vm.IsBreathing);
    }
}

