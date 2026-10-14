using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Search.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GroupDocs.Search.Mcp.Tools;

[McpServerToolType]
public static class SearchTool
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool(Name = "search"), Description(
        "Searches an index for documents that contain the query and returns the matching files ranked by relevance, with occurrence counts and the terms found. " +
        "Call this whenever the user asks which documents mention something, to find, look up or search content across their files — after the documents have been indexed with add_to_index. " +
        "Query syntax (GroupDocs.Search DSL): a single word ('invoice'); a phrase in double quotes ('\"force majeure\"') — always quote multi-word phrases, unquoted words separated by spaces do not match; " +
        "combine with AND or &, OR or |, NOT or !, and parentheses ('termination AND (notice OR penalty)'); '?' replaces one character ('?ffect', 'princip?(2~4)'); " +
        "date ranges daterange(2026-01-01 ~~ 2026-06-30); number ranges 100 ~~ 500; field prefixes filename: or content: ('filename: invoice'); a query starting with ^ is a regular expression ('^inv[0-9]+'). " +
        "There is no trailing '*' wildcard — use ? patterns or fuzzy=true for spelling variants. " +
        "To read the matching paragraphs rather than just the file names, call get_text_fragments with the same query. " +
        "Returns JSON: `query`, `index`, `documentsFound`, `documentsReturned`, `totalOccurrences`, `truncated`, optional `evaluation`, and `documents` (array of { `filePath` relative to the storage folder, `innerPath` for items inside archives or emails, `relevance`, `occurrences`, `terms`, `fields` }). " +
        "documentsFound 0 means nothing matched — not an error. In evaluation mode at most 10 documents are returned. " +
        "On failure the response starts with 'Search failed for' followed by the reason, and isError is set; an unknown index lists the indexes that exist.")]
    public static CallToolResult Search(
        ILicenseManager licenseManager,
        SearchConfig config,
        [Description("Search query, e.g. 'invoice', 'termination AND notice', '\"force majeure\"' (quoted phrase), 'filename: lease', 'daterange(2026-01-01 ~~ 2026-12-31)'.")] string query,
        [Description("Maximum number of documents to return. Default 10.")] int maxResults = 10,
        [Description("Also match words with small spelling differences (fuzzy search). Default false.")] bool fuzzy = false,
        [Description("Match letter case exactly. Default false.")] bool caseSensitive = false,
        [Description("Name of the index to search, e.g. 'contracts'. Omit to use the default index.")] string? indexName = null)
    {
        var notice = Evaluation.For(licenseManager);
        var (name, folder) = IndexPaths.ResolveIndex(config, indexName);

        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("query must not be empty.");
        if (!IndexStore.Exists(folder))
            return ToolError.Fail("Search", name, MissingIndex.Reason(config, name));

        try
        {
            lock (IndexStore.Gate(folder))
            {
                using var index = IndexStore.Open(config, folder, out var conversion);
                var options = new SearchOptions { UseCaseSensitiveSearch = caseSensitive };
                if (fuzzy) options.FuzzySearch.Enabled = true;

                var result = index.Search(query, options);
                var take = Math.Clamp(maxResults, 1, 1000);
                var documents = result.Take(take).Select(d => new
                {
                    filePath = IndexPaths.ToStorageRelative(config, d.DocumentInfo.FilePath),
                    innerPath = string.IsNullOrEmpty(d.DocumentInfo.InnerPath) ? null : d.DocumentInfo.InnerPath,
                    relevance = Math.Round(d.Relevance, 3),
                    occurrences = d.OccurrenceCount,
                    terms = d.Terms,
                    fields = d.FoundFields.Select(f => new { field = f.FieldName, occurrences = f.OccurrenceCount, terms = f.Terms }).ToArray()
                }).ToArray();

                var payload = new
                {
                    query,
                    index = name,
                    documentsFound = result.DocumentCount,
                    documentsReturned = documents.Length,
                    totalOccurrences = result.OccurrenceCount,
                    truncated = result.Truncated || result.DocumentCount > documents.Length,
                    warnings = string.IsNullOrWhiteSpace(result.Warnings) ? null : result.Warnings,
                    hint = result.DocumentCount == 0 ? QueryHint(query) : null,
                    evaluation = notice,
                    conversion,
                    documents
                };
                return ToolError.Ok(JsonSerializer.Serialize(payload, JsonOptions));
            }
        }
        catch (Exception ex)
        {
            return ToolError.Fail("Search", name, ex);
        }
    }

    /// <summary>
    /// GroupDocs.Search matches nothing for unquoted multi-word input (verified on 26.10.0), which an
    /// agent cannot tell apart from "no such text". Say so when it is the likely cause.
    /// </summary>
    internal static string? QueryHint(string query)
    {
        var q = query.Trim();
        var hasSpace = q.Contains(' ');
        var hasSyntax = q.IndexOfAny(['"', '&', '|', '!', '(', ':', '^', '~']) >= 0
                        || q.Contains(" AND ") || q.Contains(" OR ") || q.StartsWith("NOT ");
        return hasSpace && !hasSyntax
            ? $"Unquoted words separated by spaces do not match. Search the phrase as \"{q}\", or combine the words with AND / OR, e.g. {string.Join(" AND ", q.Split(' ', StringSplitOptions.RemoveEmptyEntries))}."
            : null;
    }
}
