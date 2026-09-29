using System;
using System.IO;

namespace VeloShell.Services;

/// <summary>Resolves where VeloShell keeps its local data files.</summary>
public interface IAppPaths
{
    string DataDirectory { get; }
    string ConnectionsFile { get; }
    string VaultFile { get; }
}

/// <summary>
/// Default paths under the OS application-data directory (Windows: %APPDATA%\VeloShell).
/// A directory override is accepted so tests can use a temporary location.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    public AppPaths(string? dataDirectoryOverride = null)
    {
        DataDirectory = dataDirectoryOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VeloShell");
        Directory.CreateDirectory(DataDirectory);
    }

    public string DataDirectory { get; }
    public string ConnectionsFile => Path.Combine(DataDirectory, "connections.json");
    public string VaultFile => Path.Combine(DataDirectory, "vault.json");
}
