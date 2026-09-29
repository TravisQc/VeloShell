using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeloShell.Models;
using VeloShell.Services;
using VeloShell.Services.Ssh;

namespace VeloShell.ViewModels;

public partial class SessionViewModel : ViewModelBase, IAsyncDisposable, IDisposable
{
    private bool _disposed;

    public ISshSession Session { get; }
    public Connection Connection => Session.Connection;
    public string Id => Session.Id;
    public string Title => Connection.Label;
    public override string Header => Title;
    public override bool CanClose => true;

    public DashboardViewModel Dashboard { get; }
    public SftpBrowserViewModel Sftp { get; }
    public TerminalViewModel Terminal { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyPropertyChangedFor(nameof(IsConnecting))]
    [NotifyPropertyChangedFor(nameof(IsDisconnectedOrError))]
    private SessionState _state = SessionState.Disconnected;

    [ObservableProperty] private string? _errorMessage;

    public bool IsConnected => State == SessionState.Connected;
    public bool IsConnecting => State == SessionState.Connecting;
    public bool IsDisconnectedOrError => State is SessionState.Disconnected or SessionState.Error;

    public string StatusText => State switch
    {
        SessionState.Connecting => "正在连接...",
        SessionState.Connected => "已连接",
        SessionState.Disconnecting => "断开中...",
        SessionState.Disconnected => "已断开",
        SessionState.Error => string.IsNullOrWhiteSpace(ErrorMessage) ? "连接错误" : $"连接失败: {ErrorMessage}",
        _ => "未知状态"
    };

    public event EventHandler? CloseRequested;

    public SessionViewModel(
        ISshSession session,
        ISystemMetricsCollector collector,
        ISftpService sftp,
        IDialogService? dialogs = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Dashboard = new DashboardViewModel(collector);
        Sftp = new SftpBrowserViewModel(sftp, dialogs);
        Terminal = new TerminalViewModel(session);

        Session.StateChanged += OnSessionStateChanged;
    }

    public void AttachDialogs(IDialogService dialogs)
    {
        Sftp.AttachDialogs(dialogs);
    }

    public async Task StartAsync()
    {
        ErrorMessage = null;
        try
        {
            await Session.ConnectAsync();
            Dashboard.StartMonitoring();
            await Sftp.InitializeAsync();
            _ = Terminal.StartShellAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ReconnectAsync()
    {
        ErrorMessage = null;
        try
        {
            await Session.DisconnectAsync();
            await StartAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await Session.DisconnectAsync();
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSessionStateChanged(object? sender, SessionState newState)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            State = newState;
            ErrorMessage = Session.LastErrorMessage;
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Session.StateChanged -= OnSessionStateChanged;
        Terminal.Dispose();
        Dashboard.Dispose();
        Session.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        Session.StateChanged -= OnSessionStateChanged;
        Terminal.Dispose();
        Dashboard.Dispose();
        await Session.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
