using System;
using System.Collections.Generic;

namespace VeloShell.Models;

public sealed class CpuMetrics
{
    public double UsagePercent { get; set; }
    public int CoreCount { get; set; }
}

public sealed class MemoryMetrics
{
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long AvailableBytes { get; set; }
    public double UsagePercent => TotalBytes > 0 ? Math.Round((double)UsedBytes / TotalBytes * 100.0, 1) : 0.0;

    public long SwapTotalBytes { get; set; }
    public long SwapUsedBytes { get; set; }
    public double SwapUsagePercent => SwapTotalBytes > 0 ? Math.Round((double)SwapUsedBytes / SwapTotalBytes * 100.0, 1) : 0.0;
}

public sealed class DiskPartitionMetrics
{
    public string Filesystem { get; set; } = string.Empty;
    public string MountedOn { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long AvailableBytes { get; set; }
    public double UsagePercent => TotalBytes > 0 ? Math.Round((double)UsedBytes / TotalBytes * 100.0, 1) : 0.0;
}

public sealed class NetworkRateMetrics
{
    public double UploadBytesPerSec { get; set; }
    public double DownloadBytesPerSec { get; set; }
    public long TotalReceivedBytes { get; set; }
    public long TotalTransmittedBytes { get; set; }
}

public sealed class HostOverview
{
    public string Hostname { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string KernelVersion { get; set; } = string.Empty;
    public string Uptime { get; set; } = string.Empty;
    public string LoadAverage { get; set; } = string.Empty;
}

public sealed class SystemMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public CpuMetrics Cpu { get; set; } = new();
    public MemoryMetrics Memory { get; set; } = new();
    public List<DiskPartitionMetrics> Disks { get; set; } = new();
    public NetworkRateMetrics Network { get; set; } = new();
    public HostOverview Host { get; set; } = new();
}
