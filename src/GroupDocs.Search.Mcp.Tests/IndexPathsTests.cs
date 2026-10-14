using GroupDocs.Search.Mcp;
using GroupDocs.Search.Mcp.Tools;
using Xunit;

namespace GroupDocs.Search.Mcp.Tests;

public sealed class IndexPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gdsearch-paths-" + Guid.NewGuid().ToString("N"));
    private readonly SearchConfig _cfg;

    public IndexPathsTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "docs", "contracts"));
        File.WriteAllText(Path.Combine(_root, "docs", "contracts", "lease.txt"), "lease");
        File.WriteAllText(Path.Combine(_root, "docs", "readme.txt"), "readme");
        _cfg = new SearchConfig { StorageRoot = Path.Combine(_root, "docs"), IndexRoot = Path.Combine(_root, "idx") };
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void ResolveIndex_Blank_UsesDefaultIndex()
    {
        var (name, folder) = IndexPaths.ResolveIndex(_cfg, null);

        Assert.Equal("default", name);
        Assert.Equal(Path.Combine(_cfg.IndexRoot, "default"), folder);
    }

    [Theory]
    [InlineData("contracts", "contracts")]
    [InlineData("legal/2026", "legal/2026")]
    [InlineData(" legal\\2026 ", "legal/2026")]
    public void ResolveIndex_RelativeNames_StayUnderRoot(string input, string expectedName)
    {
        var (name, folder) = IndexPaths.ResolveIndex(_cfg, input);

        Assert.Equal(expectedName, name);
        Assert.True(IndexPaths.IsUnder(folder, _cfg.IndexRoot));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/../../escape")]
    [InlineData("C:/abs")]
    [InlineData("/abs")]
    [InlineData(".")]
    public void ResolveIndex_PathsAndEscapes_Throw(string input)
    {
        var ex = Assert.Throws<ArgumentException>(() => IndexPaths.ResolveIndex(_cfg, input));
        Assert.Contains("indexName", ex.Message);
    }

    [Fact]
    public void ResolveDocuments_Blank_IsStorageRoot()
    {
        Assert.Equal(_cfg.StorageRoot, IndexPaths.ResolveDocuments(_cfg, null));
        Assert.Equal(_cfg.StorageRoot, IndexPaths.ResolveDocuments(_cfg, "."));
    }

    [Fact]
    public void ResolveDocuments_ExistingRelativePath_Resolves()
    {
        var full = IndexPaths.ResolveDocuments(_cfg, "contracts/lease.txt");

        Assert.Equal(Path.Combine(_cfg.StorageRoot, "contracts", "lease.txt"), full);
        Assert.Equal("contracts/lease.txt", IndexPaths.ToStorageRelative(_cfg, full));
    }

    [Fact]
    public void ResolveDocuments_Escape_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => IndexPaths.ResolveDocuments(_cfg, "../"));
        Assert.Contains("outside the storage folder", ex.Message);
    }

    [Fact]
    public void ResolveDocuments_Absolute_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => IndexPaths.ResolveDocuments(_cfg, _cfg.StorageRoot));
        Assert.Contains("relative to the storage folder", ex.Message);
    }

    [Fact]
    public void ResolveDocuments_Missing_ListsWhatExists()
    {
        var ex = Assert.Throws<FileNotFoundException>(() => IndexPaths.ResolveDocuments(_cfg, "nope"));

        Assert.Contains("not found in storage", ex.Message);
        Assert.Contains("Available files and folders:", ex.Message);
        Assert.Contains("- contracts/", ex.Message);
        Assert.Contains("- readme.txt", ex.Message);
    }

    [Fact]
    public void Describe_StorageRoot_IsWholeStorageFolder()
    {
        Assert.Equal("the whole storage folder", IndexPaths.Describe(_cfg, _cfg.StorageRoot));
        Assert.Equal("'readme.txt'", IndexPaths.Describe(_cfg, Path.Combine(_cfg.StorageRoot, "readme.txt")));
    }
}
