using System.ComponentModel;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GroupDocs.Search.Mcp.Tools;

[McpServerToolType]
public static class CreateIndexTool
{
    [McpServerTool(Name = "create_index"), Description(
        "Creates a named full-text search index, or opens it when it already exists, and reports what it holds. " +
        "An index is a persistent, searchable copy of the text of your documents; it survives server restarts and lives in the server's index folder, never next to the documents. " +
        "You rarely need to call this first — add_to_index creates the index on demand. Call it when the user wants a separate index for a set of documents ('make an index called contracts'), " +
        "to check whether an index exists, or with recreate=true to wipe an index and start over. " +
        "Indexes written by GroupDocs.Search 22.7–26.8 are converted to the current format automatically and the original is kept. " +
        "Pass index names such as 'contracts' or 'legal/2026', never filesystem paths. " +
        "Returns one or two sentences: whether the index was created or opened, and its document count and indexed paths (relative to the storage folder). " +
        "In evaluation mode an index holds at most 100 documents. " +
        "On failure the response starts with 'Index creation failed for' followed by the exception type and message, and isError is set.")]
    public static CallToolResult CreateIndex(
        ILicenseManager licenseManager,
        SearchConfig config,
        [Description("Name of the index, for example 'contracts'. Omit to use the default index.")] string? indexName = null,
        [Description("Delete the index's existing content and start empty. Default false.")] bool recreate = false)
    {
        var notice = Evaluation.For(licenseManager);
        var (name, folder) = IndexPaths.ResolveIndex(config, indexName);

        try
        {
            lock (IndexStore.Gate(folder))
            {
                if (recreate || !IndexStore.Exists(folder))
                {
                    var existed = IndexStore.Exists(folder);
                    using var created = IndexStore.Create(config, folder, overwrite: recreate);
                    var verb = existed ? "recreated (empty)" : "created";
                    return ToolError.Ok(Evaluation.Append(
                        $"Index '{name}' {verb}. Add documents with add_to_index (indexName: '{name}').", notice));
                }

                using var index = IndexStore.Open(config, folder, out var conversion);
                var documents = index.GetIndexedDocuments().Length;
                var paths = index.GetIndexedPaths().Select(p => IndexPaths.Describe(config, p)).ToArray();
                var pathText = paths.Length == 0
                    ? "no indexed paths yet"
                    : $"indexed paths: {string.Join(", ", paths.Take(10))}{(paths.Length > 10 ? $", …(+{paths.Length - 10} more)" : string.Empty)}";

                var text = $"Index '{name}' already exists and was opened: {documents} document(s), {pathText}.";
                if (conversion is not null) text += " " + conversion;
                return ToolError.Ok(Evaluation.Append(text, notice));
            }
        }
        catch (Exception ex)
        {
            return ToolError.Fail("Index creation", name, ex);
        }
    }
}
