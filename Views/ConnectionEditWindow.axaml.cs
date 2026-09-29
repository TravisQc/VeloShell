using Avalonia.Controls;
using VeloShell.ViewModels;

namespace VeloShell.Views;

public partial class ConnectionEditWindow : Window
{
    public ConnectionEditWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ConnectionEditViewModel vm)
                vm.CloseRequested += (_, _) => Close();
        };
    }
}
