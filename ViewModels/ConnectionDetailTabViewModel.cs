namespace VeloShell.ViewModels;

public sealed class ConnectionDetailTabViewModel : ViewModelBase
{
    public MainViewModel Main { get; }

    public ConnectionDetailTabViewModel(MainViewModel main)
    {
        Main = main;
    }

    public override string Header => "连接管理";
    public override bool CanClose => false;
}
