using System;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace VeloShell.Services;

/// <summary>
/// Stateless cryptographic primitives: Argon2id key derivation and
/// AES-256-GCM authenticated encryption. Holds no secrets of its own.
/// </summary>
public static class CryptoService
{
    /// <summary>Derived key length in bytes (256-bit key for AES-256).</summary>
    public const int KeySize = 32;

    /// <summary>Salt length in bytes.</summary>
    public const int SaltSize = 16;

    /// <summary>AES-GCM nonce length in bytes (96-bit, the recommended size).</summary>
    public const int NonceSize = 12;

    /// <summary>AES-GCM authentication tag length in bytes.</summary>
    public const int TagSize = 16;

    /// <summary>Generates a cryptographically random salt.</summary>
    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    /// <summary>Derives a 32-byte key from the master password and salt via Argon2id.</summary>
    public static byte[] DeriveKey(string password, byte[] salt, Argon2Parameters parameters)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(parameters);

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parameters.DegreeOfParallelism,
            Iterations = parameters.Iterations,
            MemorySize = parameters.MemoryKib,
        };
        return argon2.GetBytes(KeySize);
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> with AES-256-GCM under a fresh random
    /// nonce. Returns a self-describing blob laid out as nonce(12) || tag(16) || ciphertext.
    /// </summary>
    public static byte[] Encrypt(byte[] key, byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        var blob = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, blob, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, blob, NonceSize + TagSize, ciphertext.Length);
        return blob;
    }

    /// <summary>
    /// Decrypts a blob produced by <see cref="Encrypt"/>. Throws a
    /// <see cref="CryptographicException"/> if authentication fails (wrong key or
    /// tampered data); never returns an unauthenticated plaintext.
    /// </summary>
    public static byte[] Decrypt(byte[] key, byte[] blob)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(blob);
        if (blob.Length < NonceSize + TagSize)
            throw new ArgumentException("Encrypted blob is too short.", nameof(blob));

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertext = new byte[blob.Length - NonceSize - TagSize];
        Buffer.BlockCopy(blob, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(blob, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(blob, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        return plaintext;
    }
}
