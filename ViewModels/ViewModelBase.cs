using CommunityToolkit.Mvvm.ComponentModel;

namespace VeloShell.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public virtual string Header => string.Empty;
    public virtual bool CanClose => false;
}