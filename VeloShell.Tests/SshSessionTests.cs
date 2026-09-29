using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VeloShell.Models;
using VeloShell.Services.Ssh;
using Xunit;

namespace VeloShell.Tests;

public class SshSessionTests
{
    [Fact]
    public void InitialState_IsDisconnected()
    {
        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = 22,
            Username = "test"
        };
        using var session = new SshSession(connection, "pwd");

        Assert.Equal(SessionState.Disconnected, session.State);
        Assert.False(session.IsConnected);
        Assert.Null(session.LastErrorMessage);
    }

    [Fact]
    public async Task ExecuteCommandAsync_WhenDisconnected_ThrowsInvalidOperationException()
    {
        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = 22,
            Username = "test"
        };
        using var session = new SshSession(connection, "pwd");

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteCommandAsync("ls"));
    }

    [Fact]
    public async Task ConnectAsync_InvalidTarget_TransitionsToErrorAndSetsMessage()
    {
        var connection = new Connection
        {
            Host = "256.256.256.256", // Invalid host
            Port = 22,
            Username = "test"
        };
        using var session = new SshSession(connection, "pwd");

        var states = new List<SessionState>();
        session.StateChanged += (_, s) => states.Add(s);

        await Assert.ThrowsAnyAsync<Exception>(() => session.ConnectAsync());

        Assert.Equal(SessionState.Error, session.State);
        Assert.False(session.IsConnected);
        Assert.NotNull(session.LastErrorMessage);
        Assert.Contains(SessionState.Connecting, states);
        Assert.Contains(SessionState.Error, states);
    }

    [Fact]
    public async Task DisconnectAsync_WhenAlreadyDisconnected_RemainsDisconnected()
    {
        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = 22,
            Username = "test"
        };
        using var session = new SshSession(connection, "pwd");

        await session.DisconnectAsync();
        Assert.Equal(SessionState.Disconnected, session.State);
    }
}
