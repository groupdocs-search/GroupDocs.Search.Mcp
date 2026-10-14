using System.ComponentModel;
using System.Text.Json;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Search.Results;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GroupDocs.Search.Mcp.Tools;

[McpServerToolType]
public static class GetIndexInfoTool
{
    [McpServerTool(Name = "get_index_info"), Description(
        "Describes a search index without changing it: whether it exists, how many documents it holds, which paths are indexed, which documents failed to index, its format version, and the other indexes available. " +
        "Call this when the user asks what is indexed, how big an index is, which indexes exist, or before searching to confirm the right documents are in the index. " +
        "Safe to call with no arguments: it reports on the default index even when that index does not exist yet. " +
        "Also lists the document formats the engine can index when includeSupportedFormats is true. " +
        "Returns JSON: `index`, `exists`, `documentCount`, `indexedPaths` (relative to the storage folder), `documentsWithErrors`, `version`, `segmentCount`, `termCount`, `status`, `availableIndexes`, optional `supportedFormats` and `evaluation`. " +
        "On failure the response starts with 'Index-info lookup failed for' followed by the exception type and message, and isError is set.")]
    public static CallToolResult GetIndexInfo(
        ILicenseManager licenseManager,
        SearchConfig config,
        [Description("Name of the index, e.g. 'contracts'. Omit to use the default index.")] string? indexName = null,
        [Description("Also list the file formats the engine can index. Default false.")] bool includeSupportedFormats = false)
    {
        var notice = Evaluation.For(licenseManager);
        var (name, folder) = IndexPaths.ResolveIndex(config, indexName);

        try
        {
            var formats = includeSupportedFormats
                ? FileType.GetSupportedFileTypes()
                    .Select(t => t.Extension.TrimStart('.').ToUpperInvariant())
                    .Where(e => e.Length > 0)
                    .Distinct()
                    .OrderBy(e => e)
                    .ToArray()
                : null;

            if (!IndexStore.Exists(folder))
            {
                return ToolError.Ok(JsonSerializer.Serialize(new
                {
                    index = name,
                    exists = false,
                    documentCount = 0,
                    availableIndexes = IndexPaths.ListIndexes(config),
                    hint = $"Call add_to_index (indexName: '{name}') to create this index and index documents.",
                    supportedFormats = formats,
                    evaluation = notice
                }, SearchTool.JsonOptions));
            }

            lock (IndexStore.Gate(folder))
            {
                using var index = IndexStore.Open(config, folder, out var conversion);
                var documents = index.GetIndexedDocuments();
                var info = index.IndexInfo;

                return ToolError.Ok(JsonSerializer.Serialize(new
                {
                    index = name,
                    exists = true,
                    documentCount = documents.Length,
                    indexedPaths = index.GetIndexedPaths().Select(p => IndexPaths.ToStorageRelative(config, p)).ToArray(),
                    documentsWithErrors = documents.Where(d => d.IndexedWithError)
                        .Select(d => IndexPaths.ToStorageRelative(config, d.FilePath)).Take(50).ToArray(),
                    version = info?.Version,
                    segmentCount = info?.SegmentCount,
                    termCount = info?.TermCount,
                    status = info?.IndexStatus.ToString(),
                    availableIndexes = IndexPaths.ListIndexes(config),
                    supportedFormats = formats,
                    conversion,
                    evaluation = notice
                }, SearchTool.JsonOptions));
            }
        }
        catch (Exception ex)
        {
            return ToolError.Fail("Index-info lookup", name, ex);
        }
    }
}
