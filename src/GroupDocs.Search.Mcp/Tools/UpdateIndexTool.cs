using System.ComponentModel;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Search.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GroupDocs.Search.Mcp.Tools;

[McpServerToolType]
public static class UpdateIndexTool
{
    [McpServerTool(Name = "update_index"), Description(
        "Brings a search index up to date with the files on disk: re-reads documents that changed, adds new files that appeared inside already-indexed folders, and drops documents whose files were deleted. " +
        "Call this when the user says files were edited, added or removed, asks to 'refresh' or 're-index', or before searching an index that may be stale. " +
        "It only looks at paths already in the index; to index a new folder use add_to_index. " +
        "Returns a short report: documents added, updated and removed, and the total now in the index. " +
        "In evaluation mode new files are only added while the index holds fewer than 100 documents. " +
        "On failure the response starts with 'Index update failed for' followed by the exception type and message, and isError is set.")]
    public static CallToolResult UpdateIndex(
        ILicenseManager licenseManager,
        SearchConfig config,
        [Description("Name of the index to update, e.g. 'contracts'. Omit to use the default index.")] string? indexName = null)
    {
        var notice = Evaluation.For(licenseManager);
        var (name, folder) = IndexPaths.ResolveIndex(config, indexName);

        if (!IndexStore.Exists(folder))
            return ToolError.Fail("Index update", name, MissingIndex.Reason(config, name));

        try
        {
            lock (IndexStore.Gate(folder))
            {
                using var index = IndexStore.Open(config, folder, out var conversion);
                var started = DateTime.Now.AddSeconds(-1);
                index.Update(new UpdateOptions());
                var report = IndexStore.LatestReportSince(index, started);
                var total = index.GetIndexedDocuments().Length;

                var added = report?.IndexedDocuments?.Length ?? 0;
                var updated = report?.UpdatedDocuments?.Length ?? 0;
                var removed = report?.RemovedDocuments?.Length ?? 0;

                var text = added + updated + removed == 0
                    ? $"Index '{name}' is up to date: no changes found, {total} document(s) in the index."
                    : $"Index '{name}' updated: {added} added, {updated} updated, {removed} removed; {total} document(s) in the index.";
                if (report?.Errors is { Length: > 0 } errors)
                    text += $"\nCould not read {errors.Length} item(s): {string.Join("; ", errors.Take(5))}{(errors.Length > 5 ? "; …" : string.Empty)}";
                if (conversion is not null) text += "\n" + conversion;

                return ToolError.Ok(Evaluation.Append(text, notice));
            }
        }
        catch (Exception ex)
        {
            return ToolError.Fail("Index update", name, ex);
        }
    }
}
