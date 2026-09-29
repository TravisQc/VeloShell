using System;
using VeloShell.Services.Ssh;
using Xunit;

namespace VeloShell.Tests;

public class LinuxMetricsParserTests
{
    private const string SampleStat =
        "cpu  1000 0 1000 8000 0 0 0 0 0 0\n" +
        "cpu0 500 0 500 4000 0 0 0 0 0 0\n" +
        "cpu1 500 0 500 4000 0 0 0 0 0 0\n";

    private const string SampleMem =
        "MemTotal:        8192000 kB\n" +
        "MemFree:         2048000 kB\n" +
        "MemAvailable:    4096000 kB\n" +
        "Buffers:          200000 kB\n" +
        "Cached:          1848000 kB\n" +
        "SwapTotal:       2048000 kB\n" +
        "SwapFree:        1024000 kB\n";

    private const string SampleDf =
        "Filesystem     1024-blocks      Used Available Capacity Mounted on\n" +
        "/dev/sda1         20971520   5242880  15728640      25% /\n" +
        "/dev/sdb1         52428800  26214400  26214400      50% /data\n" +
        "tmpfs              4096000         0   4096000       0% /dev/shm\n";

    private const string SampleNet =
        "Inter-|   Receive                                                |  Transmit\n" +
        " face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed\n" +
        "    lo: 10000000        0    0    0    0     0          0         0 10000000        0    0    0    0     0       0          0\n" +
        "  eth0: 104857600       0    0    0    0     0          0         0 52428800        0    0    0    0     0       0          0\n";

    private const string SampleUptime =
        " 12:34:56 up 10 days,  2:30,  3 users,  load average: 0.12, 0.25, 0.40\n";

    private const string SampleUname =
        "Linux 6.8.0-31-generic x86_64\n";

    private string BuildSampleOutput()
    {
        return $"{SampleStat}{LinuxMetricsParser.Delimiter}\n" +
               $"{SampleMem}{LinuxMetricsParser.Delimiter}\n" +
               $"{SampleDf}{LinuxMetricsParser.Delimiter}\n" +
               $"{SampleNet}{LinuxMetricsParser.Delimiter}\n" +
               $"{SampleUptime}{LinuxMetricsParser.Delimiter}\n" +
               $"{SampleUname}";
    }

    [Fact]
    public void Parse_RealisticSample_ParsesAllFieldsCorrectly()
    {
        var raw = BuildSampleOutput();
        var state = new LinuxMetricsParserState();
        var t0 = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        var metrics1 = LinuxMetricsParser.Parse(raw, state, t0);

        Assert.Equal(2, metrics1.Cpu.CoreCount);
        // First sample has no delta, so usage is 0
        Assert.Equal(0, metrics1.Cpu.UsagePercent);

        // Memory: 8192000 kB total, 4096000 kB available => 4096000 kB used (50%)
        Assert.Equal(8192000L * 1024, metrics1.Memory.TotalBytes);
        Assert.Equal(4096000L * 1024, metrics1.Memory.AvailableBytes);
        Assert.Equal(4096000L * 1024, metrics1.Memory.UsedBytes);
        Assert.Equal(50.0, metrics1.Memory.UsagePercent);

        // Swap: 2048000 kB total, 1024000 kB used (50%)
        Assert.Equal(2048000L * 1024, metrics1.Memory.SwapTotalBytes);
        Assert.Equal(1024000L * 1024, metrics1.Memory.SwapUsedBytes);
        Assert.Equal(50.0, metrics1.Memory.SwapUsagePercent);

        // Disks: / and /data (tmpfs is excluded)
        Assert.Equal(2, metrics1.Disks.Count);
        Assert.Equal("/", metrics1.Disks[0].MountedOn);
        Assert.Equal(25.0, metrics1.Disks[0].UsagePercent);
        Assert.Equal("/data", metrics1.Disks[1].MountedOn);
        Assert.Equal(50.0, metrics1.Disks[1].UsagePercent);

        // Host & Uptime
        Assert.Contains("10 days", metrics1.Host.Uptime);
        Assert.Equal("0.12, 0.25, 0.40", metrics1.Host.LoadAverage);
        Assert.Equal("Linux 6.8.0-31-generic x86_64", metrics1.Host.KernelVersion);
    }

    [Fact]
    public void Parse_SecondSample_CalculatesCpuAndNetworkRates()
    {
        var raw1 = BuildSampleOutput();
        var state = new LinuxMetricsParserState();
        var t0 = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        LinuxMetricsParser.Parse(raw1, state, t0);

        // In 2 seconds:
        // CPU: total ticks increased by 1000 (user + system = 500, idle = 500) => 50% CPU
        var nextStat =
            "cpu  1250 0 1250 8500 0 0 0 0 0 0\n" +
            "cpu0 625 0 625 4250 0 0 0 0 0 0\n" +
            "cpu1 625 0 625 4250 0 0 0 0 0 0\n";
        // Net: rx increased by 204800 bytes, tx increased by 102400 bytes in 2s
        var nextNet =
            "Inter-|   Receive                                                |  Transmit\n" +
            " face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed\n" +
            "  eth0: 105062400       0    0    0    0     0          0         0 52531200        0    0    0    0     0       0          0\n";

        var raw2 = $"{nextStat}{LinuxMetricsParser.Delimiter}\n" +
                   $"{SampleMem}{LinuxMetricsParser.Delimiter}\n" +
                   $"{SampleDf}{LinuxMetricsParser.Delimiter}\n" +
                   $"{nextNet}{LinuxMetricsParser.Delimiter}\n" +
                   $"{SampleUptime}{LinuxMetricsParser.Delimiter}\n" +
                   $"{SampleUname}";

        var t1 = t0.AddSeconds(2);
        var metrics2 = LinuxMetricsParser.Parse(raw2, state, t1);

        // (1000 total delta - 500 idle delta) / 1000 = 50.0%
        Assert.Equal(50.0, metrics2.Cpu.UsagePercent);

        // 204800 / 2 = 102400 B/s download
        Assert.Equal(102400.0, metrics2.Network.DownloadBytesPerSec);
        // 102400 / 2 = 51200 B/s upload
        Assert.Equal(51200.0, metrics2.Network.UploadBytesPerSec);
    }

    [Fact]
    public void Parse_EmptyOrCorruptedOutput_DoesNotThrow()
    {
        var state = new LinuxMetricsParserState();
        var metrics = LinuxMetricsParser.Parse("", state);
        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.Cpu.UsagePercent);
        Assert.Empty(metrics.Disks);

        var corrupted = "random gibberish without delimiter";
        var metricsCorrupted = LinuxMetricsParser.Parse(corrupted, state);
        Assert.NotNull(metricsCorrupted);
    }
}
