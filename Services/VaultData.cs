using System.Collections.Generic;

namespace VeloShell.Services;

/// <summary>
/// On-disk shape of vault.json. Holds only KDF parameters and ciphertext; it
/// never contains the master password or any plaintext secret.
/// </summary>
public sealed class VaultData
{
    public int Version { get; set; } = 1;

    /// <summary>Base64 Argon2id salt.</summary>
    public string Salt { get; set; } = string.Empty;

    public int MemoryKib { get; set; }
    public int Iterations { get; set; }
    public int DegreeOfParallelism { get; set; }

    /// <summary>Base64 AES-GCM blob of a known constant, used to verify the master password.</summary>
    public string Canary { get; set; } = string.Empty;

    /// <summary>Connection id -> base64 AES-GCM blob of that connection's secret.</summary>
    public Dictionary<string, string> Entries { get; set; } = new();
}
