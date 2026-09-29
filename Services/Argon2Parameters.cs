namespace VeloShell.Services;

/// <summary>
/// Argon2id key-derivation parameters. Persisted alongside each vault so the
/// cost can be raised later without breaking existing data (see design.md D2).
/// </summary>
public sealed class Argon2Parameters
{
    /// <summary>Memory cost in kibibytes (KiB). OWASP baseline is ~19 MiB.</summary>
    public int MemoryKib { get; init; } = 19456;

    /// <summary>Number of iterations (time cost).</summary>
    public int Iterations { get; init; } = 2;

    /// <summary>Degree of parallelism (lanes).</summary>
    public int DegreeOfParallelism { get; init; } = 1;

    /// <summary>Production defaults following the OWASP Argon2id baseline.</summary>
    public static Argon2Parameters Default => new();
}
