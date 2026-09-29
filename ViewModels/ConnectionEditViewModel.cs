using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeloShell.Models;

namespace VeloShell.ViewModels;

/// <summary>
/// Backs the new/edit connection dialog. The secret is never pre-filled; leaving
/// it blank keeps any existing stored secret unchanged.
/// </summary>
public partial class ConnectionEditViewModel : ViewModelBase
{
    private readonly Connection _model;
    private readonly bool _isNew;

    public ConnectionEditViewModel(Connection? existing, bool secretAlreadyStored)
    {
        _isNew = existing is null;
        _model = existing ?? new Connection();
        SecretAlreadyStored = secretAlreadyStored;

        _displayName = _model.DisplayName ?? string.Empty;
        _host = _model.Host;
        _port = _model.Port;
        _username = _model.Username;
        _selectedAuthMethod = _model.AuthMethod;
        _privateKeyPath = _model.PrivateKeyPath ?? string.Empty;
        _notes = _model.Notes ?? string.Empty;
    }

    public string Title => _isNew ? "新建连接" : "编辑连接";
    public bool SecretAlreadyStored { get; }
    public AuthMethod[] AuthMethods { get; } = Enum.GetValues<AuthMethod>();

    [ObservableProperty] private string _displayName;
    [ObservableProperty] private string _host;
    [ObservableProperty] private int _port;
    [ObservableProperty] private string _username;
    [ObservableProperty] private AuthMethod _selectedAuthMethod;
    [ObservableProperty] private string _privateKeyPath;
    [ObservableProperty] private string _secret = string.Empty;
    [ObservableProperty] private string _notes;
    [ObservableProperty] private string? _error;

    public bool NeedsSecret => SelectedAuthMethod is AuthMethod.Password or AuthMethod.PrivateKeyWithPassphrase;

    public bool Succeeded { get; private set; }

    /// <summary>The connection with edited values applied (valid only after success).</summary>
    public Connection Result => _model;

    /// <summary>The new secret to store, or null if the user left it unchanged.</summary>
    public string? SecretResult { get; private set; }

    public event EventHandler? CloseRequested;

    partial void OnSelectedAuthMethodChanged(AuthMethod value) => OnPropertyChanged(nameof(NeedsSecret));

    [RelayCommand]
    private void Submit()
    {
        Error = null;
        if (string.IsNullOrWhiteSpace(Host)) { Error = "请填写主机 (Host)"; return; }
        if (string.IsNullOrWhiteSpace(Username)) { Error = "请填写用户名"; return; }
        if (Port is <= 0 or > 65535) { Error = "端口需在 1-65535 之间"; return; }

        _model.DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? null : DisplayName.Trim();
        _model.Host = Host.Trim();
        _model.Port = Port;
        _model.Username = Username.Trim();
        _model.AuthMethod = SelectedAuthMethod;
        _model.PrivateKeyPath = string.IsNullOrWhiteSpace(PrivateKeyPath) ? null : PrivateKeyPath.Trim();
        _model.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes;

        SecretResult = string.IsNullOrEmpty(Secret) ? null : Secret;
        Succeeded = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
