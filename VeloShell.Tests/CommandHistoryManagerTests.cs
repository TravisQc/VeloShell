using System.Linq;
using VeloShell.Services;
using Xunit;

namespace VeloShell.Tests;

public class CommandHistoryManagerTests
{
    [Fact]
    public void AddCommand_AddsNewItemToList()
    {
        var manager = new CommandHistoryManager();
        manager.AddCommand("docker ps", "server-1");

        var items = manager.Items;
        Assert.Single(items);
        Assert.Equal("docker ps", items[0].CommandText);
        Assert.Equal("server-1", items[0].Host);
    }

    [Fact]
    public void AddCommand_IgnoresEmptyOrWhitespace()
    {
        var manager = new CommandHistoryManager();
        manager.AddCommand("   ");
        manager.AddCommand("");

        Assert.Empty(manager.Items);
    }

    [Fact]
    public void AddCommand_DeduplicatesConsecutiveIdenticalCommands()
    {
        var manager = new CommandHistoryManager();
        manager.AddCommand("ls -la");
        manager.AddCommand("ls -la");
        manager.AddCommand("ls -la");

        Assert.Single(manager.Items);
        Assert.Equal("ls -la", manager.Items[0].CommandText);
    }

    [Fact]
    public void AddCommand_RespectsMaxEntriesLimit()
    {
        var manager = new CommandHistoryManager(maxEntries: 10);
        for (int i = 0; i < 15; i++)
        {
            manager.AddCommand($"cmd-{i}");
        }

        Assert.Equal(10, manager.Items.Count);
        Assert.Equal("cmd-5", manager.Items[0].CommandText);
        Assert.Equal("cmd-14", manager.Items[^1].CommandText);
    }

    [Fact]
    public void Search_FiltersMatchingCommands()
    {
        var manager = new CommandHistoryManager();
        manager.AddCommand("docker compose up");
        manager.AddCommand("git status");
        manager.AddCommand("docker logs -f");
        manager.AddCommand("systemctl restart nginx");

        var results = manager.Search("docker").ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.CommandText == "docker compose up");
        Assert.Contains(results, x => x.CommandText == "docker logs -f");
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var manager = new CommandHistoryManager();
        manager.AddCommand("test-1");
        manager.AddCommand("test-2");

        Assert.Equal(2, manager.Items.Count);

        manager.Clear();
        Assert.Empty(manager.Items);
    }
}
