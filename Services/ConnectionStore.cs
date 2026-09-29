using System;
using System.Collections.Generic;
using System.Linq;
using VeloShell.Models;

namespace VeloShell.Services;

/// <summary>File-backed <see cref="IConnectionStore"/> persisting the tree to connections.json.</summary>
public sealed class ConnectionStore : IConnectionStore
{
    public const string RootId = "root";

    private readonly string _path;
    private readonly ICredentialStore _credentials;

    public ConnectionFolder Root { get; private set; } = CreateRoot();

    public ConnectionStore(IAppPaths paths, ICredentialStore credentials)
        : this(paths.ConnectionsFile, credentials) { }

    public ConnectionStore(string path, ICredentialStore credentials)
    {
        _path = path;
        _credentials = credentials;
        Load();
    }

    private static ConnectionFolder CreateRoot() => new() { Id = RootId, Name = "所有连接" };

    public void Load() => Root = AtomicJson.Read<ConnectionFolder>(_path) ?? CreateRoot();

    public void Save() => AtomicJson.Write(_path, Root);

    public void AddConnection(Connection connection, string folderId)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var folder = FindFolder(Root, folderId) ?? throw FolderNotFound(folderId);
        folder.Connections.Add(connection);
        Save();
    }

    public void UpdateConnection(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var located = LocateConnection(Root, connection.Id) ?? throw ConnectionNotFound(connection.Id);
        located.parent.Connections[located.index] = connection;
        Save();
    }

    public void RemoveConnection(string connectionId)
    {
        var located = LocateConnection(Root, connectionId) ?? throw ConnectionNotFound(connectionId);
        located.parent.Connections.RemoveAt(located.index);
        _credentials.RemoveSecret(connectionId); // cascade: drop the associated secret
        Save();
    }

    public void MoveConnection(string connectionId, string targetFolderId)
    {
        var located = LocateConnection(Root, connectionId) ?? throw ConnectionNotFound(connectionId);
        var target = FindFolder(Root, targetFolderId) ?? throw FolderNotFound(targetFolderId);
        var connection = located.parent.Connections[located.index];
        located.parent.Connections.RemoveAt(located.index);
        target.Connections.Add(connection);
        Save();
    }

    public ConnectionFolder AddFolder(string name, string parentFolderId)
    {
        var parent = FindFolder(Root, parentFolderId) ?? throw FolderNotFound(parentFolderId);
        var folder = new ConnectionFolder { Name = name };
        parent.Folders.Add(folder);
        Save();
        return folder;
    }

    public void RenameFolder(string folderId, string newName)
    {
        var folder = FindFolder(Root, folderId) ?? throw FolderNotFound(folderId);
        folder.Name = newName;
        Save();
    }

    public void RemoveFolder(string folderId)
    {
        if (folderId == Root.Id)
            throw new InvalidOperationException("Cannot remove the root folder.");
        var parent = FindParentOfFolder(Root, folderId) ?? throw FolderNotFound(folderId);
        var folder = parent.Folders.First(f => f.Id == folderId);
        foreach (var id in CollectConnectionIds(folder))
            _credentials.RemoveSecret(id); // cascade every secret in the subtree
        parent.Folders.Remove(folder);
        Save();
    }

    private static ConnectionFolder? FindFolder(ConnectionFolder node, string id)
    {
        if (node.Id == id) return node;
        foreach (var child in node.Folders)
            if (FindFolder(child, id) is { } found) return found;
        return null;
    }

    private static ConnectionFolder? FindParentOfFolder(ConnectionFolder node, string childId)
    {
        foreach (var child in node.Folders)
        {
            if (child.Id == childId) return node;
            if (FindParentOfFolder(child, childId) is { } found) return found;
        }
        return null;
    }

    private static (ConnectionFolder parent, int index)? LocateConnection(ConnectionFolder node, string connectionId)
    {
        for (var i = 0; i < node.Connections.Count; i++)
            if (node.Connections[i].Id == connectionId) return (node, i);
        foreach (var child in node.Folders)
            if (LocateConnection(child, connectionId) is { } found) return found;
        return null;
    }

    private static IEnumerable<string> CollectConnectionIds(ConnectionFolder node)
    {
        foreach (var c in node.Connections) yield return c.Id;
        foreach (var child in node.Folders)
            foreach (var id in CollectConnectionIds(child)) yield return id;
    }

    private static InvalidOperationException FolderNotFound(string id) => new($"Folder '{id}' not found.");
    private static InvalidOperationException ConnectionNotFound(string id) => new($"Connection '{id}' not found.");
}
