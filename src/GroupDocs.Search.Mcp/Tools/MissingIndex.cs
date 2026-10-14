namespace GroupDocs.Search.Mcp.Tools;

/// <summary>The one recovery message for "that index does not exist", listing the ones that do.</summary>
internal static class MissingIndex
{
    public static string Reason(SearchConfig config, string name)
    {
        var existing = IndexPaths.ListIndexes(config);
        var list = existing.Length == 0
            ? "No indexes exist yet."
            : $"Existing indexes: {string.Join(", ", existing.Select(n => $"'{n}'"))}.";
        return $"index '{name}' does not exist. {list} Call add_to_index (indexName: '{name}') to create it and index documents.";
    }
}
