using System;
using System.IO;
using Renci.SshNet;
using VeloShell.Models;
using VeloShell.Services.Ssh;
using Xunit;

namespace VeloShell.Tests;

public class SshSessionConfigTests
{
    [Fact]
    public void FromConnection_PasswordAuth_MapsPropertiesCorrectly()
    {
        var connection = new Connection
        {
            Host = "192.168.1.100",
            Port = 2222,
            Username = "ubuntu",
            AuthMethod = AuthMethod.Password
        };

        var config = SshSessionConfig.FromConnection(connection, "mypassword");

        Assert.Equal("192.168.1.100", config.Host);
        Assert.Equal(2222, config.Port);
        Assert.Equal("ubuntu", config.Username);
        Assert.Equal(AuthMethod.Password, config.AuthMethod);
        Assert.Equal("mypassword", config.Secret);

        var connInfo = config.CreateConnectionInfo();
        Assert.Equal("192.168.1.100", connInfo.Host);
        Assert.Equal(2222, connInfo.Port);
        Assert.Equal("ubuntu", connInfo.Username);
        Assert.NotEmpty(connInfo.AuthenticationMethods);
    }

    [Fact]
    public void CreateConnectionInfo_MissingHost_ThrowsInvalidOperationException()
    {
        var config = new SshSessionConfig
        {
            Host = "",
            Username = "root",
            AuthMethod = AuthMethod.Password
        };

        Assert.Throws<InvalidOperationException>(() => config.CreateConnectionInfo());
    }

    [Fact]
    public void CreateConnectionInfo_PrivateKeyMissingFile_ThrowsFileNotFoundException()
    {
        var config = new SshSessionConfig
        {
            Host = "example.com",
            Username = "root",
            AuthMethod = AuthMethod.PrivateKey,
            PrivateKeyPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        };

        Assert.Throws<FileNotFoundException>(() => config.CreateConnectionInfo());
    }
}
