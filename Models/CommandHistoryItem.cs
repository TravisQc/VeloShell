using System;

namespace VeloShell.Models;

public class CommandHistoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CommandText { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? Host { get; set; }

    public CommandHistoryItem() { }

    public CommandHistoryItem(string commandText, string? host = null)
    {
        CommandText = commandText;
        Host = host;
        Timestamp = DateTime.Now;
    }
}
