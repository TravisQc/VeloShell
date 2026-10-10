namespace VeloShell.Models;

/// <summary>
/// Observable health classification for a session's status dot in the shell.
/// Maps from <see cref="SessionState"/> plus a "connected but degraded" signal.
/// </summary>
public enum SessionHealth
{
    Healthy,
    Warning,
    Error
}
