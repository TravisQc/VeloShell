using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VeloShell.Models;

namespace VeloShell.Services.Ssh;

public interface ISftpService
{
    string CurrentDirectory { get; }
    Task<string> GetHomeDirectoryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SftpEntryItem>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default);
    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken = default);
    Task RenameAsync(string oldPath, string newPath, CancellationToken cancellationToken = default);
}
