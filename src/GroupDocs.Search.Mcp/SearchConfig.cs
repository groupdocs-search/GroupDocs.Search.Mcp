namespace GroupDocs.Search.Mcp;

/// <summary>
/// Where documents are read from and where search indexes live.
/// </summary>
/// <remarks>
/// GroupDocs.Search is the only stateful product in the family: its indexes persist between tool
/// calls and between server restarts, so they need a home of their own. Tool arguments are always
/// relative — documents to <see cref="StorageRoot"/>, index names to <see cref="IndexRoot"/> — which
/// keeps the server inside the folders the user configured or mounted.
/// </remarks>
public sealed class SearchConfig
{
    /// <summary>Environment variable that overrides <see cref="IndexRoot"/>.</summary>
    public const string IndexPathVariable = "GROUPDOCS_MCP_INDEX_PATH";

    /// <summary>Same variable GroupDocs.Mcp.Local.Storage reads for the document folder.</summary>
    public const string StoragePathVariable = "GROUPDOCS_MCP_STORAGE_PATH";

    /// <summary>Same variable GroupDocs.Mcp.Local.Storage reads for written files.</summary>
    public const string OutputPathVariable = "GROUPDOCS_MCP_OUTPUT_PATH";

    /// <summary>Folder created under the output (or storage) folder when no index path is set.</summary>
    public const string DefaultIndexFolderName = ".groupdocs-search-index";

    /// <summary>Storage default passed to <c>AddLocalStorage</c> in Program.cs.</summary>
    public const string DefaultStorageFolder = "./Files";

    /// <summary>Absolute folder that holds the documents to index.</summary>
    public string StorageRoot { get; init; } = string.Empty;

    /// <summary>Absolute folder that holds the indexes, one sub-folder per index name.</summary>
    public string IndexRoot { get; init; } = string.Empty;

    /// <summary>Index used when a tool call does not name one.</summary>
    public string DefaultIndexName { get; init; } = "default";

    /// <summary>
    /// Resolves the configuration the same way GroupDocs.Mcp.Local.Storage resolves its folders,
    /// so the documents this server indexes are exactly the ones the storage layer serves.
    /// </summary>
    /// <param name="getVariable">Environment lookup, injectable for tests.</param>
    /// <param name="currentDirectory">Base for relative paths.</param>
    public static SearchConfig FromEnvironment(Func<string, string?> getVariable, string currentDirectory)
    {
        string Full(string path) => Path.GetFullPath(Path.Combine(currentDirectory, path));
        static string? Value(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        var storage = Full(Value(getVariable(StoragePathVariable)) ?? DefaultStorageFolder);
        var output = Value(getVariable(OutputPathVariable)) is { } o ? Full(o) : storage;
        var index = Value(getVariable(IndexPathVariable)) is { } i
            ? Full(i)
            : Path.Combine(output, DefaultIndexFolderName);

        return new SearchConfig
        {
            StorageRoot = Path.TrimEndingDirectorySeparator(storage),
            IndexRoot = Path.TrimEndingDirectorySeparator(index)
        };
    }
}
