using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Renci.SshNet;
using VeloShell.Models;
using VeloShell.Services;
using VeloShell.Services.Ssh;

namespace VeloShell.ViewModels;

public partial class TerminalViewModel : ViewModelBase, IDisposable
{
    private readonly ISshSession _session;
    private readonly CommandHistoryManager _historyManager;
    private ShellStream? _shellStream;
    private CancellationTokenSource? _streamCts;
    private readonly StringBuilder _currentCommandLine = new();
    private bool _disposed;

    public ISshSession Session => _session;
    public CommandHistoryManager HistoryManager => _historyManager;

    [ObservableProperty] private bool _isHistoryPaneOpen;
    [ObservableProperty] private string _searchHistoryQuery = string.Empty;
    [ObservableProperty] private bool _isTerminalActive;
    [ObservableProperty] private string _statusText = "终端准备中...";

    public ObservableCollection<CommandHistoryItem> HistoryItems { get; } = new();

    public event EventHandler<ReadOnlyMemory<byte>>? OutputReceived;
    public event EventHandler<string>? CommandInsertRequested;

    public TerminalViewModel(ISshSession session, CommandHistoryManager? historyManager = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _historyManager = historyManager ?? new CommandHistoryManager();

        _historyManager.CommandAdded += (_, _) => RefreshHistoryItems();
        _historyManager.HistoryCleared += (_, _) => RefreshHistoryItems();

        RefreshHistoryItems();
    }

    partial void OnSearchHistoryQueryChanged(string value)
    {
        RefreshHistoryItems();
    }

    private void RefreshHistoryItems()
    {
        void Update()
        {
            HistoryItems.Clear();
            var results = _historyManager.Search(SearchHistoryQuery);
            foreach (var item in results)
            {
                HistoryItems.Add(item);
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Dispatcher.UIThread.Post(Update);
        }
    }

    public async Task StartShellAsync(
        int cols = 80,
        int rows = 24,
        int width = 800,
        int height = 600,
        CancellationToken cancellationToken = default)
    {
        if (!_session.IsConnected)
        {
            StatusText = "SSH 未连接";
            return;
        }

        try
        {
            StatusText = "正在启动 Shell...";
            _streamCts?.Cancel();
            _streamCts?.Dispose();
            _streamCts = new CancellationTokenSource();

            _shellStream = await _session.CreateShellStreamAsync(
                "xterm-256color",
                (uint)Math.Max(1, cols),
                (uint)Math.Max(1, rows),
                (uint)Math.Max(1, width),
                (uint)Math.Max(1, height),
                bufferSize: 4096,
                cancellationToken: cancellationToken);

            IsTerminalActive = true;
            StatusText = "终端已就绪";

            StartReadLoop(_shellStream, _streamCts.Token);
        }
        catch (Exception ex)
        {
            IsTerminalActive = false;
            StatusText = $"启动 Shell 失败: {ex.Message}";
        }
    }

    private void StartReadLoop(ShellStream stream, CancellationToken token)
    {
        Task.Run(async () =>
        {
            var buffer = new byte[4096];
            try
            {
                while (!token.IsCancellationRequested && _session.IsConnected)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (read <= 0) break;

                    var slice = new byte[read];
                    Array.Copy(buffer, slice, read);

                    OutputReceived?.Invoke(this, new ReadOnlyMemory<byte>(slice));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    StatusText = $"数据接收中断: {ex.Message}";
                });
            }
            finally
            {
                Dispatcher.UIThread.Post(() =>
                {
                    IsTerminalActive = false;
                    StatusText = "终端连接已关闭";
                });
            }
        }, token);
    }

    public void OnUserInput(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Process command history tracking on Enter (\r or \n)
        foreach (char c in text)
        {
            if (c == '\r' || c == '\n')
            {
                string cmd = _currentCommandLine.ToString();
                _currentCommandLine.Clear();
                if (!string.IsNullOrWhiteSpace(cmd))
                {
                    _historyManager.AddCommand(cmd, _session.Connection?.Host);
                }
            }
            else if (c == '\b' || c == 0x7f)
            {
                if (_currentCommandLine.Length > 0)
                {
                    _currentCommandLine.Length--;
                }
            }
            else if (!char.IsControl(c))
            {
                _currentCommandLine.Append(c);
            }
        }

        if (_shellStream == null) return;

        // Send to remote shell
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            _shellStream.Write(bytes, 0, bytes.Length);
            _shellStream.Flush();
        }
        catch (Exception ex)
        {
            StatusText = $"写入终端失败: {ex.Message}";
        }
    }

    public void OnTerminalResized(int cols, int rows, int width, int height)
    {
        _session.SendWindowChange((uint)cols, (uint)rows, (uint)width, (uint)height);
    }

    [RelayCommand]
    private void ToggleHistory()
    {
        IsHistoryPaneOpen = !IsHistoryPaneOpen;
    }

    [RelayCommand]
    private void InsertCommand(CommandHistoryItem? item)
    {
        if (item != null && !string.IsNullOrWhiteSpace(item.CommandText))
        {
            CommandInsertRequested?.Invoke(this, item.CommandText);
        }
    }

    [RelayCommand]
    private void ClearHistory()
    {
        _historyManager.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _shellStream?.Dispose();
        _shellStream = null;
    }
}
