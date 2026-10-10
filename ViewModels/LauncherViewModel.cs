namespace VeloShell.ViewModels;

/// <summary>
/// The launcher surface opened by the title-bar [+] button: hosts the connection
/// tree and the connection/credential management actions. It is not a session tab
/// and carries no status dot. The management commands and the connection tree
/// (<see cref="MainViewModel.Nodes"/>) live on <see cref="MainViewModel"/>; this
/// view model only exposes them to the launcher view, delegating to the existing
/// connection-management and credential-store capabilities without changing them.
/// </summary>
public sealed class LauncherViewModel : ViewModelBase
{
    public MainViewModel Main { get; }

    public LauncherViewModel(MainViewModel main)
    {
        Main = main;
    }
}
