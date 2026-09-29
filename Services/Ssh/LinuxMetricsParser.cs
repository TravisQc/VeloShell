using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using VeloShell.Models;

namespace VeloShell.Services.Ssh;

public sealed class LinuxMetricsParserState
{
    public long PrevTotalCpuTicks { get; set; }
    public long PrevIdleCpuTicks { get; set; }
    public long PrevTotalRxBytes { get; set; }
    public long PrevTotalTxBytes { get; set; }
    public DateTime? PrevTimestamp { get; set; }
}

public static class LinuxMetricsParser
{
    public const string Delimiter = "---VELO---";

    public const string CompositeCommand =
        "cat /proc/stat 2>/dev/null; echo '" + Delimiter + "'; " +
        "cat /proc/meminfo 2>/dev/null; echo '" + Delimiter + "'; " +
        "df -kP 2>/dev/null; echo '" + Delimiter + "'; " +
        "cat /proc/net/dev 2>/dev/null; echo '" + Delimiter + "'; " +
        "uptime 2>/dev/null; echo '" + Delimiter + "'; " +
        "(uname -srm 2>/dev/null || uname -a)";

    public static SystemMetrics Parse(string output, LinuxMetricsParserState? state = null, DateTime? timestamp = null)
    {
        var now = timestamp ?? DateTime.UtcNow;
        var metrics = new SystemMetrics { Timestamp = now };
        if (string.IsNullOrWhiteSpace(output))
            return metrics;

        var sections = output.Split(Delimiter, StringSplitOptions.None);

        if (sections.Length > 0)
            ParseCpu(sections[0], metrics.Cpu, state);

        if (sections.Length > 1)
            ParseMemory(sections[1], metrics.Memory);

        if (sections.Length > 2)
            ParseDisks(sections[2], metrics.Disks);

        if (sections.Length > 3)
            ParseNetwork(sections[3], metrics.Network, state, now);

        if (sections.Length > 4)
            ParseUptime(sections[4], metrics.Host);

        if (sections.Length > 5)
            ParseUname(sections[5], metrics.Host);

        return metrics;
    }

