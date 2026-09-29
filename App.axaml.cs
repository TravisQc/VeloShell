using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VeloShell.Services;
using VeloShell.ViewModels;
using VeloShell.Views;

namespace VeloShell;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var paths = new AppPaths();
            var credentials = new CredentialStore(paths);
            var connections = new ConnectionStore(paths, credentials);
            var mainViewModel = new MainViewModel(connections, credentials);
            var window = new MainWindow { DataContext = mainViewModel };

            mainViewModel.AttachDialogs(new DialogService(window, credentials));
            window.Opened += async (_, _) => await mainViewModel.OnStartupAsync();

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
