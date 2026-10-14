using System.ComponentModel;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Search.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GroupDocs.Search.Mcp.Tools;

[McpServerToolType]
public static class AddToIndexTool
{
    [McpServerTool(Name = "add_to_index"), Description(
        "Adds documents to a full-text search index so they can be searched: a single file, a folder (indexed recursively), or the whole storage folder. " +
        "Indexes PDF, DOCX, DOC, XLSX, XLS, PPTX, PPT, ODT, ODS, ODP, RTF, TXT, HTML, EPUB, FB2, EML, EMLX, MSG, PST, OST, ONE, ZIP and 70+ more formats, including documents inside archives and email attachments. " +
        "Call this immediately whenever the user asks to index documents, make files searchable, or search documents that have not been indexed yet — the index is created on demand, so create_index is not required first. " +
        "Do NOT pre-check whether the path exists; pass what the user said. documentsPath is relative to the storage folder ('contracts', 'reports/2026/q3.pdf'); omit it to index everything in storage. " +
        "Adding a path that is already indexed re-reads only what changed. Large folders take time: indexing reads every document. " +
        "Returns a short report: documents added in this call, total documents in the index, the index name, and any files the engine could not read. " +
        "In evaluation mode an index holds at most 100 documents; indexing stops at that count. " +
        "On failure the response starts with 'Indexing failed for' followed by the exception type and message, and isError is set; a missing path lists the files and folders that do exist.")]
    public static CallToolResult AddToIndex(
        ILicenseManager licenseManager,
        SearchConfig config,
        [Description("File or folder to index, relative to the storage folder, e.g. 'contracts' or 'contracts/lease.pdf'. Omit to index the whole storage folder.")] string? documentsPath = null,
        [Description("Name of the index to add to, e.g. 'contracts'. Created if it does not exist. Omit to use the default index.")] string? indexName = null)
    {
        var notice = Evaluation.For(licenseManager);
        var (name, folder) = IndexPaths.ResolveIndex(config, indexName);
        var source = IndexPaths.ResolveDocuments(config, documentsPath);
        var sourceLabel = IndexPaths.ToStorageRelative(config, source);

        try
        {
            lock (IndexStore.Gate(folder))
            {
                string? conversion = null;
                IndexStore.EnsureWritable(config, folder);
                using var index = IndexStore.Exists(folder)
                    ? IndexStore.Open(config, folder, out conversion)
                    : IndexStore.Create(config, folder, overwrite: false);

                var before = index.GetIndexedDocuments().Length;
                var started = DateTime.Now.AddSeconds(-1);
                index.Add(source, new IndexingOptions());
                var after = index.GetIndexedDocuments().Length;
                var report = IndexStore.LatestReportSince(index, started);

                var text = $"Indexed {IndexPaths.Describe(config, source)} into index '{name}': {Math.Max(0, after - before)} document(s) added, {after} in the index.";
                if (after == before)
                    text += " Nothing new was found — the path may already be indexed, contain no supported documents, or the evaluation cap may be reached.";
                if (report?.Errors is { Length: > 0 } errors)
                    text += $"\nCould not read {errors.Length} item(s): {string.Join("; ", errors.Take(5))}{(errors.Length > 5 ? "; …" : string.Empty)}";
                if (notice is not null && after >= Evaluation.MaxIndexedDocuments)
                    text += $"\nThe index has reached the evaluation cap of {Evaluation.MaxIndexedDocuments} documents; further documents were not indexed.";
                if (conversion is not null) text += "\n" + conversion;

                return ToolError.Ok(Evaluation.Append(text, notice));
            }
        }
        catch (Exception ex)
        {
            return ToolError.Fail("Indexing", sourceLabel, ex);
        }
    }
}
