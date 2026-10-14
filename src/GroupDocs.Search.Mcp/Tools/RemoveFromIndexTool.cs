using System.ComponentModel;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Search.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GroupDocs.Search.Mcp.Tools;

[McpServerToolType]
public static class RemoveFromIndexTool
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    [McpServerTool(Name = "remove_from_index"), Description(
        "Removes an indexed path — a folder or file that was passed to add_to_index — from a search index, so its documents stop appearing in search results. " +
        "The files on disk are not touched. " +
        "Call this when the user wants documents excluded from search, e.g. 'stop searching the drafts folder' or 'remove old-contract.pdf from the index'. " +
        "documentPath is relative to the storage folder and must be one of the index's indexed paths (get_index_info lists them). " +
        "The engine removes whole indexed paths only: a single file inside an indexed folder cannot be removed on its own — remove the folder, then add back the files or sub-folders you want with add_to_index. " +
        "Returns a sentence with the number of documents removed and the total left in the index. " +
        "On failure the response starts with 'Removal failed for' followed by the reason, and isError is set; a path that is not an indexed path lists the ones that are.")]
    public static CallToolResult RemoveFromIndex(
        ILicenseManager licenseManager,
        SearchConfig config,
        [Description("File or folder to remove, relative to the storage folder, e.g. 'drafts' or 'contracts/old-contract.pdf'.")] string documentPath,
        [Description("Name of the index, e.g. 'contracts'. Omit to use the default index.")] string? indexName = null)
    {
        var notice = Evaluation.For(licenseManager);
        var (name, folder) = IndexPaths.ResolveIndex(config, indexName);
        var target = IndexPaths.ResolveDocuments(config, documentPath, mustExist: false);
        var label = IndexPaths.ToStorageRelative(config, target);

        if (!IndexStore.Exists(folder))
            return ToolError.Fail("Removal", label, MissingIndex.Reason(config, name));

        try
        {
            lock (IndexStore.Gate(folder))
            {
                IndexStore.EnsureWritable(config, folder);
                using var index = IndexStore.Open(config, folder, out _);
                var before = index.GetIndexedDocuments().Length;
                var targetTrimmed = Path.TrimEndingDirectorySeparator(target);

                var indexedPaths = index.GetIndexedPaths();
                var exactPaths = indexedPaths
                    .Where(p => string.Equals(Path.TrimEndingDirectorySeparator(p), targetTrimmed, PathComparison))
                    .ToArray();

                if (exactPaths.Length == 0)
                {
                    var container = indexedPaths.FirstOrDefault(p =>
                        IndexPaths.IsUnder(targetTrimmed, p));
                    var listed = indexedPaths.Length == 0
                        ? "The index has no indexed paths."
                        : $"Indexed paths: {string.Join(", ", indexedPaths.Select(p => $"'{IndexPaths.ToStorageRelative(config, p)}'"))}.";
                    var reason = container is null
                        ? $"'{label}' is not an indexed path of index '{name}'. {listed}"
                        : $"'{label}' is inside the indexed folder '{IndexPaths.ToStorageRelative(config, container)}'. " +
                          "GroupDocs.Search removes whole indexed paths only: remove that folder, then add back the files " +
                          $"or sub-folders you want with add_to_index. {listed}";
                    return ToolError.Fail("Removal", label, reason);
                }

                var deleted = index.Delete(exactPaths, new UpdateOptions());
                if (deleted.FailedCount > 0 && deleted.SuccessCount == 0)
                    return ToolError.Fail("Removal", label, string.Join("; ", deleted.Errors ?? []));

                var after = index.GetIndexedDocuments().Length;
                return ToolError.Ok(Evaluation.Append(
                    $"Removed '{label}' from index '{name}': {Math.Max(0, before - after)} document(s) removed, {after} left.",
                    notice));
            }
        }
        catch (Exception ex)
        {
            return ToolError.Fail("Removal", label, ex);
        }
    }
}
