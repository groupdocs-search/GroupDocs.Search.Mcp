using System.Text.Json;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Search.Mcp;
using GroupDocs.Search.Mcp.Tools;
using ModelContextProtocol.Protocol;
using Moq;
using Xunit;

namespace GroupDocs.Search.Mcp.Tests;

/// <summary>
/// Drives the tool methods against the real engine in a temp folder. Unlicensed on purpose: the
/// documents stay far below the 100-document evaluation cap, and the evaluation notice is asserted.
/// </summary>
public sealed class SearchToolsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gdsearch-tools-" + Guid.NewGuid().ToString("N"));
    private readonly SearchConfig _cfg;
    private readonly ILicenseManager _license;

    public SearchToolsTests()
    {
        var docs = Path.Combine(_root, "docs");
        Directory.CreateDirectory(docs);
        File.WriteAllText(Path.Combine(docs, "alpha-report.txt"),
            "Quarterly revenue grew by twelve percent in the third quarter.\nThe revenue forecast for next year is stable.\n");
        File.WriteAllText(Path.Combine(docs, "beta-notes.txt"),
            "Notes on the contract renewal meeting.\nThe renewal terms were accepted by both parties.\n");
        _cfg = new SearchConfig { StorageRoot = docs, IndexRoot = Path.Combine(_root, "idx") };

        var mock = new Mock<ILicenseManager>();
        mock.SetupGet(m => m.IsLicensed).Returns(false);
        _license = mock.Object;
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* engine may hold a handle briefly */ }
    }

    private static string Text(CallToolResult r) => string.Concat(r.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private static JsonElement Json(CallToolResult r) => JsonDocument.Parse(Text(r)).RootElement;

    [Fact]
    public void AddThenSearch_FindsOnlyTheMatchingDocument()
    {
        var add = AddToIndexTool.AddToIndex(_license, _cfg, indexName: "t");
        Assert.NotEqual(true, add.IsError);
        Assert.Contains("2 document(s) added", Text(add));

        var search = Json(SearchTool.Search(_license, _cfg, "revenue", indexName: "t"));
        Assert.Equal(1, search.GetProperty("documentsFound").GetInt32());
        Assert.Equal("alpha-report.txt", search.GetProperty("documents")[0].GetProperty("filePath").GetString());
        Assert.Equal(Evaluation.Notice, search.GetProperty("evaluation").GetString());

        var either = Json(SearchTool.Search(_license, _cfg, "revenue OR renewal", indexName: "t"));
        Assert.Equal(2, either.GetProperty("documentsFound").GetInt32());
    }

    [Fact]
    public void QuotedPhrase_Matches_UnquotedPhrase_ReturnsHint()
    {
        AddToIndexTool.AddToIndex(_license, _cfg, indexName: "t");

        var quoted = Json(SearchTool.Search(_license, _cfg, "\"quarterly revenue\"", indexName: "t"));
        Assert.Equal(1, quoted.GetProperty("documentsFound").GetInt32());

        var unquoted = Json(SearchTool.Search(_license, _cfg, "quarterly revenue", indexName: "t"));
        Assert.Equal(0, unquoted.GetProperty("documentsFound").GetInt32());
        Assert.Contains("\"quarterly revenue\"", unquoted.GetProperty("hint").GetString());
    }

    [Fact]
    public void Fragments_ContainTheMatchedSentence()
    {
        AddToIndexTool.AddToIndex(_license, _cfg, indexName: "t");

        var text = Text(GetTextFragmentsTool.GetTextFragments(_license, _cfg, "renewal", indexName: "t"));

        Assert.Contains("=== beta-notes.txt", text);
        Assert.Contains("contract renewal meeting", text);
    }

    [Fact]
    public void RemoveIndexedFile_StopsMatching()
    {
        AddToIndexTool.AddToIndex(_license, _cfg, "alpha-report.txt", "t");
        AddToIndexTool.AddToIndex(_license, _cfg, "beta-notes.txt", "t");

        var removed = RemoveFromIndexTool.RemoveFromIndex(_license, _cfg, "alpha-report.txt", "t");
        Assert.NotEqual(true, removed.IsError);

        var search = Json(SearchTool.Search(_license, _cfg, "revenue", indexName: "t"));
        Assert.Equal(0, search.GetProperty("documentsFound").GetInt32());
    }

    [Fact]
    public void RemoveFileInsideIndexedFolder_FailsWithReason()
    {
        AddToIndexTool.AddToIndex(_license, _cfg, indexName: "t");

        var removed = RemoveFromIndexTool.RemoveFromIndex(_license, _cfg, "alpha-report.txt", "t");

        Assert.True(removed.IsError);
        Assert.StartsWith("Removal failed for 'alpha-report.txt'", Text(removed));
        Assert.Contains("whole indexed paths only", Text(removed));
    }

    [Fact]
    public void UpdateIndex_PicksUpEditedFile()
    {
        AddToIndexTool.AddToIndex(_license, _cfg, indexName: "t");
        Thread.Sleep(1100); // modification time resolution
        File.WriteAllText(Path.Combine(_cfg.StorageRoot, "beta-notes.txt"), "A penalty clause was added for late delivery.");

        var update = UpdateIndexTool.UpdateIndex(_license, _cfg, "t");
        Assert.Contains("1 updated", Text(update));

        var search = Json(SearchTool.Search(_license, _cfg, "penalty", indexName: "t"));
        Assert.Equal(1, search.GetProperty("documentsFound").GetInt32());
    }

    [Fact]
    public void SearchMissingIndex_IsErrorAndListsIndexes()
    {
        AddToIndexTool.AddToIndex(_license, _cfg, indexName: "real");

        var r = SearchTool.Search(_license, _cfg, "x", indexName: "ghost");

        Assert.True(r.IsError);
        Assert.StartsWith("Search failed for 'ghost'", Text(r));
        Assert.Contains("'real'", Text(r));
    }

    [Fact]
    public void GetIndexInfo_BeforeAndAfterIndexing()
    {
        var before = Json(GetIndexInfoTool.GetIndexInfo(_license, _cfg));
        Assert.False(before.GetProperty("exists").GetBoolean());

        AddToIndexTool.AddToIndex(_license, _cfg);
        var after = Json(GetIndexInfoTool.GetIndexInfo(_license, _cfg));
        Assert.True(after.GetProperty("exists").GetBoolean());
        Assert.Equal(2, after.GetProperty("documentCount").GetInt32());
        Assert.Equal(".", after.GetProperty("indexedPaths")[0].GetString());
        Assert.False(string.IsNullOrEmpty(after.GetProperty("version").GetString()));
    }

    [Fact]
    public void CreateIndex_Recreate_EmptiesTheIndex()
    {
        AddToIndexTool.AddToIndex(_license, _cfg, indexName: "t");

        var r = CreateIndexTool.CreateIndex(_license, _cfg, "t", recreate: true);
        Assert.Contains("recreated", Text(r));

        var info = Json(GetIndexInfoTool.GetIndexInfo(_license, _cfg, "t"));
        Assert.Equal(0, info.GetProperty("documentCount").GetInt32());
    }

    [Fact]
    public void IndexFolderInsideStorage_IsNotIndexedItself()
    {
        var cfg = new SearchConfig
        {
            StorageRoot = _cfg.StorageRoot,
            IndexRoot = Path.Combine(_cfg.StorageRoot, SearchConfig.DefaultIndexFolderName)
        };

        AddToIndexTool.AddToIndex(_license, cfg, indexName: "inner");
        var again = AddToIndexTool.AddToIndex(_license, cfg, indexName: "inner");
        var info = Json(GetIndexInfoTool.GetIndexInfo(_license, cfg, "inner"));

        Assert.NotEqual(true, again.IsError);
        Assert.Equal(2, info.GetProperty("documentCount").GetInt32());
    }

    [Theory]
    [InlineData("quarterly revenue", true)]
    [InlineData("\"quarterly revenue\"", false)]
    [InlineData("revenue AND forecast", false)]
    [InlineData("revenue", false)]
    [InlineData("filename: report", false)]
    public void QueryHint_OnlyForUnquotedMultiWord(string query, bool expectHint)
    {
        Assert.Equal(expectHint, SearchTool.QueryHint(query) is not null);
    }
}
