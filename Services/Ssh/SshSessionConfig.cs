using System;
using System.Collections.Generic;
using System.IO;
using Renci.SshNet;
using VeloShell.Models;

namespace VeloShell.Services.Ssh;

public sealed class SshSessionConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public AuthMethod AuthMethod { get; set; } = AuthMethod.Password;
    public string? PrivateKeyPath { get; set; }
    public string? Secret { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    public static SshSessionConfig FromConnection(Connection connection, string? secret, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new SshSessionConfig
        {
            Host = connection.Host,
            Port = connection.Port <= 0 ? 22 : connection.Port,
            Username = connection.Username,
            AuthMethod = connection.AuthMethod,
            PrivateKeyPath = connection.PrivateKeyPath,
            Secret = secret,
            Timeout = timeout ?? TimeSpan.FromSeconds(10)
        };
    }

    public ConnectionInfo CreateConnectionInfo()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new InvalidOperationException("主机地址不能为空");
        if (string.IsNullOrWhiteSpace(Username))
            throw new InvalidOperationException("用户名不能为空");

        var authMethods = new List<AuthenticationMethod>();
        switch (AuthMethod)
        {
            case AuthMethod.Password:
                authMethods.Add(new PasswordAuthenticationMethod(Username, Secret ?? string.Empty));
                break;

            case AuthMethod.PrivateKey:
                if (string.IsNullOrWhiteSpace(PrivateKeyPath))
                    throw new InvalidOperationException("私钥文件路径未配置");
                if (!File.Exists(PrivateKeyPath))
                    throw new FileNotFoundException($"找不到私钥文件: {PrivateKeyPath}", PrivateKeyPath);
                authMethods.Add(new PrivateKeyAuthenticationMethod(Username, new PrivateKeyFile(PrivateKeyPath)));
                break;

            case AuthMethod.PrivateKeyWithPassphrase:
                if (string.IsNullOrWhiteSpace(PrivateKeyPath))
                    throw new InvalidOperationException("私钥文件路径未配置");
                if (!File.Exists(PrivateKeyPath))
                    throw new FileNotFoundException($"找不到私钥文件: {PrivateKeyPath}", PrivateKeyPath);
                authMethods.Add(new PrivateKeyAuthenticationMethod(Username, new PrivateKeyFile(PrivateKeyPath, Secret ?? string.Empty)));
                break;

            default:
                throw new NotSupportedException($"不支持的认证方式: {AuthMethod}");
        }

        return new ConnectionInfo(Host, Port, Username, authMethods.ToArray())
        {
            Timeout = Timeout
        };
    }
}
