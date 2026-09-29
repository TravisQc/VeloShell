using VeloShell.Models;
using VeloShell.Services;

namespace VeloShell.Tests;

public class ConnectionStoreTests : IDisposable
{
    private static readonly Argon2Parameters Fast = new()
    {
        MemoryKib = 1024,
        Iterations = 1,
        DegreeOfParallelism = 1,
    };

    private readonly string _dir;
    private readonly string _connectionsPath;
    private readonly string _vaultPath;

    public ConnectionStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "veloshell-conn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _connectionsPath = Path.Combine(_dir, "connections.json");
        _vaultPath = Path.Combine(_dir, "vault.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort cleanup */ }
    }

    private (ConnectionStore store, CredentialStore creds) NewStore()
    {
        var creds = new CredentialStore(_vaultPath, Fast);
        return (new ConnectionStore(_connectionsPath, creds), creds);
    }

    [Fact]
    public void Connection_DefaultsPortTo22() => Assert.Equal(22, new Connection().Port);

    [Fact]
    public void Connection_Label_FallsBackToUserHostPort()
    {
        var c = new Connection { Host = "example.com", Port = 2222, Username = "root" };
        Assert.Equal("root@example.com:2222", c.Label);
    }

    [Fact]
    public void Connection_Label_UsesDisplayNameWhenSet()
    {
        var c = new Connection { DisplayName = "Prod DB", Host = "example.com", Username = "root" };
        Assert.Equal("Prod DB", c.Label);
    }

    [Fact]
    public void AddAndPersist_SurvivesReload()
    {
        var (store, _) = NewStore();
        var folder = store.AddFolder("生产环境", ConnectionStore.RootId);
        store.AddConnection(new Connection { Host = "10.0.0.1", Username = "root", DisplayName = "web-1" }, folder.Id);

        var (reloaded, _) = NewStore(); // simulates restart
        var reloadedFolder = Assert.Single(reloaded.Root.Folders);
        Assert.Equal("生产环境", reloadedFolder.Name);
        var reloadedConn = Assert.Single(reloadedFolder.Connections);
        Assert.Equal("web-1", reloadedConn.DisplayName);
        Assert.Equal("10.0.0.1", reloadedConn.Host);
    }

    [Fact]
    public void UpdateConnection_Persists()
    {
        var (store, _) = NewStore();
        var conn = new Connection { Host = "h", Username = "u" };
        store.AddConnection(conn, ConnectionStore.RootId);

        conn.Host = "new-host";
        store.UpdateConnection(conn);

        var (reloaded, _) = NewStore();
        Assert.Equal("new-host", reloaded.Root.Connections.Single().Host);
    }

    [Fact]
    public void MoveConnection_ChangesParentFolder()
    {
        var (store, _) = NewStore();
        var folder = store.AddFolder("f1", ConnectionStore.RootId);
        var conn = new Connection { Host = "h", Username = "u" };
        store.AddConnection(conn, ConnectionStore.RootId);

        store.MoveConnection(conn.Id, folder.Id);

        Assert.Empty(store.Root.Connections);
        Assert.Single(store.Root.Folders.Single().Connections);
    }

    [Fact]
    public void RemoveConnection_CascadesSecretRemoval()
    {
        var (store, creds) = NewStore();
        creds.SetMasterPassword("pw");
        var conn = new Connection { Host = "h", Username = "u" };
        store.AddConnection(conn, ConnectionStore.RootId);
        creds.SaveSecret(conn.Id, "secret");
        Assert.Equal("secret", creds.GetSecret(conn.Id));

        store.RemoveConnection(conn.Id);

        Assert.Null(creds.GetSecret(conn.Id));
        Assert.Empty(store.Root.Connections);
    }

    [Fact]
    public void RemoveFolder_CascadesSecretsForContainedConnections()
    {
        var (store, creds) = NewStore();
        creds.SetMasterPassword("pw");
        var folder = store.AddFolder("f1", ConnectionStore.RootId);
        var conn = new Connection { Host = "h", Username = "u" };
        store.AddConnection(conn, folder.Id);
        creds.SaveSecret(conn.Id, "secret");

        store.RemoveFolder(folder.Id);

        Assert.Null(creds.GetSecret(conn.Id));
        Assert.Empty(store.Root.Folders);
    }
}
