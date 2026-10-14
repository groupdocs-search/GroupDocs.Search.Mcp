using GroupDocs.Search.Mcp;
using Xunit;

namespace GroupDocs.Search.Mcp.Tests;

public class SearchConfigTests
{
    private static readonly string Cwd = Path.Combine(Path.GetTempPath(), "gdsearch-cfg");

    private static SearchConfig Resolve(params (string Name, string Value)[] vars)
    {
        var map = vars.ToDictionary(v => v.Name, v => v.Value);
        return SearchConfig.FromEnvironment(n => map.TryGetValue(n, out var v) ? v : null, Cwd);
    }

    [Fact]
    public void NoVariables_StorageIsFilesUnderCwd_IndexUnderStorage()
    {
        var cfg = Resolve();

        Assert.Equal(Path.Combine(Cwd, "Files"), cfg.StorageRoot);
        Assert.Equal(Path.Combine(Cwd, "Files", SearchConfig.DefaultIndexFolderName), cfg.IndexRoot);
        Assert.Equal("default", cfg.DefaultIndexName);
    }

    [Fact]
    public void OutputPathSet_IndexLivesUnderOutput()
    {
        var output = Path.Combine(Cwd, "out");
        var cfg = Resolve((SearchConfig.StoragePathVariable, Path.Combine(Cwd, "docs")),
                          (SearchConfig.OutputPathVariable, output));

        Assert.Equal(Path.Combine(Cwd, "docs"), cfg.StorageRoot);
        Assert.Equal(Path.Combine(output, SearchConfig.DefaultIndexFolderName), cfg.IndexRoot);
    }

    [Fact]
    public void IndexPathSet_WinsOverOutputAndStorage()
    {
        var index = Path.Combine(Cwd, "idx");
        var cfg = Resolve((SearchConfig.StoragePathVariable, Path.Combine(Cwd, "docs")),
                          (SearchConfig.OutputPathVariable, Path.Combine(Cwd, "out")),
                          (SearchConfig.IndexPathVariable, index));

        Assert.Equal(index, cfg.IndexRoot);
    }

    [Fact]
    public void BlankVariables_AreIgnored()
    {
        var cfg = Resolve((SearchConfig.StoragePathVariable, "  "), (SearchConfig.IndexPathVariable, ""));

        Assert.Equal(Path.Combine(Cwd, "Files"), cfg.StorageRoot);
    }
}
