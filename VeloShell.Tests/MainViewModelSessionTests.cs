using System.Threading.Tasks;
using VeloShell.Models;
using VeloShell.Services;
using VeloShell.Services.Ssh;
using VeloShell.ViewModels;
using Xunit;

namespace VeloShell.Tests;

public class MainViewModelSessionTests
{
    private class DummyConnectionStore : IConnectionStore
    {
        public ConnectionFolder Root { get; } = new ConnectionFolder { Name = "Root" };
        public void Load() { }
        public void Save() { }
        public void AddConnection(Connection connection, string folderId) => Root.Connections.Add(connection);
        public void UpdateConnection(Connection connection) { }
        public void RemoveConnection(string connectionId) { }
        public void MoveConnection(string connectionId, string targetFolderId) { }
        public ConnectionFolder AddFolder(string name, string parentFolderId) => new ConnectionFolder { Name = name };
        public void RenameFolder(string folderId, string newName) { }
        public void RemoveFolder(string folderId) { }
    }

    private class DummyCredentialStore : ICredentialStore
    {
        public bool IsInitialized => true;
        public bool IsUnlocked => true;
        public void SetMasterPassword(string password) { }
        public bool Unlock(string password) => true;
        public void Lock() { }
        public bool ChangeMasterPassword(string currentPassword, string newPassword) => true;
        public void SaveSecret(string connectionId, string secret) { }
        public string? GetSecret(string connectionId) => "secret";
        public void RemoveSecret(string connectionId) { }
        public void Reset() { }
    }

    private static MainViewModel NewMain()
        => new MainViewModel(new DummyConnectionStore(), new DummyCredentialStore());

    private static SessionViewModel NewSession()
    {
        var conn = new Connection { Host = "127.0.0.1", Username = "user" };
        var session = new SshSession(conn, "pwd");
        var collector = new SystemMetricsCollector(session);
        var sftp = new SftpService(session);
        return new SessionViewModel(session, collector, sftp);
    }

    [Fact]
    public void InitialState_ShowsLauncher_WithNoSessions()
    {
        var vm = NewMain();

        Assert.Empty(vm.Sessions);
        Assert.NotNull(vm.Launcher);
        Assert.Null(vm.ActiveSession);
        Assert.True(vm.IsLauncherVisible);
        Assert.False(vm.HasActiveSession);
    }

    [Fact]
    public void ActivatingSession_HidesLauncher_AndShowLauncherRestoresIt()
    {
        var vm = NewMain();
        var session = NewSession();
        vm.Sessions.Add(session);

        vm.ActiveSession = session;
        Assert.False(vm.IsLauncherVisible);
        Assert.True(vm.HasActiveSession);

        vm.ShowLauncherCommand.Execute(null);
        Assert.Null(vm.ActiveSession);
        Assert.True(vm.IsLauncherVisible);

        // The launcher is not a member of the session collection; showing it
        // does not add a tab.
        Assert.Single(vm.Sessions);
    }

    [Fact]
    public async Task CloseAllSessions_ClearsSessions_AndShowsLauncher()
    {
        var vm = NewMain();
        var first = NewSession();
        var second = NewSession();
        vm.Sessions.Add(first);
        vm.Sessions.Add(second);
        vm.ActiveSession = second;

        await vm.CloseAllSessionsAsync();

        Assert.Empty(vm.Sessions);
        Assert.Null(vm.ActiveSession);
        Assert.True(vm.IsLauncherVisible);
    }

    [Fact]
    public void CloseTab_RemovesSession_AndFallsBackToRemainingThenLauncher()
    {
        var vm = NewMain();
        var first = NewSession();
        var second = NewSession();
        vm.Sessions.Add(first);
        vm.Sessions.Add(second);
        vm.ActiveSession = second;

        vm.CloseTab(second);
        Assert.Single(vm.Sessions);
        Assert.Same(first, vm.ActiveSession);
        Assert.False(vm.IsLauncherVisible);

        vm.CloseTab(first);
        Assert.Empty(vm.Sessions);
        Assert.Null(vm.ActiveSession);
        Assert.True(vm.IsLauncherVisible);
    }

    [Fact]
    public void SessionMode_IsRetainedPerSession()
    {
        var vm = NewMain();
        var first = NewSession();
        var second = NewSession();
        vm.Sessions.Add(first);
        vm.Sessions.Add(second);

        first.SetModeCommand.Execute(SessionMode.Host);
        second.SetModeCommand.Execute(SessionMode.Settings);

        // Activating each session reflects that session's own current mode,
        // not a globally shared one.
        vm.ActiveSession = first;
        Assert.Equal(SessionMode.Host, vm.ActiveSession!.CurrentMode);

        vm.ActiveSession = second;
        Assert.Equal(SessionMode.Settings, vm.ActiveSession!.CurrentMode);

        // Switching back leaves the first session's mode untouched.
        vm.ActiveSession = first;
        Assert.Equal(SessionMode.Host, vm.ActiveSession!.CurrentMode);
    }
}
