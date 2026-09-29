using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace VeloShell.Services;

/// <summary>
/// File-backed <see cref="ICredentialStore"/>. Secrets are AES-256-GCM encrypted
/// under an Argon2id-derived key; only ciphertext is persisted to vault.json.
/// </summary>
public sealed class CredentialStore : ICredentialStore
{
    private static readonly byte[] CanaryBytes = Encoding.UTF8.GetBytes("VeloShell-Vault-v1");

    private readonly string _vaultPath;
    private readonly Argon2Parameters _paramsForNewVault;

    private byte[]? _key;      // derived key; held in memory only while unlocked
    private VaultData? _vault; // loaded vault (ciphertext only)

    public CredentialStore(IAppPaths paths, Argon2Parameters? paramsForNewVault = null)
        : this(paths.VaultFile, paramsForNewVault) { }

    public CredentialStore(string vaultPath, Argon2Parameters? paramsForNewVault = null)
    {
        _vaultPath = vaultPath;
        _paramsForNewVault = paramsForNewVault ?? Argon2Parameters.Default;
    }

    public bool IsInitialized => File.Exists(_vaultPath);
    public bool IsUnlocked => _key is not null;

    public void SetMasterPassword(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        if (IsInitialized)
            throw new InvalidOperationException("A vault already exists; use ChangeMasterPassword.");

        var salt = CryptoService.GenerateSalt();
        var key = CryptoService.DeriveKey(password, salt, _paramsForNewVault);
        _vault = new VaultData
        {
            Salt = Convert.ToBase64String(salt),
            MemoryKib = _paramsForNewVault.MemoryKib,
            Iterations = _paramsForNewVault.Iterations,
            DegreeOfParallelism = _paramsForNewVault.DegreeOfParallelism,
            Canary = Convert.ToBase64String(CryptoService.Encrypt(key, CanaryBytes)),
        };
        _key = key;
        Persist();
    }

    public bool Unlock(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var vault = AtomicJson.Read<VaultData>(_vaultPath)
                    ?? throw new InvalidOperationException("No vault to unlock.");

        var pars = new Argon2Parameters
        {
            MemoryKib = vault.MemoryKib,
            Iterations = vault.Iterations,
            DegreeOfParallelism = vault.DegreeOfParallelism,
        };
        var key = CryptoService.DeriveKey(password, Convert.FromBase64String(vault.Salt), pars);
        try
        {
            var canary = CryptoService.Decrypt(key, Convert.FromBase64String(vault.Canary));
            if (!CryptographicOperations.FixedTimeEquals(canary, CanaryBytes))
            {
                CryptographicOperations.ZeroMemory(key);
                return false;
            }
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(key); // wrong password: GCM authentication failed
            return false;
        }

        Lock();
        _key = key;
        _vault = vault;
        return true;
    }

    public void Lock()
    {
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
        _vault = null;
    }

    public bool ChangeMasterPassword(string currentPassword, string newPassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(newPassword);
        if (!Unlock(currentPassword))
            return false;

        // Decrypt all entries under the current key, then re-encrypt under a new key.
        var plaintexts = new Dictionary<string, byte[]>();
        foreach (var (id, blob) in _vault!.Entries)
            plaintexts[id] = CryptoService.Decrypt(_key!, Convert.FromBase64String(blob));

        var salt = CryptoService.GenerateSalt();
        var newKey = CryptoService.DeriveKey(newPassword, salt, _paramsForNewVault);
        var entries = new Dictionary<string, string>();
        foreach (var (id, pt) in plaintexts)
        {
            entries[id] = Convert.ToBase64String(CryptoService.Encrypt(newKey, pt));
            CryptographicOperations.ZeroMemory(pt);
        }

        _vault = new VaultData
        {
            Salt = Convert.ToBase64String(salt),
            MemoryKib = _paramsForNewVault.MemoryKib,
            Iterations = _paramsForNewVault.Iterations,
            DegreeOfParallelism = _paramsForNewVault.DegreeOfParallelism,
            Canary = Convert.ToBase64String(CryptoService.Encrypt(newKey, CanaryBytes)),
            Entries = entries,
        };
        CryptographicOperations.ZeroMemory(_key!);
        _key = newKey;
        Persist();
        return true;
    }

    public void SaveSecret(string connectionId, string secret)
    {
        EnsureUnlocked();
        ArgumentException.ThrowIfNullOrEmpty(connectionId);
        ArgumentNullException.ThrowIfNull(secret);
        var blob = CryptoService.Encrypt(_key!, Encoding.UTF8.GetBytes(secret));
        _vault!.Entries[connectionId] = Convert.ToBase64String(blob);
        Persist();
    }

    public string? GetSecret(string connectionId)
    {
        EnsureUnlocked();
        if (_vault!.Entries.TryGetValue(connectionId, out var blob))
            return Encoding.UTF8.GetString(CryptoService.Decrypt(_key!, Convert.FromBase64String(blob)));
        return null;
    }

    public void RemoveSecret(string connectionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionId);
        // Dropping ciphertext needs no key, so this works whether locked or unlocked;
        // that keeps connection/folder deletion from leaving orphan secrets behind.
        if (_vault is not null)
        {
            if (_vault.Entries.Remove(connectionId))
                Persist();
            return;
        }

        var vault = AtomicJson.Read<VaultData>(_vaultPath);
        if (vault is not null && vault.Entries.Remove(connectionId))
            AtomicJson.Write(_vaultPath, vault);
    }

    public void Reset()
    {
        Lock();
        if (File.Exists(_vaultPath))
            File.Delete(_vaultPath);
    }

    private void Persist() => AtomicJson.Write(_vaultPath, _vault!);

    private void EnsureUnlocked()
    {
        if (!IsUnlocked)
            throw new InvalidOperationException("Vault is locked; unlock before accessing secrets.");
    }
}
