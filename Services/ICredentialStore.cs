namespace VeloShell.Services;

/// <summary>
/// Encrypted store for sensitive connection credentials (passwords, key
/// passphrases). Plaintext and the derived key never touch disk; the key lives
/// in memory only while unlocked. See specs/credential-store.
/// </summary>
public interface ICredentialStore
{
    /// <summary>True once a master password has been set (a vault exists on disk).</summary>
    bool IsInitialized { get; }

    /// <summary>True while the vault is unlocked and secrets can be read/written.</summary>
    bool IsUnlocked { get; }

    /// <summary>Sets the master password for the first time and creates the vault.</summary>
    void SetMasterPassword(string password);

    /// <summary>Attempts to unlock with the master password. Returns false if wrong.</summary>
    bool Unlock(string password);

    /// <summary>Clears the in-memory key; secrets become unreadable until unlocked again.</summary>
    void Lock();

    /// <summary>Re-encrypts every secret under a new master password. Returns false if the current password is wrong.</summary>
    bool ChangeMasterPassword(string currentPassword, string newPassword);

    /// <summary>Encrypts and stores a secret for the given connection id.</summary>
    void SaveSecret(string connectionId, string secret);

    /// <summary>Decrypts and returns the secret for the connection id, or null if none.</summary>
    string? GetSecret(string connectionId);

    /// <summary>Removes any stored secret for the connection id.</summary>
    void RemoveSecret(string connectionId);

    /// <summary>Deletes the vault and all stored credentials; a new master password must be set afterwards.</summary>
    void Reset();
}
