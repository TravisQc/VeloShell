namespace VeloShell.Models;

/// <summary>
/// Lifecycle state of a remote server session.
/// </summary>
public enum SessionState
{
    Connecting,
    Connected,
    Disconnecting,
    Disconnected,
    Error
}
