using System.Security.Cryptography;
using System.Text;
using VeloShell.Services;

namespace VeloShell.Tests;

public class CryptoServiceTests
{
    // Small parameters keep the KDF fast in tests; production uses Argon2Parameters.Default.
    private static readonly Argon2Parameters FastParams = new()
    {
        MemoryKib = 1024,
        Iterations = 1,
        DegreeOfParallelism = 1,
    };

    [Fact]
    public void DeriveKey_SameInputs_ProducesStable32ByteKey()
    {
        var salt = new byte[CryptoService.SaltSize];
        var k1 = CryptoService.DeriveKey("hunter2", salt, FastParams);
        var k2 = CryptoService.DeriveKey("hunter2", salt, FastParams);

        Assert.Equal(CryptoService.KeySize, k1.Length);
        Assert.Equal(k1, k2);
    }

    [Fact]
    public void DeriveKey_DifferentSalt_ProducesDifferentKey()
    {
        var salt1 = new byte[CryptoService.SaltSize];
        var salt2 = new byte[CryptoService.SaltSize];
        salt2[0] = 0x01;

        var k1 = CryptoService.DeriveKey("hunter2", salt1, FastParams);
        var k2 = CryptoService.DeriveKey("hunter2", salt2, FastParams);

        Assert.NotEqual(k1, k2);
    }

    [Fact]
    public void EncryptDecrypt_RoundTrips()
    {
        var key = RandomNumberGenerator.GetBytes(CryptoService.KeySize);
        var plaintext = Encoding.UTF8.GetBytes("s3cr3t-p@ssw0rd");

        var blob = CryptoService.Encrypt(key, plaintext);
        var recovered = CryptoService.Decrypt(key, blob);

        Assert.Equal(plaintext, recovered);
    }

    [Fact]
    public void Decrypt_WrongKey_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(CryptoService.KeySize);
        var wrongKey = RandomNumberGenerator.GetBytes(CryptoService.KeySize);
        var blob = CryptoService.Encrypt(key, Encoding.UTF8.GetBytes("data"));

        Assert.ThrowsAny<CryptographicException>(() => CryptoService.Decrypt(wrongKey, blob));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(CryptoService.KeySize);
        var blob = CryptoService.Encrypt(key, Encoding.UTF8.GetBytes("data"));
        blob[^1] ^= 0xFF; // flip a ciphertext byte

        Assert.ThrowsAny<CryptographicException>(() => CryptoService.Decrypt(key, blob));
    }

    [Fact]
    public void Decrypt_TamperedTag_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(CryptoService.KeySize);
        var blob = CryptoService.Encrypt(key, Encoding.UTF8.GetBytes("data"));
        blob[CryptoService.NonceSize] ^= 0xFF; // flip the first tag byte

        Assert.ThrowsAny<CryptographicException>(() => CryptoService.Decrypt(key, blob));
    }

    [Fact]
    public void Encrypt_UsesUniqueNoncePerCall()
    {
        var key = RandomNumberGenerator.GetBytes(CryptoService.KeySize);
        var plaintext = Encoding.UTF8.GetBytes("data");
        var seen = new HashSet<string>();

        for (var i = 0; i < 100; i++)
        {
            var blob = CryptoService.Encrypt(key, plaintext);
            var nonce = blob.Take(CryptoService.NonceSize).ToArray();
            Assert.True(seen.Add(Convert.ToBase64String(nonce)), "nonce was reused");
        }
    }
}
