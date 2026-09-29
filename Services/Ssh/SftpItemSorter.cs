using System.Collections.Generic;
using System.Linq;
using VeloShell.Models;

namespace VeloShell.Services.Ssh;

public enum SftpSortColumn
{
    Name,
    Size,
    ModifiedTime
}

public static class SftpItemSorter
{
    public static IEnumerable<SftpEntryItem> Sort(
        IEnumerable<SftpEntryItem> items,
        SftpSortColumn column,
        bool ascending = true,
        bool showHidden = true)
    {
        var filtered = showHidden ? items : items.Where(x => !x.IsHidden);

        // Always put directories first
        return ascending
            ? filtered.OrderByDescending(x => x.IsDirectory).ThenBy(x => GetSortKey(x, column))
            : filtered.OrderByDescending(x => x.IsDirectory).ThenByDescending(x => GetSortKey(x, column));
    }

    private static object GetSortKey(SftpEntryItem item, SftpSortColumn column) => column switch
    {
        SftpSortColumn.Name => item.Name.ToLowerInvariant(),
        SftpSortColumn.Size => item.Length,
        SftpSortColumn.ModifiedTime => item.LastWriteTime,
        _ => item.Name
    };
}
