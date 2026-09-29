using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeloShell.Models;
using VeloShell.Services;
using VeloShell.Services.Ssh;

namespace VeloShell.ViewModels;

public partial class SftpBrowserViewModel : ViewModelBase
{
    private readonly ISftpService _sftp;
    private IDialogService? _dialogs;
    private readonly List<SftpEntryItem> _allLoadedItems = new();

    public SftpBrowserViewModel(ISftpService sftp, IDialogService? dialogs = null)
    {
        _sftp = sftp ?? throw new ArgumentNullException(nameof(sftp));
        _dialogs = dialogs;
    }

    public void AttachDialogs(IDialogService dialogs) => _dialogs = dialogs;

    [ObservableProperty] private string _currentPath = "/";
    [ObservableProperty] private string _pathInput = "/";
    [ObservableProperty] private SftpEntryItem? _selectedItem;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _showHiddenFiles;
    [ObservableProperty] private SftpSortColumn _sortColumn = SftpSortColumn.Name;
    [ObservableProperty] private bool _sortAscending = true;

    public ObservableCollection<SftpEntryItem> Items { get; } = new();

    public async Task InitializeAsync()
    {
        try
        {
            var home = await _sftp.GetHomeDirectoryAsync();
            await NavigateToAsync(home);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"初始化目录失败: {ex.Message}";
            await NavigateToAsync("/");
        }
    }

    public async Task NavigateToAsync(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            targetPath = "/";

        targetPath = targetPath.Trim();
        if (!targetPath.StartsWith('/'))
            targetPath = "/" + targetPath;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var list = await _sftp.ListDirectoryAsync(targetPath);
            CurrentPath = targetPath;
            PathInput = targetPath;

            _allLoadedItems.Clear();
            _allLoadedItems.AddRange(list);

            ApplySortingAndFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"无法加载目录「{targetPath}」: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GoPathAsync()
    {
        if (string.IsNullOrWhiteSpace(PathInput)) return;
        await NavigateToAsync(PathInput);
    }

    [RelayCommand]
    private async Task NavigateUpAsync()
    {
        var parent = GetParentDirectory(CurrentPath);
        await NavigateToAsync(parent);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await NavigateToAsync(CurrentPath);
    }

    [RelayCommand]
    private async Task OpenItemAsync(SftpEntryItem? item)
    {
        item ??= SelectedItem;
        if (item == null) return;

        if (item.IsDirectory)
        {
            await NavigateToAsync(item.FullPath);
        }
    }

    [RelayCommand]
    private void ToggleHiddenFiles()
    {
        ShowHiddenFiles = !ShowHiddenFiles;
        ApplySortingAndFilter();
    }

    [RelayCommand]
    private void Sort(string columnStr)
    {
        if (Enum.TryParse<SftpSortColumn>(columnStr, true, out var col))
        {
            if (SortColumn == col)
            {
                SortAscending = !SortAscending;
            }
            else
            {
                SortColumn = col;
                SortAscending = true;
            }
            ApplySortingAndFilter();
        }
    }

    [RelayCommand]
    private async Task NewFolderAsync()
    {
        if (_dialogs == null) return;
        var folderName = await _dialogs.PromptAsync("新建远程文件夹", "新建文件夹");
        if (string.IsNullOrWhiteSpace(folderName)) return;

        try
        {
            var targetPath = CombinePath(CurrentPath, folderName.Trim());
            await _sftp.CreateDirectoryAsync(targetPath);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"新建文件夹失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RenameSelectedAsync()
    {
        if (_dialogs == null || SelectedItem == null) return;
        var newName = await _dialogs.PromptAsync("重命名", SelectedItem.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == SelectedItem.Name) return;

        try
        {
            var newPath = CombinePath(CurrentPath, newName.Trim());
            await _sftp.RenameAsync(SelectedItem.FullPath, newPath);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"重命名失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (_dialogs == null || SelectedItem == null) return;
        var item = SelectedItem;
        var confirmed = await _dialogs.ConfirmAsync(
            "删除确认",
            $"确定删除「{item.Name}」吗？此操作不可撤销。"
        );
        if (!confirmed) return;

        try
        {
            if (item.IsDirectory)
                await _sftp.DeleteDirectoryAsync(item.FullPath);
            else
                await _sftp.DeleteFileAsync(item.FullPath);

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"删除失败: {ex.Message}";
        }
    }

    private void ApplySortingAndFilter()
    {
        var sorted = SftpItemSorter.Sort(_allLoadedItems, SortColumn, SortAscending, ShowHiddenFiles);
        Items.Clear();
        foreach (var it in sorted)
            Items.Add(it);
    }

    public static string GetParentDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
            return "/";

        var trimmed = path.TrimEnd('/');
        int lastSlash = trimmed.LastIndexOf('/');
        if (lastSlash <= 0)
            return "/";

        return trimmed.Substring(0, lastSlash);
    }

    public static string CombinePath(string directory, string name)
    {
        if (directory.EndsWith('/'))
            return directory + name;
        return directory + "/" + name;
    }
}
