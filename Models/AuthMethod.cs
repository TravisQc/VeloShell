namespace VeloShell.Models;

/// <summary>How a connection authenticates to the remote host.</summary>
public enum AuthMethod
{
    Password,
    PrivateKey,
    PrivateKeyWithPassphrase,
}
