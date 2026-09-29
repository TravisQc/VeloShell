using Avalonia.Controls;
using Avalonia.Input;
using VeloShell.ViewModels;

namespace VeloShell.Views;

public partial class SftpBrowserView : UserControl
{
    public SftpBrowserView()
    {
        InitializeComponent();
    }

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is SftpBrowserViewModel vm && vm.SelectedItem != null)
        {
            _ = vm.OpenItemCommand.ExecuteAsync(vm.SelectedItem);
        }
    }
}
