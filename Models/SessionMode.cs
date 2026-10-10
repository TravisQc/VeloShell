namespace VeloShell.Models;

/// <summary>
/// The per-session navigation mode selected in the left rail of the window shell.
/// Each session keeps its own current mode (not globally shared).
/// </summary>
public enum SessionMode
{
    Terminal,
    Host,
    Settings
}
