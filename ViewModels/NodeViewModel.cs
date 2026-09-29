using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VeloShell.Models;

namespace VeloShell.ViewModels;

/// <summary>A node in the connection tree: either a folder (with children) or a connection (leaf).</summary>
public partial class NodeViewModel : ViewModelBase
{
    public NodeViewModel(ConnectionFolder folder)
    {
        IsFolder = true;
        Id = folder.Id;
        Folder = folder;
        _label = folder.Name;
    }

    public NodeViewModel(Connection connection)
    {
        IsFolder = false;
        Id = connection.Id;
        Connection = connection;
        _label = connection.Label;
    }

    public bool IsFolder { get; }
    public string Id { get; }
    public ConnectionFolder? Folder { get; }
    public Connection? Connection { get; }

    public ObservableCollection<NodeViewModel> Children { get; } = new();

    [ObservableProperty] private string _label;
}
