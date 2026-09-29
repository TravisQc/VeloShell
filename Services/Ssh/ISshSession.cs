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
    ShellStream? ShellStream { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<string> ExecuteCommandAsync(string commandText, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    Task<ShellStream> CreateShellStreamAsync(
        string terminalName = "xterm-256color",
        uint columns = 80,
        uint rows = 24,
        uint width = 800,
        uint height = 600,
        int bufferSize = 4096,
        CancellationToken cancellationToken = default);

    void SendWindowChange(uint columns, uint rows, uint width, uint height);
}
