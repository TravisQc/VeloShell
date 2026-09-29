using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeloShell.Services;

namespace VeloShell.ViewModels;

public enum MasterPasswordMode
{
    Set,
    Unlock,
    Change,
}

/// <summary>
/// Drives the set / unlock / change master-password dialog. Talks directly to
/// the credential store; the view only binds and closes on <see cref="CloseRequested"/>.
/// </summary>
public partial class MasterPasswordViewModel : ViewModelBase
{
    private readonly ICredentialStore _credentials;

    public MasterPasswordViewModel(ICredentialStore credentials, MasterPasswordMode mode)
    {
        _credentials = credentials;
        Mode = mode;
    }

    public MasterPasswordMode Mode { get; }

    public string Title => Mode switch
    {
        MasterPasswordMode.Set => "设置主密码",
        MasterPasswordMode.Unlock => "解锁凭据库",
        MasterPasswordMode.Change => "更改主密码",
        _ => "主密码",
    };

    public bool ShowCurrent => Mode == MasterPasswordMode.Change;
    public bool ShowConfirm => Mode is MasterPasswordMode.Set or MasterPasswordMode.Change;
    public bool ShowResetHint => Mode == MasterPasswordMode.Unlock;

    [ObservableProperty] private string _currentPassword = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string? _error;

    /// <summary>True once the operation succeeded; the caller reads this after the dialog closes.</summary>
    public bool Succeeded { get; private set; }

    /// <summary>True when the user reset the vault instead of unlocking.</summary>
    public bool ResetRequested { get; private set; }

    public event EventHandler? CloseRequested;

    [RelayCommand]
    private void Submit()
    {
        Error = null;
        switch (Mode)
        {
            case MasterPasswordMode.Set:
                if (!ValidateNewPassword()) return;
                _credentials.SetMasterPassword(Password);
                break;
            case MasterPasswordMode.Unlock:
                if (string.IsNullOrEmpty(Password)) { Error = "请输入主密码"; return; }
                if (!_credentials.Unlock(Password)) { Error = "主密码错误"; return; }
                break;
            case MasterPasswordMode.Change:
                if (!ValidateNewPassword()) return;
                if (!_credentials.ChangeMasterPassword(CurrentPassword, Password))
                {
                    Error = "当前主密码错误";
                    return;
                }
                break;
        }

        Succeeded = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ResetVault()
    {
        _credentials.Reset();
        ResetRequested = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool ValidateNewPassword()
    {
        if (string.IsNullOrEmpty(Password)) { Error = "请输入主密码"; return false; }
        if (Password.Length < 8) { Error = "主密码至少 8 位"; return false; }
        if (Password != ConfirmPassword) { Error = "两次输入的主密码不一致"; return false; }
        return true;
    }
}
