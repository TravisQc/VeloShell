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
    [NotifyPropertyChangedFor(nameof(Health))]
    [NotifyPropertyChangedFor(nameof(IsHealthy))]
    [NotifyPropertyChangedFor(nameof(IsWarning))]
    [NotifyPropertyChangedFor(nameof(IsError))]
    [NotifyPropertyChangedFor(nameof(IsBreathing))]
    private SessionState _state = SessionState.Disconnected;

    [ObservableProperty] private string? _errorMessage;

    // Per-session navigation mode (left rail). Defaults to Terminal; kept per session.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTerminalMode))]
    [NotifyPropertyChangedFor(nameof(IsHostMode))]
    [NotifyPropertyChangedFor(nameof(IsSettingsMode))]
    private SessionMode _currentMode = SessionMode.Terminal;

    // "Connected but degraded" input point (high load / packet loss). The signal
    // source is out of scope for this layer; when set true a connected session
    // classifies as Warning. Lights up once a later change supplies the signal.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Health))]
    [NotifyPropertyChangedFor(nameof(IsHealthy))]
    [NotifyPropertyChangedFor(nameof(IsWarning))]
    [NotifyPropertyChangedFor(nameof(IsError))]
    private bool _isDegraded;

    public bool IsConnected => State == SessionState.Connected;
    public bool IsConnecting => State == SessionState.Connecting;
    public bool IsDisconnectedOrError => State is SessionState.Disconnected or SessionState.Error;

    public bool IsTerminalMode => CurrentMode == SessionMode.Terminal;
    public bool IsHostMode => CurrentMode == SessionMode.Host;
    public bool IsSettingsMode => CurrentMode == SessionMode.Settings;

    /// <summary>
    /// Health classification driving the tab status dot. A connected session is
    /// Healthy (or Warning when degraded); every other state is Error. Connecting
    /// and Disconnecting are transitional and classify as Error (red, no breathing).
    /// </summary>
    public SessionHealth Health => State == SessionState.Connected
        ? (IsDegraded ? SessionHealth.Warning : SessionHealth.Healthy)
        : SessionHealth.Error;

    public bool IsHealthy => Health == SessionHealth.Healthy;
    public bool IsWarning => Health == SessionHealth.Warning;
    public bool IsError => Health == SessionHealth.Error;

    // The dot "breathes" only while the session is alive (connected).
    public bool IsBreathing => State == SessionState.Connected;

    // Status-bar fields with no data source yet; reserved so the slots light up
    // once a later change supplies them (graceful degradation: null = hidden).
    public string? Latency => null;
    public string? Encoding => null;

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

    /// <summary>Switches this session's current navigation mode (left rail).</summary>
    [RelayCommand]
    private void SetMode(SessionMode mode)
    {
        CurrentMode = mode;
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
