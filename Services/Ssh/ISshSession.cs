using System;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using VeloShell.Models;

namespace VeloShell.Services.Ssh;

/// <summary>
/// Represents an active or pending SSH and SFTP session to a remote host.
/// </summary>
public interface ISshSession : IAsyncDisposable, IDisposable
{
    string Id { get; }
    Connection Connection { get; }
    SessionState State { get; }
    string? LastErrorMessage { get; }

    event EventHandler<SessionState>? StateChanged;

    bool IsConnected { get; }
    SshClient? SshClient { get; }
    SftpClient? SftpClient { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<string> ExecuteCommandAsync(string commandText, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
