// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;

namespace GauntletCI.Tests;

public class IgnoreListTests
{
    private static string MakeTempDir()
    {
        var dir = Directory.CreateTempSubdirectory("gauntletci_test_").FullName;
        return dir;
    }

    [Fact]
    public void Load_NoIgnoreFile_ReturnsEmpty()
    {
        var dir = MakeTempDir();
        try
        {
            var list = IgnoreList.Load(dir);
            Assert.True(list.IsEmpty);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_EmptyFile_ReturnsEmpty()
    {
        var dir = MakeTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, ".gauntletci-ignore"), "# just a comment\n   \n");
            var list = IgnoreList.Load(dir);
            Assert.True(list.IsEmpty);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_GlobalSuppression_IsSuppressed()
    {
        var dir = MakeTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, ".gauntletci-ignore"), "GCI0003\n");
            var list = IgnoreList.Load(dir);
            Assert.True(list.IsSuppressed("GCI0003"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_GlobalSuppression_CaseInsensitive()
    {
        var dir = MakeTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, ".gauntletci-ignore"), "gci0003\n");
            var list = IgnoreList.Load(dir);
            Assert.True(list.IsSuppressed("GCI0003"));
            Assert.True(list.IsSuppressed("gci0003"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_DifferentRule_NotSuppressed()
    {
        var dir = MakeTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, ".gauntletci-ignore"), "GCI0003\n");
            var list = IgnoreList.Load(dir);
            Assert.False(list.IsSuppressed("GCI0001"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_PathGlobSuppression_MatchingPath_IsSuppressed()
    {
        var dir = MakeTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, ".gauntletci-ignore"), "GCI0005:src/Generated/**\n");
            var list = IgnoreList.Load(dir);
            Assert.True(list.IsSuppressed("GCI0005", "src/Generated/Foo.cs"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_PathGlobSuppression_NonMatchingPath_NotSuppressed()
    {
        var dir = MakeTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, ".gauntletci-ignore"), "GCI0005:src/Generated/**\n");
            var list = IgnoreList.Load(dir);
            Assert.False(list.IsSuppressed("GCI0005", "src/Main/Foo.cs"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_CommentLines_Ignored()
    {
        var dir = MakeTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, ".gauntletci-ignore"), "# comment\nGCI0001\n");
            var list = IgnoreList.Load(dir);
            Assert.False(list.IsEmpty);
            Assert.True(list.IsSuppressed("GCI0001"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void IsSuppressed_GlobalRule_NoFilePath_IsSuppressed()
    {
        var dir = MakeTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, ".gauntletci-ignore"), "GCI0003\n");
            var list = IgnoreList.Load(dir);
            Assert.True(list.IsSuppressed("GCI0003", filePath: null));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void IsSuppressed_PathRule_NoFilePath_NotSuppressed()
    {
        var dir = MakeTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, ".gauntletci-ignore"), "GCI0005:src/**\n");
            var list = IgnoreList.Load(dir);
            Assert.False(list.IsSuppressed("GCI0005", filePath: null));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Append_CreatesFile_WithRule()
    {
        var dir = MakeTempDir();
        try
        {
            IgnoreList.Append(dir, "GCI0009");
            var list = IgnoreList.Load(dir);
            Assert.True(list.IsSuppressed("GCI0009"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Append_CreatesFile_WithRuleAndPath()
    {
        var dir = MakeTempDir();
        try
        {
            IgnoreList.Append(dir, "GCI0005", "src/**");
            var list = IgnoreList.Load(dir);
            Assert.True(list.IsSuppressed("GCI0005", "src/Foo.cs"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Append_ExistingFile_AppendsEntry()
    {
        var dir = MakeTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, ".gauntletci-ignore"), "GCI0001\n");
            IgnoreList.Append(dir, "GCI0002");
            var list = IgnoreList.Load(dir);
            Assert.True(list.IsSuppressed("GCI0001"));
            Assert.True(list.IsSuppressed("GCI0002"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
