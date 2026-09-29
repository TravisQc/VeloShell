using System.Linq;
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

    [Fact]
    public void InitialState_HasOneDetailTab()
    {
        var connStore = new DummyConnectionStore();
        var credStore = new DummyCredentialStore();
        var vm = new MainViewModel(connStore, credStore);

        Assert.Single(vm.Tabs);
        Assert.IsType<ConnectionDetailTabViewModel>(vm.Tabs[0]);
        Assert.Same(vm.Tabs[0], vm.SelectedTab);
        Assert.False(vm.Tabs[0].CanClose);
    }

    [Fact]
    public async Task CloseAllSessionsAsync_RemovesOnlySessionTabs()
    {
        var connStore = new DummyConnectionStore();
        var credStore = new DummyCredentialStore();
        var vm = new MainViewModel(connStore, credStore);

        var conn = new Connection { Host = "127.0.0.1", Username = "user" };
        var session = new SshSession(conn, "pwd");
        var collector = new SystemMetricsCollector(session);
        var sftp = new SftpService(session);
        var sessionVm = new SessionViewModel(session, collector, sftp);

        vm.Tabs.Add(sessionVm);
        vm.SelectedTab = sessionVm;
        Assert.Equal(2, vm.Tabs.Count);

        await vm.CloseAllSessionsAsync();

        Assert.Single(vm.Tabs);
        Assert.IsType<ConnectionDetailTabViewModel>(vm.Tabs[0]);
        Assert.Same(vm.Tabs[0], vm.SelectedTab);
    }

    [Fact]
    public void CloseTab_RemovesSpecificSessionTab()
    {
        var connStore = new DummyConnectionStore();
        var credStore = new DummyCredentialStore();
        var vm = new MainViewModel(connStore, credStore);

        var conn = new Connection { Host = "127.0.0.1", Username = "user" };
        var session = new SshSession(conn, "pwd");
        var collector = new SystemMetricsCollector(session);
        var sftp = new SftpService(session);
        var sessionVm = new SessionViewModel(session, collector, sftp);

        vm.Tabs.Add(sessionVm);
        vm.SelectedTab = sessionVm;

        vm.CloseTab(sessionVm);

        Assert.Single(vm.Tabs);
        Assert.IsType<ConnectionDetailTabViewModel>(vm.Tabs[0]);
        Assert.Same(vm.Tabs[0], vm.SelectedTab);
    }
}