    private static void ParseCpu(string section, CpuMetrics cpu, LinuxMetricsParserState? state)
    {
        var lines = section.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int cores = 0;
        long totalTicks = 0;
        long idleTicks = 0;
        bool foundAggregate = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("cpu "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5)
                {
                    // cpu user nice system idle iowait irq softirq steal
                    long sum = 0;
                    for (int i = 1; i < parts.Length; i++)
                    {
                        if (long.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tick))
                            sum += tick;
                    }
                    totalTicks = sum;

                    long idle = 0;
                    long iowait = 0;
                    if (long.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out idle))
                    {
                        if (parts.Length > 5 && long.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out iowait))
                            idle += iowait;
                    }
                    idleTicks = idle;
                    foundAggregate = true;
                }
            }
            else if (Regex.IsMatch(line, @"^cpu\d+"))
            {
                cores++;
            }
        }

        cpu.CoreCount = cores > 0 ? cores : 1;

        if (foundAggregate && state != null)
        {
            if (state.PrevTotalCpuTicks > 0)
            {
                long deltaTotal = totalTicks - state.PrevTotalCpuTicks;
                long deltaIdle = idleTicks - state.PrevIdleCpuTicks;
                if (deltaTotal > 0)
                {
                    double usage = (double)(deltaTotal - deltaIdle) / deltaTotal * 100.0;
                    cpu.UsagePercent = Math.Clamp(Math.Round(usage, 1), 0.0, 100.0);
                }
            }
            state.PrevTotalCpuTicks = totalTicks;
            state.PrevIdleCpuTicks = idleTicks;
        }
    }

    private static void ParseMemory(string section, MemoryMetrics mem)
    {
        var lines = section.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        long memTotalKb = 0;
        long memFreeKb = 0;
        long memAvailableKb = -1;
        long buffersKb = 0;
        long cachedKb = 0;
        long swapTotalKb = 0;
        long swapFreeKb = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            var parts = line.Split(':', 2);
            if (parts.Length < 2) continue;

            var key = parts[0].Trim();
            var valPart = parts[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!long.TryParse(valPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
                continue;

            switch (key)
            {
                case "MemTotal": memTotalKb = kb; break;
                case "MemFree": memFreeKb = kb; break;
                case "MemAvailable": memAvailableKb = kb; break;
                case "Buffers": buffersKb = kb; break;
                case "Cached": cachedKb = kb; break;
                case "SwapTotal": swapTotalKb = kb; break;
                case "SwapFree": swapFreeKb = kb; break;
            }
        }

        mem.TotalBytes = memTotalKb * 1024;
        if (memAvailableKb >= 0)
            mem.AvailableBytes = memAvailableKb * 1024;
        else
            mem.AvailableBytes = (memFreeKb + buffersKb + cachedKb) * 1024;

        mem.UsedBytes = Math.Max(0, mem.TotalBytes - mem.AvailableBytes);

        mem.SwapTotalBytes = swapTotalKb * 1024;
        mem.SwapUsedBytes = Math.Max(0, (swapTotalKb - swapFreeKb) * 1024);
    }

    private static void ParseDisks(string section, List<DiskPartitionMetrics> disks)
    {
        var lines = section.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        disks.Clear();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("Filesystem", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6) continue;

            var fs = parts[0];
            if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var totalKb))
                continue;
            if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var usedKb))
                continue;
            if (!long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var availKb))
                continue;

            var mountedOn = parts[5];

            // Filter out loop devices or non-filesystem pseudo mounts unless it's root
            if (fs.StartsWith("/dev/loop") || totalKb <= 0)
                continue;
            if ((fs == "tmpfs" || fs == "devtmpfs" || fs == "overlay" || fs == "none") && mountedOn != "/")
                continue;

            disks.Add(new DiskPartitionMetrics
            {
                Filesystem = fs,
                MountedOn = mountedOn,
                TotalBytes = totalKb * 1024,
                UsedBytes = usedKb * 1024,
                AvailableBytes = availKb * 1024
            });
        }
    }

    private static void ParseNetwork(string section, NetworkRateMetrics net, LinuxMetricsParserState? state, DateTime now)
    {
        var lines = section.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        long totalRx = 0;
        long totalTx = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            int colonIndex = line.IndexOf(':');
            if (colonIndex <= 0) continue;

            var iface = line.Substring(0, colonIndex).Trim();
            if (iface == "lo") continue; // Skip loopback

            var dataPart = line.Substring(colonIndex + 1).Trim();
            var tokens = dataPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 9)
            {
                if (long.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rx))
                    totalRx += rx;
                if (long.TryParse(tokens[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tx))
                    totalTx += tx;
            }
        }

        net.TotalReceivedBytes = totalRx;
        net.TotalTransmittedBytes = totalTx;

        if (state != null)
        {
            if (state.PrevTimestamp.HasValue && state.PrevTotalRxBytes > 0)
            {
                var deltaSec = (now - state.PrevTimestamp.Value).TotalSeconds;
                if (deltaSec > 0.05)
                {
                    net.DownloadBytesPerSec = Math.Max(0, (totalRx - state.PrevTotalRxBytes) / deltaSec);
                    net.UploadBytesPerSec = Math.Max(0, (totalTx - state.PrevTotalTxBytes) / deltaSec);
                }
            }

            state.PrevTotalRxBytes = totalRx;
            state.PrevTotalTxBytes = totalTx;
            state.PrevTimestamp = now;
        }
    }

    private static void ParseUptime(string section, HostOverview host)
    {
        var line = section.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(line)) return;

        // e.g.: 14:20:50 up 3 days,  4:12,  2 users,  load average: 0.15, 0.22, 0.18
        int loadIdx = line.IndexOf("load average:", StringComparison.OrdinalIgnoreCase);
        if (loadIdx >= 0)
        {
            host.LoadAverage = line.Substring(loadIdx + "load average:".Length).Trim();
        }

        int upIdx = line.IndexOf("up ", StringComparison.OrdinalIgnoreCase);
        if (upIdx >= 0)
        {
            int endIdx = loadIdx >= 0 ? line.LastIndexOf(',', loadIdx) : line.Length;
            if (endIdx > upIdx)
            {
                var upPart = line.Substring(upIdx + 3, endIdx - (upIdx + 3)).Trim();
                // strip trailing users part if comma present
                int userIdx = upPart.IndexOf("user", StringComparison.OrdinalIgnoreCase);
                if (userIdx > 0)
                {
                    int lastComma = upPart.LastIndexOf(',', userIdx);
                    if (lastComma > 0)
                        upPart = upPart.Substring(0, lastComma).Trim();
                }
                host.Uptime = upPart.Trim(',', ' ');
            }
        }
    }

    private static void ParseUname(string section, HostOverview host)
    {
        var line = section.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        if (!string.IsNullOrWhiteSpace(line))
        {
            host.KernelVersion = line;
        }
    }
}
