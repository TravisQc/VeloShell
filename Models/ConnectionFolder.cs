using System;
using System.Collections.Generic;

namespace VeloShell.Models;

/// <summary>
/// A node in the connection tree. A folder holds child folders and connections,
/// forming the hierarchy shown in the left pane.
/// </summary>
public sealed class ConnectionFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public List<ConnectionFolder> Folders { get; set; } = new();

    public List<Connection> Connections { get; set; } = new();
}
