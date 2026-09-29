using Avalonia.Controls;
using VeloShell.ViewModels;

namespace VeloShell.Views;

public partial class MasterPasswordWindow : Window
{
    public MasterPasswordWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MasterPasswordViewModel vm)
                vm.CloseRequested += (_, _) => Close();
        };
    }
}
