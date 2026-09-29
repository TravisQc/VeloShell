using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using VeloShell.Models;
using VeloShell.ViewModels;
using VeloShell.Views;

namespace VeloShell.Services;

/// <summary>Avalonia implementation of <see cref="IDialogService"/> that shows windows modally over an owner.</summary>
public sealed class DialogService : IDialogService
{
    private readonly Window _owner;
    private readonly ICredentialStore _credentials;

    public DialogService(Window owner, ICredentialStore credentials)
    {
        _owner = owner;
        _credentials = credentials;
    }

    public async Task<MasterPasswordOutcome> MasterPasswordAsync(MasterPasswordMode mode)
    {
        var vm = new MasterPasswordViewModel(_credentials, mode);
        await new MasterPasswordWindow { DataContext = vm }.ShowDialog(_owner);
        return new MasterPasswordOutcome(vm.Succeeded, vm.ResetRequested);
    }

    public async Task<ConnectionEditOutcome?> EditConnectionAsync(Connection? existing, bool secretAlreadyStored)
    {
        var vm = new ConnectionEditViewModel(existing, secretAlreadyStored);
        await new ConnectionEditWindow { DataContext = vm }.ShowDialog(_owner);
        return vm.Succeeded ? new ConnectionEditOutcome(vm.Result, vm.SecretResult, vm.SecretResult is not null) : null;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var result = false;
        var dialog = NewDialog(title);
        var yes = new Button { Content = "确定", IsDefault = true, Classes = { "accent" } };
        var no = new Button { Content = "取消", IsCancel = true };
        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click += (_, _) => { result = false; dialog.Close(); };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                ButtonsRow(no, yes),
            },
        };
        await dialog.ShowDialog(_owner);
        return result;
    }

    public async Task<string?> PromptAsync(string title, string? initial = null)
    {
        string? result = null;
        var box = new TextBox { Text = initial ?? string.Empty };
        var dialog = NewDialog(title);
        var ok = new Button { Content = "确定", IsDefault = true, Classes = { "accent" } };
        var cancel = new Button { Content = "取消", IsCancel = true };
        ok.Click += (_, _) => { result = string.IsNullOrWhiteSpace(box.Text) ? null : box.Text.Trim(); dialog.Close(); };
        cancel.Click += (_, _) => { result = null; dialog.Close(); };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children = { new TextBlock { Text = title }, box, ButtonsRow(cancel, ok) },
        };
        await dialog.ShowDialog(_owner);
        return result;
    }

    public async Task<string?> PickFolderAsync(IReadOnlyList<FolderChoice> folders)
    {
        string? result = null;
        var list = new ListBox
        {
            ItemsSource = folders,
            SelectedIndex = folders.Count > 0 ? 0 : -1,
            MaxHeight = 260,
            ItemTemplate = new FuncDataTemplate<FolderChoice>((choice, _) => new TextBlock { Text = choice.Path }),
        };
        var dialog = NewDialog("移动到文件夹");
        var ok = new Button { Content = "移动到此", IsDefault = true, Classes = { "accent" } };
        var cancel = new Button { Content = "取消", IsCancel = true };
        ok.Click += (_, _) => { result = (list.SelectedItem as FolderChoice)?.Id; dialog.Close(); };
        cancel.Click += (_, _) => { result = null; dialog.Close(); };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children = { new TextBlock { Text = "选择目标文件夹:" }, list, ButtonsRow(cancel, ok) },
        };
        await dialog.ShowDialog(_owner);
        return result;
    }

    private static Window NewDialog(string title) => new()
    {
        Title = title,
        Width = 340,
        SizeToContent = SizeToContent.Height,
        CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
    };

    private static StackPanel ButtonsRow(params Control[] buttons)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        foreach (var b in buttons)
            row.Children.Add(b);
        return row;
    }
}
