using System;
using System.Text.Json.Serialization;

namespace VeloShell.Models;

/// <summary>
/// A saved SSH connection definition. Holds no plaintext secret: passwords and
/// key passphrases live in the credential store, keyed by <see cref="Id"/>.
/// </summary>
public sealed class Connection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string? DisplayName { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 22;

    public string Username { get; set; } = string.Empty;

    public AuthMethod AuthMethod { get; set; } = AuthMethod.Password;

    /// <summary>Path to the private key file (for key-based auth). A reference, not the key itself.</summary>
    public string? PrivateKeyPath { get; set; }

    public string? Notes { get; set; }

    /// <summary>Display label: the display name if set, otherwise <c>user@host:port</c>.</summary>
    [JsonIgnore]
    public string Label => string.IsNullOrWhiteSpace(DisplayName)
        ? $"{Username}@{Host}:{Port}"
        : DisplayName!;
}
