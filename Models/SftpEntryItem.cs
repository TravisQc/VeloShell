using System;
using System.Globalization;

namespace VeloShell.Models;

public sealed class SftpEntryItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public SftpItemType ItemType { get; set; } = SftpItemType.File;
    public long Length { get; set; }
    public string Permissions { get; set; } = string.Empty;
    public DateTime LastWriteTime { get; set; }

    public bool IsDirectory => ItemType == SftpItemType.Directory;
    public bool IsHidden => Name.StartsWith('.') && Name != "." && Name != "..";

    public string HumanSize => IsDirectory ? "-" : FormatHumanSize(Length);

    public static string FormatHumanSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < units.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return order == 0 ? $"{len:0} {units[order]}" : $"{len.ToString("0.0", CultureInfo.InvariantCulture)} {units[order]}";
    }
}
