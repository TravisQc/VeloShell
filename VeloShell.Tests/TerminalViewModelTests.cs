using System.Collections.Generic;
using VeloShell.Models;
using VeloShell.Services;
using VeloShell.Services.Ssh;
using VeloShell.ViewModels;
using Xunit;

namespace VeloShell.Tests;

public class TerminalViewModelTests
{
    private class DummySshSession : ISshSession
    {
        public string Id { get; } = "dummy";
        public Connection Connection { get; } = new() { Host = "10.0.0.1", DisplayName = "Test Server" };
        public SessionState State { get; set; } = SessionState.Connected;
        public string? LastErrorMessage { get; set; }
#pragma warning disable CS0067
        public event System.EventHandler<SessionState>? StateChanged;
#pragma warning restore CS0067
        public bool IsConnected => State == SessionState.Connected;
        public Renci.SshNet.SshClient? SshClient => null;
        public Renci.SshNet.SftpClient? SftpClient => null;
        public Renci.SshNet.ShellStream? ShellStream => null;

        public System.Threading.Tasks.Task ConnectAsync(System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task DisconnectAsync() => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<string> ExecuteCommandAsync(string commandText, System.TimeSpan? timeout = null, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult("");
        public System.Threading.Tasks.Task<Renci.SshNet.ShellStream> CreateShellStreamAsync(string terminalName = "xterm-256color", uint columns = 80, uint rows = 24, uint width = 800, uint height = 600, int bufferSize = 4096, System.Threading.CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();
        public void SendWindowChange(uint columns, uint rows, uint width, uint height) { }
        public void Dispose() { }
        public System.Threading.Tasks.ValueTask DisposeAsync() => System.Threading.Tasks.ValueTask.CompletedTask;
    }

    [Fact]
    public void ToggleHistory_FlipsIsHistoryPaneOpen()
    {
        var session = new DummySshSession();
        using var vm = new TerminalViewModel(session);

        Assert.False(vm.IsHistoryPaneOpen);
        vm.ToggleHistoryCommand.Execute(null);
        Assert.True(vm.IsHistoryPaneOpen);
        vm.ToggleHistoryCommand.Execute(null);
        Assert.False(vm.IsHistoryPaneOpen);
    }

    [Fact]
    public void OnUserInput_TracksCommandLineAndAddsToHistoryOnEnter()
    {
        var session = new DummySshSession();
        var history = new CommandHistoryManager();
        using var vm = new TerminalViewModel(session, history);

        // Simulate typing "uname -a" followed by Enter ("\r")
        vm.OnUserInput("uname -a");
        Assert.Empty(history.Items);

        vm.OnUserInput("\r");
        Assert.Single(history.Items);
        Assert.Equal("uname -a", history.Items[0].CommandText);
        Assert.Equal("10.0.0.1", history.Items[0].Host);
    }

    [Fact]
    public void InsertCommand_FiresCommandInsertRequestedEvent()
    {
        var session = new DummySshSession();
        using var vm = new TerminalViewModel(session);

        string? inserted = null;
        vm.CommandInsertRequested += (_, cmd) => inserted = cmd;

        var item = new CommandHistoryItem("systemctl status nginx");
        vm.InsertCommandCommand.Execute(item);

        Assert.Equal("systemctl status nginx", inserted);
    }

    [Fact]
    public void ClearHistory_ClearsManagerItems()
    {
        var session = new DummySshSession();
        var history = new CommandHistoryManager();
        history.AddCommand("cmd1");
        history.AddCommand("cmd2");

        using var vm = new TerminalViewModel(session, history);
        Assert.Equal(2, vm.HistoryItems.Count);

        vm.ClearHistoryCommand.Execute(null);
        Assert.Empty(history.Items);
    }
}
