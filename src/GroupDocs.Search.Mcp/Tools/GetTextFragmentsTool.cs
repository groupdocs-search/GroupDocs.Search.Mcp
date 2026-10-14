using System.ComponentModel;
using System.Text;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Search.Highlighters;
using GroupDocs.Search.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GroupDocs.Search.Mcp.Tools;

[McpServerToolType]
public static class GetTextFragmentsTool
{
    [McpServerTool(Name = "get_text_fragments"), Description(
        "Returns the passages of indexed documents that match a query — each match with the words around it — so the user can read what a document says without opening it. " +
        "Call this when the user wants to see the matching text, quote a clause, or answer a question from their documents ('what does the lease say about termination?'), usually after search has shown which files match. " +
        "Takes the same query syntax as search. Works on documents indexed by this server (text is stored in the index). " +
        "Returns plain text: a header line with the document count, then per document a '=== <file> (<n> occurrence(s)) ===' line followed by up to maxFragmentsPerDocument fragments, each prefixed with the field it came from. " +
        "In evaluation mode at most 10 documents are considered. " +
        "On failure the response starts with 'Fragment extraction failed for' followed by the reason, and isError is set.")]
    public static CallToolResult GetTextFragments(
        ILicenseManager licenseManager,
        SearchConfig config,
        [Description("Search query, same syntax as the search tool.")] string query,
        [Description("Words of context before each match. Default 8.")] int termsBefore = 8,
        [Description("Words of context after each match. Default 8.")] int termsAfter = 8,
        [Description("Maximum fragments per document. Default 3.")] int maxFragmentsPerDocument = 3,
        [Description("Maximum documents to include. Default 5.")] int maxDocuments = 5,
        [Description("Name of the index, e.g. 'contracts'. Omit to use the default index.")] string? indexName = null)
    {
        var notice = Evaluation.For(licenseManager);
        var (name, folder) = IndexPaths.ResolveIndex(config, indexName);

        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("query must not be empty.");
        if (!IndexStore.Exists(folder))
            return ToolError.Fail("Fragment extraction", name, MissingIndex.Reason(config, name));

        try
        {
            lock (IndexStore.Gate(folder))
            {
                using var index = IndexStore.Open(config, folder, out _);
                var result = index.Search(query);

                if (result.DocumentCount == 0)
                    return ToolError.Ok(Evaluation.Append($"No documents in index '{name}' match '{query}'.", notice));

                termsBefore = Math.Clamp(termsBefore, 0, 50);
                termsAfter = Math.Clamp(termsAfter, 0, 50);
                maxFragmentsPerDocument = Math.Clamp(maxFragmentsPerDocument, 1, 20);
                maxDocuments = Math.Clamp(maxDocuments, 1, 50);

                var highlightOptions = new HighlightOptions
                {
                    TermsBefore = termsBefore,
                    TermsAfter = termsAfter,
                    TermsTotal = (termsBefore + termsAfter + 1) * maxFragmentsPerDocument
                };

                var sb = new StringBuilder();
                sb.AppendLine($"Query '{query}' in index '{name}': {result.DocumentCount} document(s) match.");

                foreach (var doc in result.Take(maxDocuments))
                {
                    var file = IndexPaths.ToStorageRelative(config, doc.DocumentInfo.FilePath);
                    var inner = string.IsNullOrEmpty(doc.DocumentInfo.InnerPath) ? string.Empty : $" → {doc.DocumentInfo.InnerPath}";
                    sb.AppendLine();
                    sb.AppendLine($"=== {file}{inner} ({doc.OccurrenceCount} occurrence(s)) ===");

                    var highlighter = new FragmentHighlighter(OutputFormat.PlainText);
                    index.Highlight(doc, highlighter, highlightOptions);

                    var written = 0;
                    foreach (var container in highlighter.GetResult())
                    {
                        foreach (var fragment in container.GetFragments())
                        {
                            if (written >= maxFragmentsPerDocument) break;
                            sb.AppendLine($"  [{container.FieldName}] …{fragment.Trim()}…");
                            written++;
                        }
                        if (written >= maxFragmentsPerDocument) break;
                    }

                    if (written == 0)
                        sb.AppendLine("  (no stored text for this document — re-add it with add_to_index to enable fragments)");
                }

                if (result.DocumentCount > maxDocuments)
                    sb.AppendLine().AppendLine($"…and {result.DocumentCount - maxDocuments} more document(s). Raise maxDocuments to see them.");

                return ToolError.Ok(Evaluation.Append(sb.ToString().TrimEnd(), notice));
            }
        }
        catch (Exception ex)
        {
            return ToolError.Fail("Fragment extraction", name, ex);
        }
    }
}
