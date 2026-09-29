using System;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;
using VeloShell.Models;

namespace VeloShell.Services.Ssh;

public class SshSession : ISshSession, IDisposable
{
    private readonly SshSessionConfig _config;
    private readonly Func<ConnectionInfo, SshClient>? _sshClientFactory;
    private readonly Func<ConnectionInfo, SftpClient>? _sftpClientFactory;

    private SshClient? _sshClient;
    private SftpClient? _sftpClient;
    private bool _disposed;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public Connection Connection { get; }
    public SshSessionConfig Config => _config;

    public SessionState State { get; private set; } = SessionState.Disconnected;
    public string? LastErrorMessage { get; private set; }

    public event EventHandler<SessionState>? StateChanged;

    public bool IsConnected => State == SessionState.Connected && (_sshClient?.IsConnected == true);
    public SshClient? SshClient => _sshClient;
    public SftpClient? SftpClient => _sftpClient;

    public SshSession(
        Connection connection,
        string? secret,
        Func<ConnectionInfo, SshClient>? sshClientFactory = null,
        Func<ConnectionInfo, SftpClient>? sftpClientFactory = null)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _config = SshSessionConfig.FromConnection(connection, secret);
        _sshClientFactory = sshClientFactory;
        _sftpClientFactory = sftpClientFactory;
    }

    public SshSession(
        Connection connection,
        SshSessionConfig config,
        Func<ConnectionInfo, SshClient>? sshClientFactory = null,
        Func<ConnectionInfo, SftpClient>? sftpClientFactory = null)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _sshClientFactory = sshClientFactory;
        _sftpClientFactory = sftpClientFactory;
    }

    private void SetState(SessionState newState, string? errorMessage = null)
    {
        State = newState;
        if (errorMessage != null)
            LastErrorMessage = errorMessage;
        else if (newState == SessionState.Connected)
            LastErrorMessage = null;

        StateChanged?.Invoke(this, newState);
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected) return;

        SetState(SessionState.Connecting);

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var connInfo = _config.CreateConnectionInfo();
                _sshClient = _sshClientFactory != null ? _sshClientFactory(connInfo) : new SshClient(connInfo);
                _sftpClient = _sftpClientFactory != null ? _sftpClientFactory(connInfo) : new SftpClient(connInfo);

                _sshClient.KeepAliveInterval = _config.KeepAliveInterval;
                _sftpClient.KeepAliveInterval = _config.KeepAliveInterval;

                _sshClient.ErrorOccurred += OnClientError;
                _sftpClient.ErrorOccurred += OnClientError;

                cancellationToken.ThrowIfCancellationRequested();
                _sshClient.Connect();

                cancellationToken.ThrowIfCancellationRequested();
                _sftpClient.Connect();
            }, cancellationToken).ConfigureAwait(false);

            SetState(SessionState.Connected);
        }
        catch (OperationCanceledException)
        {
            CleanupClients();
            SetState(SessionState.Disconnected, "连接已取消");
            throw;
        }
        catch (Exception ex)
        {
            CleanupClients();
            SetState(SessionState.Error, ex.Message);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (State == SessionState.Disconnected && _sshClient == null && _sftpClient == null)
            return;

        SetState(SessionState.Disconnecting);

        await Task.Run(() =>
        {
            CleanupClients();
        }).ConfigureAwait(false);

        SetState(SessionState.Disconnected);
    }

    public async Task<string> ExecuteCommandAsync(string commandText, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _sshClient == null)
            throw new InvalidOperationException("SSH 会话未连接，无法执行命令");

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var cmd = _sshClient.CreateCommand(commandText);
            cmd.CommandTimeout = timeout ?? TimeSpan.FromSeconds(5);

            var result = cmd.Execute();
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }, cancellationToken).ConfigureAwait(false);
    }

    private void OnClientError(object? sender, ExceptionEventArgs e)
    {
        SetState(SessionState.Error, e.Exception.Message);
    }

    private void CleanupClients()
    {
        if (_sftpClient != null)
        {
            try
            {
                _sftpClient.ErrorOccurred -= OnClientError;
                if (_sftpClient.IsConnected)
                    _sftpClient.Disconnect();
                _sftpClient.Dispose();
            }
            catch { /* Ignore cleanup errors */ }
            finally { _sftpClient = null; }
        }

        if (_sshClient != null)
        {
            try
            {
                _sshClient.ErrorOccurred -= OnClientError;
                if (_sshClient.IsConnected)
                    _sshClient.Disconnect();
                _sshClient.Dispose();
            }
            catch { /* Ignore cleanup errors */ }
            finally { _sshClient = null; }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CleanupClients();
        SetState(SessionState.Disconnected);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
