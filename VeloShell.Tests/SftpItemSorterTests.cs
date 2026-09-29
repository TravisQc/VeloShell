using System;
using System.Collections.Generic;
using System.Linq;
using VeloShell.Models;
using VeloShell.Services.Ssh;
using Xunit;

namespace VeloShell.Tests;

public class SftpItemSorterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1073741824, "1.0 GB")]
    public void FormatHumanSize_ReturnsExpectedString(long bytes, string expected)
    {
        var result = SftpEntryItem.FormatHumanSize(bytes);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Sort_DirectoriesAlwaysPrecedeFiles()
    {
        var items = new List<SftpEntryItem>
        {
            new() { Name = "zeta.txt", ItemType = SftpItemType.File, Length = 100 },
            new() { Name = "alpha_dir", ItemType = SftpItemType.Directory },
            new() { Name = "beta.txt", ItemType = SftpItemType.File, Length = 50 },
            new() { Name = "omega_dir", ItemType = SftpItemType.Directory }
        };

        var sorted = SftpItemSorter.Sort(items, SftpSortColumn.Name, ascending: true).ToList();

        Assert.Equal("alpha_dir", sorted[0].Name);
        Assert.Equal("omega_dir", sorted[1].Name);
        Assert.Equal("beta.txt", sorted[2].Name);
        Assert.Equal("zeta.txt", sorted[3].Name);
    }

    [Fact]
    public void Sort_DescendingSize_KeepsDirectoriesFirstThenSortsFiles()
    {
        var items = new List<SftpEntryItem>
        {
            new() { Name = "small.txt", ItemType = SftpItemType.File, Length = 100 },
            new() { Name = "big.txt", ItemType = SftpItemType.File, Length = 50000 },
            new() { Name = "dir_a", ItemType = SftpItemType.Directory }
        };

        var sorted = SftpItemSorter.Sort(items, SftpSortColumn.Size, ascending: false).ToList();

        Assert.Equal("dir_a", sorted[0].Name);
        Assert.Equal("big.txt", sorted[1].Name);
        Assert.Equal("small.txt", sorted[2].Name);
    }

    [Fact]
    public void Sort_FilterHiddenFiles_ExcludesDotFilesWhenDisabled()
    {
        var items = new List<SftpEntryItem>
        {
            new() { Name = ".bashrc", ItemType = SftpItemType.File },
            new() { Name = ".config", ItemType = SftpItemType.Directory },
            new() { Name = "public", ItemType = SftpItemType.Directory },
            new() { Name = "readme.txt", ItemType = SftpItemType.File }
        };

        var visibleOnly = SftpItemSorter.Sort(items, SftpSortColumn.Name, ascending: true, showHidden: false).ToList();
        Assert.Equal(2, visibleOnly.Count);
        Assert.Equal("public", visibleOnly[0].Name);
        Assert.Equal("readme.txt", visibleOnly[1].Name);

        var allItems = SftpItemSorter.Sort(items, SftpSortColumn.Name, ascending: true, showHidden: true).ToList();
        Assert.Equal(4, allItems.Count);
    }
}
