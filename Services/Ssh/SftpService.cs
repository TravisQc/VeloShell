using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet.Sftp;
using VeloShell.Models;

namespace VeloShell.Services.Ssh;

public class SftpService : ISftpService
{
    private readonly ISshSession _session;
    private string _currentDirectory = "/";

    public string CurrentDirectory => _currentDirectory;

    public SftpService(ISshSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    private Renci.SshNet.SftpClient GetClient()
    {
        if (!_session.IsConnected || _session.SftpClient == null)
            throw new InvalidOperationException("SFTP 未连接，无法执行文件操作");
        return _session.SftpClient;
    }

    public async Task<string> GetHomeDirectoryAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var client = GetClient();
            var dir = client.WorkingDirectory;
            _currentDirectory = string.IsNullOrWhiteSpace(dir) ? "/" : dir;
            return _currentDirectory;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SftpEntryItem>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var client = GetClient();
            string targetPath = string.IsNullOrWhiteSpace(path) ? client.WorkingDirectory : path.Trim();

            // Normalize path
            if (!targetPath.StartsWith('/'))
                targetPath = "/" + targetPath;

            var files = client.ListDirectory(targetPath);
            _currentDirectory = targetPath;

            var result = new List<SftpEntryItem>();
            foreach (var f in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (f.Name == "." || f.Name == "..")
                    continue;

                result.Add(MapToEntryItem(f));
            }

            return (IReadOnlyList<SftpEntryItem>)result;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            var client = GetClient();
            client.CreateDirectory(path);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            var client = GetClient();
            client.DeleteFile(path);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            var client = GetClient();
            client.DeleteDirectory(path);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task RenameAsync(string oldPath, string newPath, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            var client = GetClient();
            client.RenameFile(oldPath, newPath);
        }, cancellationToken).ConfigureAwait(false);
    }

    public static SftpEntryItem MapToEntryItem(ISftpFile file)
    {
        var itemType = file.IsDirectory ? SftpItemType.Directory :
                       file.IsSymbolicLink ? SftpItemType.SymbolicLink :
                       SftpItemType.File;

        return new SftpEntryItem
        {
            Name = file.Name,
            FullPath = file.FullName,
            ItemType = itemType,
            Length = file.Length,
            LastWriteTime = file.LastWriteTime,
            Permissions = FormatPermissions(file)
        };
    }

    public static string FormatPermissions(ISftpFile file)
    {
        char typeChar = file.IsDirectory ? 'd' : (file.IsSymbolicLink ? 'l' : '-');
        char uR = file.OwnerCanRead ? 'r' : '-';
        char uW = file.OwnerCanWrite ? 'w' : '-';
        char uX = file.OwnerCanExecute ? 'x' : '-';
        char gR = file.GroupCanRead ? 'r' : '-';
        char gW = file.GroupCanWrite ? 'w' : '-';
        char gX = file.GroupCanExecute ? 'x' : '-';
        char oR = file.OthersCanRead ? 'r' : '-';
        char oW = file.OthersCanWrite ? 'w' : '-';
        char oX = file.OthersCanExecute ? 'x' : '-';

        return $"{typeChar}{uR}{uW}{uX}{gR}{gW}{gX}{oR}{oW}{oX}";
    }
}
