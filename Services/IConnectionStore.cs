using VeloShell.Models;

namespace VeloShell.Services;

/// <summary>
/// Persists and mutates the connection tree (connections.json). Deleting a
/// connection or a folder cascades to the credential store so no orphan secrets
/// remain. See specs/connection-management.
/// </summary>
public interface IConnectionStore
{
    /// <summary>Root of the connection tree (the top-level folder).</summary>
    ConnectionFolder Root { get; }

    /// <summary>Reloads the tree from disk.</summary>
    void Load();

    /// <summary>Persists the current tree to disk.</summary>
    void Save();

    void AddConnection(Connection connection, string folderId);
    void UpdateConnection(Connection connection);
    void RemoveConnection(string connectionId);
    void MoveConnection(string connectionId, string targetFolderId);

    ConnectionFolder AddFolder(string name, string parentFolderId);
    void RenameFolder(string folderId, string newName);
    void RemoveFolder(string folderId);
}
