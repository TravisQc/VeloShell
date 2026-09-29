using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeloShell.Models;
using VeloShell.Services;

namespace VeloShell.ViewModels;

/// <summary>Shell view model: owns the connection tree and the toolbar commands.</summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IConnectionStore _connections;
    private readonly ICredentialStore _credentials;
    private IDialogService? _dialogs;

    public MainViewModel(IConnectionStore connections, ICredentialStore credentials)
    {
        _connections = connections;
        _credentials = credentials;
        RebuildTree();
    }

    public ObservableCollection<NodeViewModel> Nodes { get; } = new();

    [ObservableProperty] private NodeViewModel? _selectedNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _locked;

    public string StatusText => !_credentials.IsInitialized
        ? "尚未设置主密码"
        : Locked ? "已锁定" : "已解锁";

    /// <summary>Wires the dialog service after the owner window exists.</summary>
    public void AttachDialogs(IDialogService dialogs) => _dialogs = dialogs;

    /// <summary>On launch, if a vault exists, require the master password before showing secrets.</summary>
    public async Task OnStartupAsync()
    {
        if (_dialogs is not null && _credentials.IsInitialized && !_credentials.IsUnlocked)
            await _dialogs.MasterPasswordAsync(MasterPasswordMode.Unlock);
        Locked = _credentials.IsInitialized && !_credentials.IsUnlocked;
        RebuildTree();
    }

    [RelayCommand]
    private async Task NewConnectionAsync()
    {
        if (_dialogs is null) return;
        var outcome = await _dialogs.EditConnectionAsync(existing: null, secretAlreadyStored: false);
        if (outcome is null) return;

        _connections.AddConnection(outcome.Connection, TargetFolderId());
        await SaveSecretIfProvidedAsync(outcome);
        RebuildTree();
    }

    [RelayCommand]
    private async Task EditConnectionAsync()
    {
        if (_dialogs is null || SelectedNode is not { IsFolder: false, Connection: { } connection }) return;
        var outcome = await _dialogs.EditConnectionAsync(connection, secretAlreadyStored: true);
        if (outcome is null) return;

        _connections.UpdateConnection(outcome.Connection);
        await SaveSecretIfProvidedAsync(outcome);
        RebuildTree();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_dialogs is null || SelectedNode is not { } node) return;

        if (node.IsFolder)
        {
            if (node.Id == ConnectionStore.RootId) return;
            if (!await _dialogs.ConfirmAsync("删除文件夹", $"确定删除文件夹「{node.Label}」及其中的所有连接?此操作不可撤销。"))
                return;
            _connections.RemoveFolder(node.Id);
        }
        else
        {
            if (!await _dialogs.ConfirmAsync("删除连接", $"确定删除连接「{node.Label}」?其保存的凭据也会一并删除。"))
                return;
            _connections.RemoveConnection(node.Id);
        }
        RebuildTree();
    }

    [RelayCommand]
    private async Task NewFolderAsync()
    {
        if (_dialogs is null) return;
        var name = await _dialogs.PromptAsync("新建文件夹", "新文件夹");
        if (string.IsNullOrWhiteSpace(name)) return;
        _connections.AddFolder(name, TargetFolderId());
        RebuildTree();
    }

    [RelayCommand]
    private async Task RenameFolderAsync()
    {
        if (_dialogs is null || SelectedNode is not { IsFolder: true } folder || folder.Id == ConnectionStore.RootId) return;
        var name = await _dialogs.PromptAsync("重命名文件夹", folder.Label);
        if (string.IsNullOrWhiteSpace(name)) return;
        _connections.RenameFolder(folder.Id, name);
        RebuildTree();
    }

    [RelayCommand]
    private async Task ChangeMasterPasswordAsync()
    {
        if (_dialogs is null) return;
        // Change mode itself verifies the current password; before any vault exists we set one.
        var mode = _credentials.IsInitialized ? MasterPasswordMode.Change : MasterPasswordMode.Set;
        await _dialogs.MasterPasswordAsync(mode);
        Locked = _credentials.IsInitialized && !_credentials.IsUnlocked;
    }

    [RelayCommand]
    private void Lock()
    {
        _credentials.Lock();
        Locked = _credentials.IsInitialized;
    }

    /// <summary>Invoked by the view when a connection node is dropped onto a folder node.</summary>
    public void MoveConnection(string connectionId, string targetFolderId)
    {
        _connections.MoveConnection(connectionId, targetFolderId);
        RebuildTree();
    }

    [RelayCommand]
    private async Task MoveSelectedAsync()
    {
        if (_dialogs is null || SelectedNode is not { IsFolder: false, Connection: { } connection }) return;

        var choices = new List<FolderChoice>();
        CollectFolderChoices(_connections.Root, string.Empty, choices);
        var targetFolderId = await _dialogs.PickFolderAsync(choices);
        if (targetFolderId is null) return;

        _connections.MoveConnection(connection.Id, targetFolderId);
        RebuildTree();
    }

    private static void CollectFolderChoices(ConnectionFolder folder, string prefix, List<FolderChoice> into)
    {
        var path = string.IsNullOrEmpty(prefix) ? folder.Name : $"{prefix} / {folder.Name}";
        into.Add(new FolderChoice(folder.Id, path));
        foreach (var sub in folder.Folders)
            CollectFolderChoices(sub, path, into);
    }

    private async Task SaveSecretIfProvidedAsync(ConnectionEditOutcome outcome)
    {
        if (!outcome.SecretProvided) return;
        if (await EnsureCredentialsReadyAsync())
            _credentials.SaveSecret(outcome.Connection.Id, outcome.Secret!);
    }

    // Gate secret access on the vault being ready: unlock it, or set a master password the first time.
    private async Task<bool> EnsureCredentialsReadyAsync()
    {
        if (_credentials.IsUnlocked) return true;
        var mode = _credentials.IsInitialized ? MasterPasswordMode.Unlock : MasterPasswordMode.Set;
        await _dialogs!.MasterPasswordAsync(mode);
        Locked = _credentials.IsInitialized && !_credentials.IsUnlocked;
        return _credentials.IsUnlocked;
    }

    private string TargetFolderId()
        => SelectedNode is { IsFolder: true } folder ? folder.Id : ConnectionStore.RootId;

    private void RebuildTree()
    {
        Nodes.Clear();
        Nodes.Add(BuildFolderNode(_connections.Root));
        OnPropertyChanged(nameof(StatusText));
    }

    private static NodeViewModel BuildFolderNode(ConnectionFolder folder)
    {
        var node = new NodeViewModel(folder);
        foreach (var sub in folder.Folders)
            node.Children.Add(BuildFolderNode(sub));
        foreach (var connection in folder.Connections)
            node.Children.Add(new NodeViewModel(connection));
        return node;
    }
}
