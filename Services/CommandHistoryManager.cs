using System;
using System.Collections.Generic;
using System.Linq;
using VeloShell.Models;

namespace VeloShell.Services;

public class CommandHistoryManager
{
    private readonly List<CommandHistoryItem> _history = new();
    private readonly int _maxEntries;
    private readonly object _lock = new();

    public IReadOnlyList<CommandHistoryItem> Items
    {
        get
        {
            lock (_lock)
            {
                return _history.ToList();
            }
        }
    }

    public event EventHandler<CommandHistoryItem>? CommandAdded;
    public event EventHandler? HistoryCleared;

    public CommandHistoryManager(int maxEntries = 500)
    {
        _maxEntries = Math.Max(10, maxEntries);
    }

    public void AddCommand(string commandText, string? host = null)
    {
        if (string.IsNullOrWhiteSpace(commandText)) return;

        string trimmed = commandText.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;

        CommandHistoryItem? itemToAdd = null;

        lock (_lock)
        {
            // Deduplicate consecutive identical commands
            if (_history.Count > 0 && string.Equals(_history[^1].CommandText, trimmed, StringComparison.Ordinal))
            {
                _history[^1].Timestamp = DateTime.Now;
                return;
            }

            itemToAdd = new CommandHistoryItem(trimmed, host);
            _history.Add(itemToAdd);

            if (_history.Count > _maxEntries)
            {
                _history.RemoveAt(0);
            }
        }

        if (itemToAdd != null)
        {
            CommandAdded?.Invoke(this, itemToAdd);
        }
    }

    public IEnumerable<CommandHistoryItem> Search(string? query)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return _history.OrderByDescending(x => x.Timestamp).ToList();
            }

            return _history
                .Where(x => x.CommandText.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Timestamp)
                .ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _history.Clear();
        }
        HistoryCleared?.Invoke(this, EventArgs.Empty);
    }
}
