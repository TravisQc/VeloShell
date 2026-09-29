using VeloShell.Services;

namespace VeloShell.Tests;

public class CredentialStoreTests : IDisposable
{
    private static readonly Argon2Parameters Fast = new()
    {
        MemoryKib = 1024,
        Iterations = 1,
        DegreeOfParallelism = 1,
    };

    private readonly string _dir;
    private readonly string _vault;

    public CredentialStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "veloshell-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _vault = Path.Combine(_dir, "vault.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort cleanup */ }
    }

    private CredentialStore NewStore() => new(_vault, Fast);

    [Fact]
    public void VaultData_SerializationRoundTrips()
    {
        var v = new VaultData { Salt = "abc", MemoryKib = 1024, Iterations = 1, DegreeOfParallelism = 1, Canary = "xyz" };
        v.Entries["c1"] = "blob1";

        AtomicJson.Write(_vault, v);
        var back = AtomicJson.Read<VaultData>(_vault)!;

        Assert.Equal(v.Salt, back.Salt);
        Assert.Equal(v.Canary, back.Canary);
        Assert.Equal("blob1", back.Entries["c1"]);
    }

    [Fact]
    public void Set_Then_Unlock_CorrectAndWrongPassword()
    {
        var store = NewStore();
        Assert.False(store.IsInitialized);

        store.SetMasterPassword("master-pw");
        Assert.True(store.IsInitialized);
        Assert.True(store.IsUnlocked);

        store.Lock();
        Assert.False(store.IsUnlocked);

        Assert.False(store.Unlock("wrong-pw"));
        Assert.False(store.IsUnlocked);

        Assert.True(store.Unlock("master-pw"));
        Assert.True(store.IsUnlocked);
    }

    [Fact]
    public void Save_Get_Remove_Secret()
    {
        var store = NewStore();
        store.SetMasterPassword("master-pw");

        store.SaveSecret("conn-1", "super-secret");
        Assert.Equal("super-secret", store.GetSecret("conn-1"));

        store.RemoveSecret("conn-1");
        Assert.Null(store.GetSecret("conn-1"));
    }

    [Fact]
    public void SavedSecret_IsNotStoredInPlaintext()
    {
        var store = NewStore();
        store.SetMasterPassword("master-pw");
        store.SaveSecret("conn-1", "PLAINTEXT-NEEDLE");

        Assert.DoesNotContain("PLAINTEXT-NEEDLE", File.ReadAllText(_vault));
    }

    [Fact]
    public void Secrets_SurviveRestart()
    {
        var store = NewStore();
        store.SetMasterPassword("master-pw");
        store.SaveSecret("conn-1", "value-1");
        store.Lock();

        var reopened = NewStore(); // simulates an app restart against the same file
        Assert.True(reopened.IsInitialized);
        Assert.True(reopened.Unlock("master-pw"));
        Assert.Equal("value-1", reopened.GetSecret("conn-1"));
    }

    [Fact]
    public void GetSecret_WhenLocked_Throws()
    {
        var store = NewStore();
        store.SetMasterPassword("master-pw");
        store.SaveSecret("conn-1", "value-1");
        store.Lock();

        Assert.Throws<InvalidOperationException>(() => store.GetSecret("conn-1"));
    }

    [Fact]
    public void ChangeMasterPassword_ReencryptsAndInvalidatesOld()
    {
        var store = NewStore();
        store.SetMasterPassword("old-pw");
        store.SaveSecret("conn-1", "value-1");

        Assert.True(store.ChangeMasterPassword("old-pw", "new-pw"));

        var reopened = NewStore();
        Assert.False(reopened.Unlock("old-pw"));
        Assert.True(reopened.Unlock("new-pw"));
        Assert.Equal("value-1", reopened.GetSecret("conn-1"));
    }

    [Fact]
    public void ChangeMasterPassword_WrongCurrent_Fails()
    {
        var store = NewStore();
        store.SetMasterPassword("old-pw");

        Assert.False(store.ChangeMasterPassword("not-the-pw", "new-pw"));
    }

    [Fact]
    public void Reset_ClearsVaultAndRequiresNewMasterPassword()
    {
        var store = NewStore();
        store.SetMasterPassword("master-pw");
        store.SaveSecret("conn-1", "value-1");

        store.Reset();
        Assert.False(store.IsInitialized);
        Assert.False(store.IsUnlocked);

        store.SetMasterPassword("brand-new");
        Assert.True(store.Unlock("brand-new"));
        Assert.Null(store.GetSecret("conn-1"));
    }
}
